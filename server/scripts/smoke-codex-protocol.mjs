import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { RpcProcess } from '../build/chat/rpc-process.js';
const executable = process.argv[2];
if (!executable) throw new Error('Pass absolute codex.exe path');
const state = await mkdtemp(join(tmpdir(), 'tybim-codex-protocol-'));
const env = { ...process.env, CODEX_HOME: state };
for (const key of ['OPENAI_API_KEY','CODEX_API_KEY','CODEX_ACCESS_TOKEN','OPENAI_ACCESS_TOKEN']) delete env[key];
const rpc = new RpcProcess(executable, ['app-server','-c','features.shell_tool=false','-c','features.unified_exec=false','-c','features.code_mode_host=true','-c','features.daemon_auto_start=false','-c','mcp_servers={}'], env, state);
try {
  await rpc.request('initialize', { clientInfo: { name: 'tybim_protocol_smoke', title: 'TYBIM Protocol Smoke', version: '0.2.0' }, capabilities: { experimentalApi: true } });
  rpc.notify('initialized');
  const models = await rpc.request('model/list', { includeHidden: false, limit: 100 });
  const model = models.data[0]?.model;
  if (!model) throw new Error('Native model catalog is empty');
  const result = await rpc.request('thread/start', { model, sandbox: 'read-only', approvalPolicy: 'on-request',
    developerInstructions: 'Protocol validation only. No turn will be started.',
    dynamicTools: [{ type: 'function', name: 'revit_health', description: 'Check Revit connection', inputSchema: { type: 'object', properties: {} } }]
  });
  if (!result.thread?.id) throw new Error('Missing thread response');
  console.log(JSON.stringify({ nativeModelCatalog: true, threadStartWithDynamicTools: true, inferenceRequested: false }));
} finally {
  await rpc.close();
  await rm(state, { recursive: true, force: true, maxRetries: 10, retryDelay: 100 });
}
