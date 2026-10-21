export async function checkResponse(response: Response): Promise<Response> {
  if (response.ok) return response;
  // Keep headers, API keys and large response bodies out of UI/logs.
  let message = response.statusText;
  try { const body = await response.json() as { error?: { message?: string } }; message = body.error?.message ?? message; } catch { /* no JSON */ }
  throw new Error(`AI HTTP ${response.status}: ${message.slice(0, 1000)}`);
}

export async function* sseJson(response: Response): AsyncGenerator<any> {
  await checkResponse(response);
  if (!response.body) throw new Error("Empty AI response");
  const decoder = new TextDecoder();
  let buffer = "";
  let data: string[] = [];
  const processLine = (line: string): any | undefined => {
    if (line === "") {
      const payload = data.join("\n"); data = [];
      if (!payload || payload === "[DONE]") return undefined;
      return JSON.parse(payload);
    }
    if (line.startsWith("data:")) data.push(line.slice(5).trimStart());
    return undefined;
  };
  for await (const chunk of response.body) {
    buffer += decoder.decode(chunk, { stream: true });
    if (buffer.length > 8 * 1024 * 1024) throw new Error("AI stream frame too large");
    let end: number;
    while ((end = buffer.indexOf("\n")) >= 0) {
      const value = processLine(buffer.slice(0, end).replace(/\r$/, ""));
      buffer = buffer.slice(end + 1);
      if (value !== undefined) yield value;
    }
  }
  buffer += decoder.decode();
  if (buffer) processLine(buffer.replace(/\r$/, ""));
  const final = processLine("");
  if (final !== undefined) yield final;
}

export function requestSignal(signal?: AbortSignal): AbortSignal {
  return signal ? AbortSignal.any([signal, AbortSignal.timeout(180000)]) : AbortSignal.timeout(30000);
}
