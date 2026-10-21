import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm, readFile, readdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { createServer } from '../build/tools.js';
import { closeDatabase } from '../build/integrations/mcp-servers-for-revit/database/db.js';

const documentId = 'test-doc', operationId = 'a'.repeat(32);
const decode = result => JSON.parse(result.content[0].text);
test('all imported C# commands have a matching implemented TypeScript bridge caller', async () => {
  const catalog = JSON.parse((await readFile(new URL('../../third_party/mcp-servers-for-revit/catalog.json', import.meta.url), 'utf8')).replace(/^\uFEFF/, ''));
  const directory = new URL('../src/integrations/mcp-servers-for-revit/tools/', import.meta.url);
  const commands = new Set();
  for (const file of await readdir(directory)) {
    if (!file.endsWith('.ts')) continue;
    const code = await readFile(new URL(file, directory), 'utf8');
    for (const match of code.matchAll(/sendCommand\s*\(\s*["']([^"']+)["']/g)) commands.add(match[1]);
  }
  assert.equal(catalog.length, 23);
  assert.deepEqual([...commands].sort(), catalog.map(c => c.name).sort());
});
async function setup(t, handler) {
  const calls = [];
  const bridge = { call: async (method, args, timeout) => { calls.push({ method, args, timeout }); return handler ? handler(method, args) : { success: true, method }; } };
  const server = createServer(bridge);
  const client = new Client({ name: 'upstream-test', version: '1' });
  const [a, b] = InMemoryTransport.createLinkedPair();
  await server.connect(b); await client.connect(a);
  t.after(async () => { await client.close(); await server.close(); });
  return { client, calls };
}
test('all implemented upstream tools are discoverable; empty upstream files are not advertised', async t => {
  const { client } = await setup(t);
  const tools = (await client.listTools()).tools;
  const upstream = tools.filter(tool => tool.name.startsWith('revit_upstream_'));
  assert.equal(upstream.length, 27); // 26 upstream tools plus operation status
  for (const name of ['modify_element', 'search_modules', 'use_module']) assert.ok(!tools.some(tool => tool.name === 'revit_upstream_' + name));
  for (const tool of upstream) {
    assert.ok(tool.inputSchema.required.includes('documentId'));
    if (!tool.annotations.readOnlyHint) assert.ok(tool.inputSchema.required.includes('operationId'));
  }
});
test('read tools forward transformed upstream parameters through authenticated TYBIM bridge', async t => {
  const { client, calls } = await setup(t);
  const result = await client.callTool({ name: 'revit_upstream_get_available_family_types', arguments: { documentId, categoryList: ['OST_Doors'], limit: 12 } });
  assert.equal(result.isError, false);
  assert.equal(calls[0].method, 'upstream.get_available_family_types');
  assert.equal(calls[0].args.documentId, documentId);
  assert.deepEqual(calls[0].args.categoryList, ['OST_Doors']);
  assert.equal(calls[0].args.limit, 12);
  assert.equal(calls[0].timeout, 60000);
});
test('native failures cannot be reported as successful by upstream text formatting', async t => {
  const { client } = await setup(t, () => ({ status: 'rolledBack', error: 'join conflict' }));
  const result = await client.callTool({ name: 'revit_upstream_send_code_to_revit', arguments: { documentId, operationId, code: 'return 1;' } });
  assert.equal(result.isError, true);
  assert.equal(decode(result).error, 'join conflict');
});
test('bridge errors propagate even when original upstream handler catches them', async t => {
  const { client } = await setup(t, () => { throw new Error('DOCUMENT_CHANGED'); });
  const result = await client.callTool({ name: 'revit_upstream_get_current_view_info', arguments: { documentId } });
  assert.equal(result.isError, true);
  assert.match(decode(result).error, /DOCUMENT_CHANGED/);
});
test('writes reject missing operation/document IDs before reaching Revit', async t => {
  const { client, calls } = await setup(t);
  for (const args of [{ elevations: [0] }, { documentId, elevations: [0] }]) {
    const result = await client.callTool({ name: 'revit_upstream_create_level', arguments: args });
    assert.equal(result.isError, true);
  }
  assert.equal(calls.length, 0);
});
test('local SQLite stores projects and rooms, deduplicates operations, and exposes status', async t => {
  const dir = await mkdtemp(join(tmpdir(), 'tybim-upstream-db-'));
  process.env.TYBIM_UPSTREAM_DB = join(dir, 'data.db');
  t.after(async () => { closeDatabase(); delete process.env.TYBIM_UPSTREAM_DB; await rm(dir, { recursive: true, force: true }); });
  const { client, calls } = await setup(t);
  const args = { documentId, operationId, project_name: '測試專案', project_number: 'P001' };
  const first = decode(await client.callTool({ name: 'revit_upstream_store_project_data', arguments: args }));
  const repeat = decode(await client.callTool({ name: 'revit_upstream_store_project_data', arguments: args }));
  assert.deepEqual(repeat, first); assert.equal(first.status, 'completed');
  const changed = await client.callTool({ name: 'revit_upstream_store_project_data', arguments: { ...args, project_number: 'changed' } });
  assert.equal(changed.isError, true);
  const rooms = await client.callTool({ name: 'revit_upstream_store_room_data', arguments: { documentId, operationId: 'b'.repeat(32), project_name: '測試專案', rooms: [{ room_id: 'R1', room_name: '會議室', area: 12 }] } });
  assert.equal(rooms.isError, false);
  const query = decode(await client.callTool({ name: 'revit_upstream_query_stored_data', arguments: { documentId, query_type: 'rooms_by_project_name', project_name: '測試專案' } }));
  assert.match(JSON.stringify(query), /會議室/);
  const status = decode(await client.callTool({ name: 'revit_upstream_operation_status', arguments: { documentId, operationId } }));
  assert.deepEqual(status, first);
  assert.equal(calls.length, 0, 'local database tools do not query Revit');
});
