import { readFileSync } from 'node:fs';
import { connect } from 'node:net';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

const path = process.argv[2];
if (!path) throw new Error('Pass the generated external-ai.mcp.json path.');
const entry = JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, '')).mcpServers.tybim_revit_2025_external;
const bridge = JSON.parse(readFileSync(entry.env.TYBIM_MCP_CONFIG, 'utf8').replace(/^\uFEFF/, ''));
if (bridge.port !== 21825) throw new Error('Expected external port 21825.');
const listening = port => new Promise(resolve => {
  const socket = connect({ host: '127.0.0.1', port });
  let done = false;
  const finish = value => { if (!done) { done = true; socket.destroy(); resolve(value); } };
  socket.setTimeout(1500, () => finish(false));
  socket.once('connect', () => finish(true));
  socket.once('error', () => finish(false));
});
const internalListening = await listening(17825);
console.log(JSON.stringify({ internalPort: 17825, internalListening, externalPort: bridge.port }));
if (internalListening) throw new Error('Close AI助手 before running this independence check.');
const client = new Client({ name: 'tybim-external-read-check', version: '1.0.0' });
const transport = new StdioClientTransport({ command: entry.command, args: entry.args, env: { ...process.env, ...entry.env }, stderr: 'pipe' });
try {
  await client.connect(transport);
  async function call(name, args = {}) {
    const result = await client.callTool({ name, arguments: args });
    if (result.isError) throw new Error(`${name}: ${JSON.stringify(result.content)}`);
    const raw = result.content.filter(c => c.type === 'text').map(c => c.text).join('\n');
    const data = JSON.parse(raw);
    console.log(JSON.stringify({ tool: name, data }));
    return data;
  }
  await call('revit_health');
  const project = await call('revit_project_info');
  await call('revit_get_levels', { documentId: project.documentId, limit: 100, offset: 0 });
} finally { await client.close(); }
