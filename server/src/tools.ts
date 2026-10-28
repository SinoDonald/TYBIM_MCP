import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { RevitBridge } from "./bridge.js";
import { registerUpstreamTools } from "./integrations/mcp-servers-for-revit/register.js";
import { registerStudyTools } from "./integrations/revit-mcp-study/register.js";

export function createServer(bridge: RevitBridge): McpServer {
  const server = new McpServer({ name: "tybim-revit-mcp", version: "0.7.1" });
  const documentId = z.string().min(1).optional().describe("Document ID returned by revit_project_info; rejects a changed active document");
  const limit = z.number().int().min(1).max(1000).default(100);
  const requiredDocumentId = z.string().min(1).describe("Exact session document ID returned by project_info or door placement context");
  const planId = z.string().regex(/^[a-f0-9]{32}$/).describe("Preview plan ID; also identifies this operation for status and deduplication");
  const wallUniqueIds = z.array(z.string().min(1)).min(1).max(100).refine(ids => new Set(ids).size === ids.length, "Wall IDs must be distinct");
  const definitions: Array<{ name: string; method: string; description: string; schema: z.ZodRawShape; writes?: boolean; timeoutMs?: number }> = [
    { name: "revit_preview_doors_along_walls", method: "revit.doors.preview_along", description: "Preview one door per wall at positionFraction of wall curve length from Revit endpoint 0 toward endpoint 1 (e.g. 2/3). Door bottom is at wall base. Trial-commits doors to check joined-wall conflicts, then rolls back the entire trial group. Skipped targets include diagnostic element IDs; no retained model changes. Returns a 10-minute plan.", schema: { documentId: requiredDocumentId, doorTypeUniqueId: z.string().min(1), wallUniqueIds, positionFraction: z.number().gt(0).lt(1) }, timeoutMs: 60000 },
    { name: "revit_execute_doors_along_walls", method: "revit.doors.execute_along", description: "Execute a reviewed door placement plan at its stored wall length fraction in one undoable transaction. Same plan ID is deduplicated. On timeout, query operation status before taking further action.", schema: { documentId: requiredDocumentId, planId }, writes: true, timeoutMs: 60000 },
    { name: "revit_get_door_placement_context", method: "revit.doors.context", description: "Read walls and loaded door types. scope all is current document only, excluding links; paginated walls. No model changes.", schema: { documentId, scope: z.enum(["all", "view", "selection"]).default("all"), offset: z.number().int().min(0).max(2147483647).default(0), limit } },
    { name: "revit_preview_doors_at_wall_centers", method: "revit.doors.preview_centers", description: "Preview one wall-hosted door at each wall curve midpoint at its base elevation. Explicit wall UniqueIds (1..100) and door type required. Skips unsupported walls and overlapping inserts. Trial-commits doors to check joined-wall conflicts, then rolls back the entire trial group. Skipped targets include diagnostic element IDs; no retained model changes. Returns a 10-minute plan.", schema: { documentId: requiredDocumentId, doorTypeUniqueId: z.string().min(1), wallUniqueIds: z.array(z.string().min(1)).min(1).max(100).refine(ids => new Set(ids).size === ids.length, "Wall IDs must be distinct") }, timeoutMs: 60000 },
    { name: "revit_execute_doors_at_wall_centers", method: "revit.doors.execute_centers", description: "Execute a reviewed preview plan in one Revit transaction. Changes the model; whole batch rolls back on failures/warnings. Same plan ID never creates twice in this Revit session. If timed out, query operation status rather than create a new plan or retry blindly.", schema: { documentId: requiredDocumentId, planId }, writes: true, timeoutMs: 60000 },
    { name: "revit_get_door_operation_status", method: "revit.doors.operation_status", description: "Read the result of a door placement plan, including after a timeout. Plans and results are scoped to the current Revit session.", schema: { documentId: requiredDocumentId, planId } },
    { name: "revit_hello_world", method: "revit.hello_world", description: "Show a modal Revit dialog containing Hello World！. The user must click OK to dismiss it. Does not modify the model. A timeout does not dismiss the dialog; do not retry automatically.", schema: {} },
    { name: "revit_health", method: "revit.health", description: "Check Revit bridge availability and version", schema: {} },
    { name: "revit_project_info", method: "revit.project_info", description: "Read active document and view information", schema: { documentId } },
    { name: "revit_get_selection", method: "revit.selection", description: "Read selected elements (bounded output)", schema: { documentId, limit } },
    { name: "revit_get_levels", method: "revit.levels", description: "Read levels with elevations in millimeters", schema: { documentId, limit, offset: z.number().int().min(0).max(2147483647).default(0) } }
  ];
  for (const tool of definitions) {
    server.registerTool(tool.name, {
      description: tool.description, inputSchema: tool.schema,
      annotations: { readOnlyHint: !tool.writes, destructiveHint: !!tool.writes, idempotentHint: tool.method !== "revit.hello_world" && !tool.method.startsWith("revit.doors.preview_"), openWorldHint: false }
    }, async (args) => {
      try {
        const data = await bridge.call(tool.method, args, tool.timeoutMs);
        return { content: [{ type: "text" as const, text: JSON.stringify(data) }] };
      } catch (error) {
        return { isError: true, content: [{ type: "text" as const, text: error instanceof Error ? error.message : String(error) }] };
      }
    });
  }
  registerUpstreamTools(server, bridge);
  registerStudyTools(server, bridge);
  return server;
}
