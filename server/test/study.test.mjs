import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { createServer } from '../build/tools.js';
import { McpGateway } from '../build/chat/gateway.js';
const documentId = 'study-doc', operationId = 'a'.repeat(32);
async function setup(t, answer = () => ({ success: true })) {
  const calls = [], server = createServer({ call: async (method, args, timeout) => { calls.push({ method, args, timeout }); return answer(method, args); } });
  const client = new Client({ name: 'study-test', version: '1' });
  const [a, b] = InMemoryTransport.createLinkedPair();
  await server.connect(b); await client.connect(a);
  t.after(async () => { await client.close(); await server.close(); });
  return { client, calls };
}
test('Study exposes only the 220 implemented commands with document/write guards', async t => {
  const { client } = await setup(t);
  const tools = (await client.listTools()).tools, study = tools.filter(t => t.name.startsWith('revit_study_'));
  assert.equal(study.length, 220); assert.equal(tools.length, 258);
  assert.equal(tools.filter(t => t.annotations.readOnlyHint).length, 109);
  assert.ok(!study.some(t => t.name.endsWith('check_sanitary_fixture_requirements')));
  for (const tool of study) {
    assert.ok(tool.inputSchema.required.includes('documentId'));
    if (!tool.annotations.readOnlyHint) assert.ok(tool.inputSchema.required.includes('operationId'));
  }
});
test('Study aliases map to implemented native command and preserve filters', async t => {
  const { client, calls } = await setup(t);
  const result = await client.callTool({ name: 'revit_study_query_elements_with_filter', arguments: { documentId, category: 'OST_Walls', filters: [{ field: 'Name', operator: 'contains', value: '外牆' }] } });
  assert.equal(result.isError, false);
  assert.equal(calls[0].method, 'upstream.study.query_elements');
  assert.equal(calls[0].args.filters[0].value, '外牆'); assert.equal(calls[0].timeout, 60000);
});
test('Study validation rejects missing write IDs and out-of-range capture dimensions', async t => {
  const { client, calls } = await setup(t);
  for (const [name, args] of [ ['open_document', { documentId, filePath: 'C:/test.rvt' }], ['capture_view_image', { documentId, operationId, pixelSize: 10 }] ]) {
    assert.equal((await client.callTool({ name: 'revit_study_' + name, arguments: args })).isError, true);
  }
  assert.equal(calls.length, 0);
});
test('Study native failure keeps partial-change diagnostics and is not presented as rollback', async t => {
  const { client } = await setup(t, () => ({ status: 'failed', partialChangesMayRemain: true, error: 'native failure' }));
  const result = await client.callTool({ name: 'revit_study_open_document', arguments: { documentId: 'application', operationId, filePath: 'C:/test.rvt' } });
  assert.equal(result.isError, true);
  assert.equal(JSON.parse(result.content[0].text).status, 'failed');
  assert.equal(JSON.parse(result.content[0].text).partialChangesMayRemain, true);
});
test('Study screenshot is an MCP image block, never a base64 text dump; uses longer deadline', async t => {
  const { client, calls } = await setup(t, () => ({ status: 'completed', result: { ImageBase64: 'c25hcHNob3Q=', MimeType: 'image/png', Width: 1600 } }));
  const result = await client.callTool({ name: 'revit_study_capture_view_image', arguments: { documentId, operationId } });
  assert.equal(calls[0].timeout, 120000);
  assert.equal(result.content[1].type, 'image'); assert.equal(result.content[1].data, 'c25hcHNob3Q=');
  assert.ok(!result.content[0].text.includes('c25hcHNob3Q='));
  assert.equal(JSON.parse(result.content[0].text).result.Width, 1600);
});
for (const name of ['revit_study_open_document', 'revit_private_example']) test(name + ' uses parameter confirmation; declining blocks native call', async () => {
  const calls = [];
  const gateway = new McpGateway(() => {}, async request => { assert.equal(request.preview.kind, 'upstream'); return false; }, {
    callTool: async request => { calls.push(request.name); return { content: [{ type: 'text', text: JSON.stringify({ documentId, title: 'Test' }) }] }; }, close: async () => {}
  });
  gateway.tools = [{ name, inputSchema: { type: 'object' }, annotations: { readOnlyHint: false } }];
  assert.equal((await gateway.call(name, { documentId, operationId }, new AbortController().signal)).isError, true);
  assert.deepEqual(calls, ['revit_project_info']);
});
test('pristine upstream snapshots match every recorded hash and do not include private source', async () => {
  for (const source of ['mcp-servers-for-revit', 'revit-mcp-study']) {
    const base = new URL('../../third_party/' + source + '/', import.meta.url);
    const manifest = JSON.parse(await readFile(new URL('UPSTREAM.json', base), 'utf8'));
    assert.ok(manifest.files.length > 100);
    for (const file of manifest.files) {
      assert.ok(file.snapshotPath.startsWith('original/'));
      assert.ok(!file.sourcePath.startsWith('private/'));
      const hash = createHash('sha256').update(await readFile(new URL(file.snapshotPath, base))).digest('hex').toUpperCase();
      assert.equal(hash, file.originalSha256, file.snapshotPath);
    }
  }
});
