// Opt-in live integration test. Uses the panel's existing login and an open Revit
// model. Only these three read operations are exposed; never edits the model.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { CodexProvider } from '../build/chat/codex-provider.js';
import { McpGateway } from '../build/chat/gateway.js';

const install = process.argv[2];
if (!install) throw new Error('Pass installed Tybim.Mcp folder');
const config = JSON.parse((await readFile(join(install, 'chat.config.json'), 'utf8')).replace(/^\uFEFF/, ''));
const state = config.stateDirectory.replace(/%([^%]+)%/g, (_, name) => {
  if (!process.env[name]) throw new Error(`Missing environment variable ${name}`);
  return process.env[name];
});
const required = ['revit_health', 'revit_project_info', 'revit_get_levels'];
const completed = new Map();
let answer = '';
const emit = event => {
  if (event.event === 'toolCompleted') {
    assert.equal(event.result.isError, false, `${event.name}: ${JSON.stringify(event.result)}`);
    completed.set(event.name, event.data);
    console.log(JSON.stringify({ tool: event.name, data: event.data }));
  }
  if (event.event === 'textDelta') answer += event.text;
};
const gateway = new McpGateway(emit, async () => false);
const provider = new CodexProvider({ provider: 'codex', model: process.argv[3] || 'gpt-6.1-sol', codexExecutable: config.codexExecutable }, gateway, emit, state);
const controller = new AbortController();
const timer = setTimeout(() => controller.abort(), 180000);
try {
  await gateway.connect(join(install, 'bridge.config.json'));
  gateway.tools = gateway.tools.filter(tool => required.includes(tool.name));
  assert.equal(gateway.tools.length, required.length);
  await provider.start();
  console.log(await provider.status());
  assert.equal(provider.authenticated, true, 'Login through the Revit panel first');
  await provider.send('請實際依序呼叫 revit_health、revit_project_info，以及 revit_get_levels，確認 Revit 連線並列出目前模型與所有樓層。不要只說明操作方法。這是唯讀測試，不修改模型。', controller.signal);
  for (const tool of required) assert.ok(completed.has(tool), `No successful real tool call: ${tool}. Assistant: ${answer}`);
  console.log(JSON.stringify({ passed: true, tools: [...completed.keys()], answer }));
} finally {
  clearTimeout(timer);
  await provider.close();
  await gateway.close();
}
