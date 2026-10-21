import { createConnection } from "node:net";
import { randomUUID } from "node:crypto";
import { readFileSync } from "node:fs";

export interface BridgeConfig { port: number; token: string }
export function readConfig(path: string): BridgeConfig {
  const config = JSON.parse(readFileSync(path, "utf8").replace(/^\uFEFF/, "")) as BridgeConfig;
  if (!Number.isInteger(config.port) || config.port < 1024 || config.port > 65535 ||
      typeof config.token !== "string" || config.token.length < 32) throw new Error("Invalid bridge configuration");
  return config;
}
export interface BridgeResponse { version: number; id: string; success: boolean; data?: unknown; error?: string }
export class RevitBridge {
  constructor(private readonly config: BridgeConfig, private readonly timeoutMs = 15000) {
    if (!Number.isInteger(timeoutMs) || timeoutMs < 1 || timeoutMs > 180000) throw new Error("Invalid timeout");
  }
  call(method: string, parameters: Record<string, unknown> = {}, timeoutMs = this.timeoutMs): Promise<unknown> {
    if (!Number.isInteger(timeoutMs) || timeoutMs < 1 || timeoutMs > 180000) return Promise.reject(new Error("Invalid timeout"));
    return new Promise((resolve, reject) => {
      const id = randomUUID();
      const expiresAt = Date.now() + timeoutMs;
      const socket = createConnection({ host: "127.0.0.1", port: this.config.port });
      const chunks: Buffer[] = [];
      let size = 0;
      let settled = false;
      const finish = (error?: Error, data?: unknown) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        socket.destroy();
        if (error) reject(error); else resolve(data);
      };
      const timer = setTimeout(() => finish(new Error("REVIT_TIMEOUT: Revit may be busy or awaiting a dialog")), timeoutMs);
      socket.on("error", error => finish(error));
      socket.on("close", () => { if (!settled) finish(new Error("REVIT_DISCONNECTED")); });
      socket.on("connect", () => socket.write(JSON.stringify({ version: 1, id, token: this.config.token,
        method, parameters, expiresAt }) + "\n"));
      socket.on("data", (chunk: Buffer) => {
        const end = chunk.indexOf(10);
        const part = end < 0 ? chunk : chunk.subarray(0, end);
        size += part.length;
        if (size > 32 * 1024 * 1024) { finish(new Error("FRAME_TOO_LARGE")); return; }
        chunks.push(part);
        if (end < 0) return;
        try {
          const response = JSON.parse(Buffer.concat(chunks).toString("utf8")) as BridgeResponse;
          if (response.version !== 1 || response.id !== id || typeof response.success !== "boolean") throw new Error("INVALID_RESPONSE");
          if (!response.success) throw new Error(response.error ?? "REVIT_ERROR");
          finish(undefined, response.data);
        } catch (error) { finish(error instanceof Error ? error : new Error(String(error))); }
      });
    });
  }
}
