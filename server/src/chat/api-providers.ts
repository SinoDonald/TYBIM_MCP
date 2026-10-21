import { checkResponse, requestSignal, sseJson } from "./http.js";
import { instructions, type ChatProvider, type EventSink, type ModelOption, type ProviderSettings, type ToolGateway } from "./types.js";

export class ApiProvider implements ChatProvider {
  private history: any[] = [];
  constructor(private readonly settings: ProviderSettings, private readonly gateway: ToolGateway,
    private readonly emit: EventSink, private readonly fetcher: typeof fetch = fetch) { }
  private key(): string {
    if (!this.settings.apiKey?.trim()) throw new Error("請在面板輸入此供應商的 API Key，再按連線。");
    return this.settings.apiKey.trim();
  }
  private headers(): Record<string, string> {
    return this.settings.provider === "claude"
      ? { "content-type": "application/json", "x-api-key": this.key(), "anthropic-version": "2023-06-01" }
      : { "content-type": "application/json", "x-goog-api-key": this.key() };
  }
  async status(): Promise<string> { return this.settings.apiKey ? "API Key 已設定；模型清單連線成功後即可選擇模型。" : "需要 API Key"; }
  async login(): Promise<void> { this.emit({ event: "status", text: "此連線使用 API Key，請至供應商開發者平台取得金鑰；訂閱帳號登入不會自動提供 API 額度。" }); }
  async logout(): Promise<void> { this.settings.apiKey = undefined; this.history = []; }
  async models(): Promise<ModelOption[]> {
    const url = this.settings.provider === "claude" ? "https://api.anthropic.com/v1/models?limit=100" : "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000";
    const response = await checkResponse(await this.fetcher(url, { headers: this.headers(), signal: requestSignal() }));
    const data = await response.json() as any;
    if (this.settings.provider === "claude") return (data.data ?? []).map((m: any) => ({ id: m.id, name: m.display_name ?? m.id }));
    return (data.models ?? []).filter((m: any) => m.supportedGenerationMethods?.includes("generateContent"))
      .map((m: any) => ({ id: m.name.replace(/^models\//, ""), name: m.displayName ?? m.name }));
  }
  async send(text: string, signal: AbortSignal): Promise<void> {
    if (!this.settings.model) throw new Error("請先選擇模型。");
    this.key();
    try {
      if (this.settings.provider === "claude") await this.claude(text, signal);
      else await this.gemini(text, signal);
    } catch (error) {
      // A cancelled/failed tool round may contain incomplete tool-use pairs.
      // Never resend such history (and never replay writes) on the next prompt.
      this.history = [];
      this.emit({ event: "status", text: "此回合中止；下一則訊息會開始新對話。已完成的工具結果仍保留在面板。" });
      throw error;
    }
  }
  private async claude(text: string, signal: AbortSignal): Promise<void> {
    this.history.push({ role: "user", content: text });
    for (let round = 0; round < 30; round++) {
      signal.throwIfAborted();
      const response = await this.fetcher("https://api.anthropic.com/v1/messages", { method: "POST", headers: this.headers(), signal: requestSignal(signal), body: JSON.stringify({
        model: this.settings.model, max_tokens: 4096, stream: true, system: instructions, messages: this.history,
        tools: this.gateway.tools.map(t => ({ name: t.name, description: t.description ?? t.name, input_schema: t.inputSchema }))
      }) });
      const blocks: any[] = [];
      const json = new Map<number, string>();
      let stopped = false;
      let stopReason = "";
      for await (const event of sseJson(response)) {
        if (event.type === "error") throw new Error(event.error?.message ?? "Claude stream error");
        if (event.type === "content_block_start") blocks[event.index] = structuredClone(event.content_block);
        if (event.type === "content_block_delta") {
          const block = blocks[event.index];
          if (!block) throw new Error("Claude content block missing");
          if (event.delta.type === "text_delta") { block.text = (block.text ?? "") + event.delta.text; this.emit({ event: "textDelta", text: event.delta.text }); }
          if (event.delta.type === "input_json_delta") json.set(event.index, (json.get(event.index) ?? "") + event.delta.partial_json);
          if (event.delta.type === "thinking_delta") block.thinking = (block.thinking ?? "") + event.delta.thinking;
          if (event.delta.type === "signature_delta") block.signature = (block.signature ?? "") + event.delta.signature;
        }
        if (event.type === "message_delta") stopReason = event.delta?.stop_reason ?? stopReason;
        if (event.type === "message_stop") stopped = true;
      }
      if (!stopped) throw new Error("Claude 回應串流提早結束，尚未執行該回應的工具。");
      for (const [index, value] of json) blocks[index].input = JSON.parse(value);
      if (stopReason === "max_tokens") throw new Error("Claude 回應超過長度限制，請縮小工作範圍。");
      this.history.push({ role: "assistant", content: blocks });
      const calls = blocks.filter(b => b.type === "tool_use");
      if (!calls.length) return;
      const results: any[] = [];
      for (const call of calls) {
        signal.throwIfAborted();
        const result = await this.gateway.call(call.name, call.input, signal);
        results.push({ type: "tool_result", tool_use_id: call.id, content: result.content.map(c => c.type === 'text' ? c : ({ type: 'image', source: { type: 'base64', media_type: c.mimeType, data: c.data } })), is_error: !!result.isError });
      }
      this.history.push({ role: "user", content: results });
    }
    throw new Error("工具回合超過 30 次，請拆分工作。");
  }
  private async gemini(text: string, signal: AbortSignal): Promise<void> {
    this.history.push({ role: "user", parts: [{ text }] });
    for (let round = 0; round < 30; round++) {
      signal.throwIfAborted();
      const model = encodeURIComponent(this.settings.model!);
      const response = await this.fetcher(`https://generativelanguage.googleapis.com/v1beta/models/${model}:streamGenerateContent?alt=sse`, {
        method: "POST", headers: this.headers(), signal: requestSignal(signal), body: JSON.stringify({
          systemInstruction: { parts: [{ text: instructions }] }, contents: this.history,
          tools: [{ functionDeclarations: this.gateway.tools.map(t => ({ name: t.name, description: t.description ?? t.name, parametersJsonSchema: t.inputSchema })) }],
          generationConfig: { maxOutputTokens: 8192 }
        })
      });
      const parts: any[] = [];
      let finishReason = "";
      for await (const event of sseJson(response)) {
        if (event.error) throw new Error(event.error.message ?? "Gemini stream error");
        if (event.promptFeedback?.blockReason) throw new Error("Gemini blocked the request: " + event.promptFeedback.blockReason);
        const candidate = event.candidates?.[0];
        if (!candidate) continue;
        finishReason = candidate.finishReason ?? finishReason;
        for (const part of candidate.content?.parts ?? []) {
          // Keep complete parts and thought signatures intact for subsequent tool turns.
          parts.push(structuredClone(part));
          if (part.text && !part.thought) this.emit({ event: "textDelta", text: part.text });
        }
      }
      if (finishReason !== "STOP") throw new Error("Gemini 回應未完整完成：" + (finishReason || "串流中斷"));
      this.history.push({ role: "model", parts });
      const calls = parts.filter(p => p.functionCall).map(p => p.functionCall);
      if (!calls.length) return;
      const results: any[] = [];
      for (const call of calls) {
        signal.throwIfAborted();
        const result = await this.gateway.call(call.name, call.args ?? {}, signal);
        const images = result.content.filter(c => c.type === 'image');
        results.push({ functionResponse: { ...(call.id ? { id: call.id } : {}), name: call.name,
          response: { result: { ...result, content: result.content.filter(c => c.type === 'text') } },
          ...(images.length && this.settings.model?.startsWith('gemini-3') ? { parts: images.map(c => ({ inlineData: { mimeType: c.mimeType, data: c.data } })) } : {}) } });
        if (images.length && !this.settings.model?.startsWith('gemini-3')) results.push(...images.map(c => ({ inlineData: { mimeType: c.mimeType, data: c.data } })));
      }
      this.history.push({ role: "user", parts: results });
    }
    throw new Error("工具回合超過 30 次，請拆分工作。");
  }
  async close(): Promise<void> { this.history = []; }
}
