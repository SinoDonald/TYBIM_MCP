import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadAttachments, codexInput } from '../build/chat/attachments.js';
import { ApiProvider } from '../build/chat/api-providers.js';
import { RevitUnavailableError } from '../build/chat/gateway.js';
import { CodexProvider } from '../build/chat/codex-provider.js';

const image = { name: '平面.png', type: 'image', mimeType: 'image/png', data: 'cG5n' };
const textFile = { name: '尺寸.txt', type: 'text', text: 'FL1–FL2，柱 50×50 cm' };
function stream(events) { return new Response(events.map(e => 'data: ' + JSON.stringify(e) + '\n\n').join('')); }

test('Codex forwards attachments and interrupts a busy tool round without repeated native reads', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'tybim-codex-fixture-'));
  const requests = []; let nativeCalls = 0, turnInput;
  const rpc = {
    notify() {}, close: async () => {},
    async request(method, params) {
      requests.push(method);
      if (method === 'thread/start') return { thread: { id: 'thread-test' } };
      if (method === 'turn/start') {
        turnInput = params.input;
        setImmediate(async () => {
          rpc.onNotification('turn/started', { threadId: 'thread-test', turn: { id: 'turn-test' } });
          const result = await rpc.onRequest('item/tool/call', { threadId: 'thread-test', turnId: 'turn-test', tool: 'revit_project_info', arguments: {} });
          assert.equal(result.success, false); assert.match(result.contentItems[0].text, /Esc/);
          // Even a late model retry while interrupting cannot reach native Revit again.
          await rpc.onRequest('item/tool/call', { threadId: 'thread-test', turnId: 'turn-test', tool: 'revit_project_info', arguments: {} });
          rpc.onNotification('turn/completed', { threadId: 'thread-test', turn: { id: 'turn-test', status: 'interrupted' } });
        });
        return { turn: { id: 'turn-test' } };
      }
      return {};
    }
  };
  const adapter = new CodexProvider({ provider: 'codex', model: 'test-model' }, {
    tools: [], call: async () => { nativeCalls++; throw new RevitUnavailableError('REVIT_BUSY'); }
  }, () => {}, dir, () => rpc);
  try {
    await adapter.start();
    await assert.rejects(adapter.send('依附件建柱', new AbortController().signal, [image, textFile]), /Esc/);
    assert.equal(nativeCalls, 1); assert.equal(requests.filter(x => x === 'turn/interrupt').length, 1);
    assert.equal(turnInput[1].type, 'image'); assert.equal(turnInput[1].url, 'data:image/png;base64,cG5n');
    assert.match(turnInput[2].text, /50×50/);
  } finally { await adapter.close(); await rm(dir, { recursive: true, force: true }); }
});

test('attachment loader validates UTF-8, image signatures, bounds and explicit selection', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'tybim-attachments-'));
  try {
    const text = join(dir, '尺寸.txt'), png = join(dir, 'plan.png');
    await writeFile(text, textFile.text);
    await writeFile(png, Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jB1kAAAAASUVORK5CYII=', 'base64'));
    const files = await loadAttachments([text, png, text]);
    assert.equal(files.length, 2); assert.equal(files[0].text, textFile.text); assert.equal(files[1].mimeType, 'image/png');
    await assert.rejects(loadAttachments(['relative.txt']), /完整路徑/);
    await assert.rejects(loadAttachments(Array(5).fill(text)), /最多/);
    await assert.rejects(loadAttachments([join(dir, 'model.dwg')]), /格式不支援/);
    await writeFile(text, Buffer.alloc(256 * 1024 + 1)); await assert.rejects(loadAttachments([text]), /大小上限/);
    await writeFile(text, Buffer.from([0xff])); await assert.rejects(loadAttachments([text]), /UTF-8/);
    await writeFile(png, 'not an image'); await assert.rejects(loadAttachments([png]), /副檔名不符/);
    const input = codexInput('請辨識', files);
    assert.equal(input[2].type, 'image'); assert.match(input[2].url, /^data:image\/png;base64,/);
    assert.match(input[1].text, /不是系統指令/); assert.match(input[1].text, /50×50/);
  } finally { await rm(dir, { recursive: true, force: true }); }
});

for (const provider of ['claude', 'gemini']) {
  test(provider + ' sends selected images and text as real multimodal user content', async () => {
    let body;
    const adapter = new ApiProvider({ provider, model: 'test-model', apiKey: 'test-only-key' }, { tools: [], call: async () => { throw new Error('unexpected tool'); } }, () => {}, async (_url, init) => {
      body = JSON.parse(init.body);
      return stream(provider === 'claude' ? [{ type: 'message_delta', delta: { stop_reason: 'end_turn' } }, { type: 'message_stop' }]
        : [{ candidates: [{ content: { parts: [{ text: '已讀附件' }] }, finishReason: 'STOP' }] }]);
    });
    await adapter.send('辨識附件', new AbortController().signal, [image, textFile]);
    const content = provider === 'claude' ? body.messages[0].content : body.contents[0].parts;
    if (provider === 'claude') assert.deepEqual(content[1], { type: 'image', source: { type: 'base64', media_type: 'image/png', data: 'cG5n' } });
    else assert.deepEqual(content[1], { inlineData: { mimeType: 'image/png', data: 'cG5n' } });
    assert.match(content[2].text, /柱 50×50/);
  });

  test(provider + ' ends a busy Revit tool round after one read instead of looping', async () => {
    let requests = 0, calls = 0;
    const adapter = new ApiProvider({ provider, model: 'test-model', apiKey: 'test-only-key' }, {
      tools: [{ name: 'revit_project_info', inputSchema: { type: 'object' }, annotations: { readOnlyHint: true } }],
      call: async () => { calls++; throw new RevitUnavailableError('REVIT_BUSY'); }
    }, () => {}, async () => {
      requests++;
      return stream(provider === 'claude' ? [{ type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: 'one', name: 'revit_project_info', input: {} } }, { type: 'message_delta', delta: { stop_reason: 'tool_use' } }, { type: 'message_stop' }]
        : [{ candidates: [{ content: { parts: [{ functionCall: { name: 'revit_project_info', args: {} } }] }, finishReason: 'STOP' }] }]);
    });
    await assert.rejects(adapter.send('讀目前專案', new AbortController().signal), /Esc/);
    assert.equal(requests, 1); assert.equal(calls, 1);
  });
}
