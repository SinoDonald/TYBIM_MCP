import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { createServer } from 'node:net';
import { once } from 'node:events';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

test('real stdio MCP handshake, tool discovery, validation and bridge roundtrip', async t => {
  const requests = [];
  const bridge = createServer(socket => {
    let data = '';
    socket.on('data', chunk => {
      data += chunk.toString();
      if (!data.includes('\n')) return;
      const request = JSON.parse(data.trim());
      requests.push(request);
      socket.end(JSON.stringify({ version: 1, id: request.id, success: true, data: { revitVersion: 'MOCK', hasActiveDocument: true } }) + '\n');
    });
  });
  bridge.listen(0, '127.0.0.1');
  await once(bridge, 'listening');
  const directory = await mkdtemp(join(tmpdir(), 'tybim-mcp-'));
  const config = join(directory, 'bridge.config.json');
  await writeFile(config, JSON.stringify({ port: bridge.address().port, token: 'x'.repeat(32) }));
  const transport = new StdioClientTransport({ command: process.execPath, args: [resolve('build/index.js')], env: { ...process.env, TYBIM_MCP_CONFIG: config } });
  const client = new Client({ name: 'tybim-test', version: '1.0' });
  t.after(async () => { await client.close(); await new Promise(resolve => bridge.close(resolve)); await rm(directory, { recursive: true, force: true }); });
  await client.connect(transport);
  const { tools } = await client.listTools();
  assert.equal(tools.length, 258);
  assert.equal(tools.filter(tool => tool.name.startsWith('revit_study_')).length, 220);
  const execute = tools.find(tool => tool.name === 'revit_execute_doors_at_wall_centers');
  assert.equal(execute.annotations.readOnlyHint, false);
  assert.equal(execute.annotations.destructiveHint, true);
  const result = await client.callTool({ name: 'revit_health', arguments: {} });
  assert.equal(JSON.parse(result.content[0].text).revitVersion, 'MOCK');
  assert.equal(requests[0].method, 'revit.health');
  const invalid = await client.callTool({ name: 'revit_get_selection', arguments: { limit: 1001 } });
  assert.equal(invalid.isError, true);
  assert.equal(requests.length, 1);
  const hello = tools.find(tool => tool.name === 'revit_hello_world');
  assert.equal(hello.annotations.idempotentHint, false);
  const dialogResult = await client.callTool({ name: 'revit_hello_world', arguments: {} });
  assert.ok(!dialogResult.isError);
  assert.equal(requests[1].method, 'revit.hello_world');
  const missingDocument = await client.callTool({ name: 'revit_execute_doors_at_wall_centers', arguments: { planId: 'a'.repeat(32) } });
  assert.equal(missingDocument.isError, true);
  const duplicateWalls = await client.callTool({ name: 'revit_preview_doors_at_wall_centers', arguments: { documentId: 'document', doorTypeUniqueId: 'type', wallUniqueIds: ['wall', 'wall'] } });
  assert.equal(duplicateWalls.isError, true);
  assert.equal(requests.length, 2);
  await client.callTool({ name: 'revit_execute_doors_at_wall_centers', arguments: { documentId: 'document', planId: 'a'.repeat(32) } });
  assert.equal(requests[2].method, 'revit.doors.execute_centers');
  assert.ok(requests[2].expiresAt > Date.now() + 50000);
  for (const fraction of [0, 1, -0.1, 1.1]) {
    const invalidPosition = await client.callTool({ name: 'revit_preview_doors_along_walls', arguments: { documentId: 'document', doorTypeUniqueId: 'type', wallUniqueIds: ['wall'], positionFraction: fraction } });
    assert.equal(invalidPosition.isError, true);
  }
  assert.equal(requests.length, 3);
  await client.callTool({ name: 'revit_preview_doors_along_walls', arguments: { documentId: 'document', doorTypeUniqueId: 'type', wallUniqueIds: ['wall'], positionFraction: 2/3 } });
  assert.equal(requests[3].method, 'revit.doors.preview_along');
  assert.equal(requests[3].parameters.positionFraction, 2/3);
  assert.ok(requests[3].expiresAt > Date.now() + 50000, 'trial preview gets time for Revit commit checks and group rollback');
  await client.callTool({ name: 'revit_preview_doors_at_wall_centers', arguments: { documentId: 'document', doorTypeUniqueId: 'type', wallUniqueIds: ['wall'] } });
  assert.equal(requests[4].method, 'revit.doors.preview_centers');
  assert.ok(requests[4].expiresAt > Date.now() + 50000);
});
