import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

// Real local MCP + native app-server handshake only. No model turn, sign-in, or paid inference.
const codex = process.argv[2];
if (!codex) throw new Error('Pass the absolute codex.exe path');
const state = await mkdtemp(join(tmpdir(), 'tybim-native-smoke-'));
const config = join(state, 'bridge.json');
await writeFile(config, JSON.stringify({ port: 17825, token: 'test-only-token-'.repeat(3) }));
const entry = process.argv[3] ?? resolve('server/build/chat/index.js');
const child = spawn(process.execPath, [entry], { windowsHide: true, stdio: ['pipe','pipe','pipe'], env: { ...process.env, TYBIM_MCP_CONFIG: config, TYBIM_CHAT_STATE: state } });
const reader = createInterface({ input: child.stdout });
let timer;
try {
  const response = await new Promise((resolve, reject) => {
    timer = setTimeout(() => reject(new Error('Native smoke timed out')), 60000);
    child.on('error', reject);
    child.on('exit', code => { if (code) reject(new Error('Runtime exited: ' + code)); });
    child.stderr.on('data', chunk => process.stderr.write(chunk));
    reader.on('line', line => {
      const event = JSON.parse(line);
      if (event.event === 'ready') child.stdin.write(JSON.stringify({ id: 1, method: 'configure', params: { provider: 'codex', codexExecutable: codex } }) + '\n');
      if (event.id === 1) event.error ? reject(new Error(event.error)) : resolve(event.result);
      if (event.event === 'fatal') reject(new Error(event.text));
    });
  });
  console.log(JSON.stringify({ status: response.status, modelCount: response.models.length, inferenceRequested: false }));
} finally {
  clearTimeout(timer);
  child.stdin.write('{"method":"shutdown"}\n');
  child.stdin.end();
  await Promise.race([new Promise(resolve => child.on('exit', resolve)), new Promise(resolve => setTimeout(resolve, 4000))]);
  child.kill(); reader.close();
  // Windows releases SQLite/WAL handles asynchronously after process exit.
  await rm(state, { recursive: true, force: true, maxRetries: 10, retryDelay: 100 });
}
