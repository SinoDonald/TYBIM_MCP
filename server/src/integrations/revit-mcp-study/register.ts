import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { AjvJsonSchemaValidator } from '@modelcontextprotocol/sdk/validation/ajv';
import { z } from 'zod';
import catalog from './catalog.json' with { type: 'json' };
import type { RevitBridge } from '../../bridge.js';

// Zod supplies discoverable parameter types; the pinned complete upstream JSON
// schema is ALSO validated by AJV so constraints not represented here are kept.
function toZod(s: any): z.ZodTypeAny {
  let type: z.ZodTypeAny;
  if (s.enum?.length) type = z.union(s.enum.map((v: any) => z.literal(v)) as any);
  else if (s.const !== undefined) type = z.literal(s.const);
  else if (s.anyOf || s.oneOf) type = z.union((s.anyOf || s.oneOf).map(toZod) as any);
  else if (Array.isArray(s.type)) type = z.union(s.type.map((t: string) => toZod({ ...s, type: t })) as any);
  else switch (s.type) {
    case 'string': {
      let value = z.string();
      if (s.minLength !== undefined) value = value.min(s.minLength);
      if (s.maxLength !== undefined) value = value.max(s.maxLength);
      if (s.pattern) value = value.regex(new RegExp(s.pattern));
      type = value; break;
    }
    case 'number': case 'integer': {
      let value = s.type === 'integer' ? z.number().int() : z.number();
      if (s.minimum !== undefined) value = value.min(s.minimum);
      if (s.maximum !== undefined) value = value.max(s.maximum);
      type = value; break;
    }
    case 'boolean': type = z.boolean(); break;
    case 'null': type = z.null(); break;
    case 'array': {
      let value = z.array(toZod(s.items || {}));
      if (s.minItems !== undefined) value = value.min(s.minItems);
      if (s.maxItems !== undefined) value = value.max(s.maxItems);
      type = value; break;
    }
    case 'object': type = z.object(shape(s)).passthrough(); break;
    default: type = z.any();
  }
  if (s.description) type = type.describe(s.description);
  if (s.default !== undefined) type = type.default(s.default);
  return type;
}
function shape(schema: any): z.ZodRawShape {
  return Object.fromEntries(Object.entries(schema.properties || {}).map(([key, value]) => {
    const type = toZod(value);
    return [key, schema.required?.includes(key) ? type : type.optional()];
  }));
}
function failed(value: any): boolean { return !!value && (value.status === 'rolledBack' || value.status === 'failed' || value.Success === false || value.success === false); }
export function registerStudyTools(server: McpServer, bridge: RevitBridge): void {
  const validator = new AjvJsonSchemaValidator();
  const slow = new Set(['read_source_file_sheets', 'copy_sheets_from_file', 'sync_sheet_parameters_from_source', 'inject_green_material_into_family',
    'open_document', 'save_document', 'reload_links', 'capture_view_image', 'run_camera_keyplan', 'list_cameras', 'run_camera_overview', 'create_camera_sheet']);
  for (const entry of catalog.filter(entry => entry.implemented)) {
    const tool = entry.tool, writes = tool.annotations?.readOnlyHint !== true;
    const validate = validator.getValidator<Record<string, unknown>>(tool.inputSchema as any);
    server.registerTool('revit_study_' + tool.name, {
      title: tool.title, description: tool.description + '\n來源：REVIT_MCP_study。先取得 TYBIM documentId。'
        + (writes ? ' 執行前顯示完整參數確認；operationId 為新的 32 碼十六進位。逾時先用 revit_upstream_operation_status 查詢同一 ID。' : '')
        + (entry.externalEffects ? ' 此操作可能寫檔、切換文件或影響其他文件，不能用目前模型的 Undo 完整回復。' : ''),
      inputSchema: { ...shape(tool.inputSchema), documentId: z.string().min(1), ...(writes ? { operationId: z.string().regex(/^[a-f0-9]{32}$/) } : {}) },
      annotations: { ...tool.annotations, readOnlyHint: !writes, openWorldHint: entry.externalEffects }
    }, async args => {
      const { documentId, operationId, ...parameters } = args;
      const checked = validate(parameters);
      if (!checked.valid) return { content: [{ type: 'text', text: checked.errorMessage }], isError: true };
      try {
        const data: any = await bridge.call('upstream.study.' + entry.command, { ...checked.data, documentId, operationId }, slow.has(entry.command) ? 120000 : 60000);
        const body = data?.result ?? data;
        if (typeof body?.ImageBase64 === 'string') {
          const { ImageBase64, ...metadata } = body;
          const summary = data?.status ? { ...data, result: metadata } : metadata;
          return { content: [{ type: 'text', text: JSON.stringify(summary) }, { type: 'image', data: ImageBase64, mimeType: body.MimeType || 'image/png' }], isError: failed(data) };
        }
        return { content: [{ type: 'text', text: JSON.stringify(data) }], isError: failed(data) };
      } catch (error) { return { content: [{ type: 'text', text: JSON.stringify({ success: false, error: error instanceof Error ? error.message : String(error) }) }], isError: true }; }
    });
  }
}
