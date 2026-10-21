import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import type { Tool } from "@modelcontextprotocol/sdk/types.js";
import { fileURLToPath } from "node:url";
import { randomUUID } from "node:crypto";
import { textResult, type EventSink, type ToolGateway, type ToolResult } from "./types.js";

export type Approve = (request: { id: string; name: string; arguments: Record<string, unknown>; preview: unknown }, signal: AbortSignal) => Promise<boolean>;
export class McpGateway implements ToolGateway {
  tools: Tool[] = [];
  private previews = new Map<string, any>();
  private approved = new Set<string>();
  private unknownOperations = new Set<string>();
  private inFlight = new Set<Promise<ToolResult>>();
  private tail: Promise<unknown> = Promise.resolve();
  constructor(private readonly emit: EventSink, private readonly approve: Approve,
    private readonly client: Pick<Client, "connect" | "listTools" | "callTool" | "close"> = new Client({ name: "tybim-revit-chat", version: "0.5.0" })) { }
  async connect(bridgeConfig: string): Promise<void> {
    await this.client.connect(new StdioClientTransport({ command: process.execPath, args: [fileURLToPath(new URL("../index.js", import.meta.url))], env: { ...process.env as Record<string, string>, TYBIM_MCP_CONFIG: bridgeConfig }, stderr: "pipe" }));
    this.tools = (await this.client.listTools()).tools;
  }
  call(name: string, args: Record<string, unknown>, signal: AbortSignal): Promise<ToolResult> {
    // Serialize across providers/tool batches. Cancelled turns cannot leave concurrent writes.
    const work = this.tail.then(() => this.perform(name, args, signal));
    this.tail = work.catch(() => {});
    this.inFlight.add(work);
    void work.finally(() => this.inFlight.delete(work)).catch(() => {});
    return work;
  }
  private async perform(name: string, args: Record<string, unknown>, signal: AbortSignal): Promise<ToolResult> {
    signal.throwIfAborted();
    const definition = this.tools.find(t => t.name === name);
    if (!definition) return textResult("UNKNOWN_TOOL", true);
    const operationKey = String(args.documentId) + ":" + String(args.planId ?? args.operationId);
    const upstream = name.startsWith("revit_upstream_") || name.startsWith("revit_study_") || name.startsWith("revit_private_");
    const writes = definition.annotations?.readOnlyHint !== true;
    if (this.unknownOperations.size && (writes || name.startsWith("revit_preview_"))) return textResult("前次寫入結果尚未確認：放門使用原 planId 查詢 revit_get_door_operation_status；整合工具使用原 operationId 查詢 revit_upstream_operation_status。不可重試或建立新計畫。", true);
    if (writes && upstream) {
      if (typeof args.documentId !== 'string' || !/^[a-f0-9]{32}$/.test(String(args.operationId))) return textResult('documentId 與 32 碼 operationId 必填。', true);
      let info: any;
      if (args.documentId === 'application' && name === 'revit_study_open_document') info = { documentId: 'application', title: 'Revit（尚未開啟模型）' };
      else {
        const project = await this.client.callTool({ name: 'revit_project_info', arguments: { documentId: args.documentId } }, undefined, { timeout: 65000 });
        if (project.isError) return textResult('目前專案已變更或無法讀取，請重新讀取專案。', true);
        info = JSON.parse((project.content as any[]).find(c => c.type === 'text')?.text ?? '{}');
      }
      if (info.documentId !== args.documentId) return textResult('目前專案不符，請重新讀取專案。', true);
      const preview = { kind: 'upstream', title: info.title, documentId: info.documentId, tool: name, arguments: args,
        note: '以下是本次執行參數，尚未試建幾何。請核對元素 ID、尺寸、範圍或完整程式碼後確認。' };
      if (!await this.approve({ id: randomUUID(), name, arguments: args, preview }, signal)) return textResult('使用者已取消此次操作，停止後續寫入。', true);
    } else if (writes) {
      const preview = this.previews.get(operationKey);
      if (!preview || preview.documentId !== args.documentId) return textResult("此對話尚無對應文件的放置預覽，請先呼叫 preview。", true);
      if (!this.approved.has(operationKey)) {
        const accepted = await this.approve({ id: randomUUID(), name, arguments: args, preview }, signal);
        if (!accepted) return textResult("使用者已取消此次模型修改；停止此操作，不得改用其他計畫重送。", true);
        this.approved.add(operationKey);
      }
    }
    signal.throwIfAborted();
    this.emit({ event: "toolStarted", name, arguments: args });
    // Once dispatched, wait for the real result even when the AI request is cancelled.
    // An AbortSignal here would abandon a possibly committed Revit transaction.
    let result: ToolResult;
    try {
      const response = await this.client.callTool({ name, arguments: args }, undefined, { timeout: name.startsWith('revit_study_') ? 185000 : 65000 });
      result = { isError: !!response.isError, content: (response.content as any[]).filter(c => c.type === "text" || c.type === "image").map(c => c.type === 'text' ? { type: "text", text: c.text } : { type: 'image', data: c.data, mimeType: c.mimeType }) };
    } catch (error) { result = textResult(error instanceof Error ? error.message : String(error), true); }
    let data: any;
    try { data = JSON.parse(result.content.find(c => c.type === 'text')?.text ?? ""); } catch { /* plain tool error */ }
    if (name.startsWith("revit_preview_") && !result.isError && data?.planId && data?.documentId) {
      if (this.previews.size >= 1000) this.previews.delete(this.previews.keys().next().value!);
      this.previews.set(data.documentId + ":" + data.planId, data);
    }
    if (writes && !["completed", "rolledBack", "failed"].includes(data?.status)) this.unknownOperations.add(operationKey);
    if ((name === "revit_get_door_operation_status" || name === "revit_upstream_operation_status") && ["completed", "rolledBack", "failed", "preview"].includes(data?.status)) this.unknownOperations.delete(operationKey);
    this.emit({ event: "toolCompleted", name, result, data });
    return result;
  }
  async idle(): Promise<void> { await Promise.allSettled([...this.inFlight]); }
  async close(): Promise<void> { await this.idle(); await this.client.close(); }
}




