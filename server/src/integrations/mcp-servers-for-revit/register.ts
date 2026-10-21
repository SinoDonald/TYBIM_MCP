import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { z } from 'zod';
import type { RevitBridge } from '../../bridge.js';
import { invocation, type Invocation } from './utils/ConnectionManager.js';
import { registrars } from './registrars.js';
export const upstreamReadOnly = new Set(['get_current_view_info', 'get_current_view_elements', 'get_available_family_types',
  'get_selected_elements', 'get_material_quantities', 'ai_element_filter', 'analyze_model_statistics', 'export_room_data', 'say_hello', 'query_stored_data']);
const localTools = new Set(['store_project_data', 'store_room_data', 'query_stored_data']);
const localOperations = new Map<string, { signature: string; result: unknown }>();
function hasFailure(value: any): boolean {
  return !!value && typeof value === 'object' && (value.status === 'rolledBack' || value.status === 'failed' || value.success === false || value.Success === false || Object.values(value).some(v => typeof v === 'object' && hasFailure(v)));
}
const output = (data: any) => {
  if (typeof data?.result?.ImageBase64 === 'string') {
    const { ImageBase64, ...metadata } = data.result;
    return { content: [{ type: 'text' as const, text: JSON.stringify({ ...data, result: metadata }) },
      { type: 'image' as const, data: ImageBase64, mimeType: data.result.MimeType || 'image/png' }], isError: hasFailure(data) };
  }
  return { content: [{ type: 'text' as const, text: JSON.stringify(data, (_, value) => typeof value === 'bigint' ? Number(value) : value) }], isError: hasFailure(data) };
};
export function registerUpstreamTools(server: McpServer, bridge: RevitBridge): void {
  const proxy = { tool(name: string, description: string, schema: z.ZodRawShape, handler: (args: any, extra: any) => Promise<any>) {
    const local = localTools.has(name), writes = !upstreamReadOnly.has(name);
    server.registerTool('revit_upstream_' + name, {
      description: description + (local ? ' Local SQLite data only; not a live Revit query.' : ' Uses TYBIM bridge. Read revit_project_info first for documentId.')
        + (writes ? ' Embedded chat shows exact arguments for confirmation. Use a fresh operationId (32 hex characters); on timeout query revit_upstream_operation_status before retry.' : ''),
      inputSchema: { ...schema, documentId: z.string().min(1), ...(writes ? { operationId: z.string().regex(/^[a-f0-9]{32}$/) } : {}) },
      annotations: { readOnlyHint: !writes, destructiveHint: writes, idempotentHint: writes || name !== 'say_hello', openWorldHint: name === 'send_code_to_revit' }
    }, async (args, extra) => {
      try {
        const key = args.documentId + ':' + args.operationId, signature = JSON.stringify({ name, args });
        if (local && writes && localOperations.has(key)) {
          const old = localOperations.get(key)!;
          if (old.signature !== signature) throw new Error('OPERATION_ID_REUSED_WITH_DIFFERENT_ARGUMENTS');
          return output(old.result);
        }
        if (local && writes && localOperations.size >= 1000) throw new Error('LOCAL_OPERATION_STORE_FULL');
        const context: Invocation = { bridge, documentId: args.documentId, operationId: args.operationId };
        const result = await invocation.run(context, () => handler(args, extra));
        if (context.error) throw context.error;
        // Use actual bridge result rather than upstream "execution successful" text.
        let data = context.response;
        if (local) {
          const body = result.content?.find((c: any) => c.type === 'text')?.text;
          data = body ? JSON.parse(body) : result;
          if (writes) {
            data = { operationId: args.operationId, documentId: args.documentId, status: result.isError || hasFailure(data) ? 'rolledBack' : 'completed', result: data };
            localOperations.set(key, { signature, result: data });
          }
        }
        if (data === undefined) throw new Error('UPSTREAM_RETURNED_NO_RESULT');
        return output(data);
      } catch (error) { return output({ success: false, error: error instanceof Error ? error.message : String(error) }); }
    });
  } };
  for (const register of registrars) register(proxy as unknown as McpServer);
  server.registerTool('revit_upstream_operation_status', {
    description: 'Read a prior upstream operation using documentId and operationId. Records last for this Revit/session (server process for local database writes). Not found does not prove no change after a restart.',
    inputSchema: { documentId: z.string().min(1), operationId: z.string().regex(/^[a-f0-9]{32}$/) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
  }, async args => {
    const local = localOperations.get(args.documentId + ':' + args.operationId);
    if (local) return output(local.result);
    try { return output(await bridge.call('upstream.operation_status', args)); }
    catch (error) { return output({ success: false, error: error instanceof Error ? error.message : String(error) }); }
  });
}
