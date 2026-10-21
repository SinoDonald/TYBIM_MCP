import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { fileURLToPath } from 'node:url';

const name = process.argv[2];
if (!name) throw new Error('Usage: node server/scripts/invoke.mjs <tool> [arguments JSON]');
const args = JSON.parse(process.argv[3] ?? '{}');
const client = new Client({ name: 'tybim-local-client', version: '1.0.0' });
try {
  await client.connect(new StdioClientTransport({ command: process.execPath,
    args: [fileURLToPath(new URL('../build/index.js', import.meta.url))],
    env: { ...process.env, TYBIM_MCP_CONFIG: process.env.TYBIM_MCP_CONFIG ?? fileURLToPath(new URL('../../artifacts/2025/Tybim.Mcp/bridge.config.json', import.meta.url)) }
  }));
  const result = await client.callTool({ name, arguments: args });
  for (const item of result.content ?? []) if (item.type === 'text') console.log(item.text);
  if (result.isError) process.exitCode = 1;
} catch (error) { console.error(error.message); process.exitCode = 1; }
finally { await client.close(); }
