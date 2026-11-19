// .NET Framework's redirected StandardInput can emit a UTF-8 preamble before
// the first JSONL request, even when payload bytes are written without a BOM.
export function parseChatRequest(line: string): any {
  if (line.length > 1024 * 1024) throw new Error("Chat request too large");
  return JSON.parse(line.charCodeAt(0) === 0xfeff ? line.slice(1) : line);
}
