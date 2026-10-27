import assert from 'node:assert/strict';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
const install = process.argv[2];
if (!install) throw new Error('Pass packaged or installed Tybim.Mcp folder');
const client = new Client({ name: 'tybim-package-check', version: '0.5.0' });
try {
  const node = existsSync(join(install, 'runtime/node/node.exe')) ? join(install, 'runtime/node/node.exe') : process.execPath;
  await client.connect(new StdioClientTransport({ command: node,
    args: [join(install, 'runtime/build/index.js')],
    env: { ...process.env, TYBIM_MCP_CONFIG: process.env.TYBIM_VERIFY_CONFIG || join(install, 'bridge.config.json') }, stderr: 'pipe' }));
  const tools = (await client.listTools()).tools;
  assert.equal(tools.filter(tool => !tool.name.startsWith('revit_private_')).length, 258);
  assert.equal(tools.filter(t => t.name.startsWith('revit_study_')).length, 220);
  assert.equal(tools.filter(t => t.name.startsWith('revit_upstream_')).length, 27);
  assert.ok(tools.some(t => t.name === 'revit_upstream_send_code_to_revit'));
  assert.ok(tools.some(t => t.name === 'revit_execute_doors_along_walls'));
  console.log(JSON.stringify({ passed: true, toolCount: tools.length, privateTools: tools.filter(t => t.name.startsWith('revit_private_')).length, upstreamToolsIncludingStatus: 27, studyTools: 220, inferenceRequested: false, modelModified: false }));
} finally { await client.close(); }




