import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:net';
import { once } from 'node:events';
import { RevitBridge } from '../build/bridge.js';

async function fixture(t, handle) {
  const sockets = new Set();
  const server = createServer(socket => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
    let buffer = '';
    socket.on('data', chunk => {
      buffer += chunk.toString();
      if (buffer.includes('\n')) handle(socket, JSON.parse(buffer.trim()));
    });
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  t.after(async () => { for (const socket of sockets) socket.destroy(); await new Promise(resolve => server.close(resolve)); });
  return new RevitBridge({ port: server.address().port, token: 'x'.repeat(32) }, 300);
}
test('request auth, deadline, correlation and fragmented UTF-8 response', async t => {
  const bridge = await fixture(t, (socket, request) => {
    assert.equal(request.version, 1);
    assert.equal(request.token, 'x'.repeat(32));
    assert.equal(request.method, 'revit.health');
    assert.ok(request.expiresAt > Date.now());
    const frame = Buffer.from(JSON.stringify({ version: 1, id: request.id, success: true, data: '樓層資訊' }) + '\n');
    // Split inside multibyte UTF-8 text.
    const split = frame.indexOf(Buffer.from('樓')) + 1;
    socket.write(frame.subarray(0, split));
    setTimeout(() => socket.end(frame.subarray(split)), 5);
  });
  assert.equal(await bridge.call('revit.health'), '樓層資訊');
});
test('reject mismatched response ID', async t => {
  const bridge = await fixture(t, socket => socket.end(JSON.stringify({ version: 1, id: 'wrong', success: true }) + '\n'));
  await assert.rejects(bridge.call('revit.health'), /INVALID_RESPONSE/);
});
test('surface Revit errors', async t => {
  const bridge = await fixture(t, (socket, r) => socket.end(JSON.stringify({ version: 1, id: r.id, success: false, error: 'NO_ACTIVE_DOCUMENT' }) + '\n'));
  await assert.rejects(bridge.call('revit.project_info'), /NO_ACTIVE_DOCUMENT/);
});
test('reject disconnected requests immediately', async t => {
  const bridge = await fixture(t, socket => socket.end());
  await assert.rejects(bridge.call('revit.health'), /REVIT_DISCONNECTED/);
});
test('timeout does not retry the command', async t => {
  let requests = 0;
  const bridge = await fixture(t, () => requests++);
  await assert.rejects(bridge.call('revit.health'), /REVIT_TIMEOUT/);
  assert.equal(requests, 1);
});
test('parallel calls keep responses independent', async t => {
  const bridge = await fixture(t, (socket, r) => socket.end(JSON.stringify({ version: 1, id: r.id, success: true, data: r.parameters.index }) + '\n'));
  assert.deepEqual(await Promise.all(Array.from({ length: 8 }, (_, index) => bridge.call('revit.health', { index }))), [0,1,2,3,4,5,6,7]);
});
