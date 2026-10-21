import { access } from 'node:fs/promises';
import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import type { RevitBridge } from './bridge.js';
export async function loadPrivateTools(server: McpServer, bridge: RevitBridge): Promise<void> {
  if (process.env.TYBIM_INCLUDE_PRIVATE === 'false') return;
  const entry = new URL('./private/register.js', import.meta.url);
  try { await access(entry); } catch { return; }
  const module = await import(entry.href);
  if (typeof module.registerPrivateTools !== 'function') throw new Error('Private module must export registerPrivateTools');
  await module.registerPrivateTools(server, bridge);
}
