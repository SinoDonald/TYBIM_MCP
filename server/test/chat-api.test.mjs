import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ApiProvider } from '../build/chat/api-providers.js';
import { sseJson } from '../build/chat/http.js';

const tool = { name: 'revit_health', description: 'Read Revit version', inputSchema: { type: 'object', properties: {} }, annotations: { readOnlyHint: true } };
function stream(events) { return new Response(events.map(event => 'data: ' + JSON.stringify(event) + '\r\n\r\n').join(''), { headers: { 'Content-Type': 'text/event-stream' } }); }

for (const [providerName, model] of [['claude', 'test-claude'], ['gemini', 'gemini-3-test'], ['gemini', 'gemini-2.5-test']]) {
  test(providerName + '/' + model + ' forwards MCP images using provider image parts', async () => {
    const bodies = [];
    const provider = new ApiProvider({ provider: providerName, model, apiKey: 'test-key' }, {
      tools: [tool], call: async () => ({ content: [{ type: 'text', text: 'metadata' }, { type: 'image', data: 'cG5n', mimeType: 'image/png' }] })
    }, () => {}, async (_url, init) => {
      bodies.push(JSON.parse(init.body));
      if (providerName === 'claude') return stream(bodies.length === 1 ? [
        { type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: 'image-call', name: 'revit_health', input: {} } },
        { type: 'message_delta', delta: { stop_reason: 'tool_use' } }, { type: 'message_stop' }
      ] : [{ type: 'message_delta', delta: { stop_reason: 'end_turn' } }, { type: 'message_stop' }]);
      return stream([{ candidates: [{ content: { parts: bodies.length === 1 ? [{ functionCall: { name: 'revit_health', args: {} } }] : [{ text: 'done' }] }, finishReason: 'STOP' }] }]);
    });
    await provider.send('image test', new AbortController().signal);
    assert.equal(bodies.length, 2);
    if (providerName === 'claude') {
      assert.deepEqual(bodies[1].messages[2].content[0].content[1], { type: 'image', source: { type: 'base64', media_type: 'image/png', data: 'cG5n' } });
    } else {
      const parts = bodies[1].contents[2].parts;
      const image = model.startsWith('gemini-3') ? parts[0].functionResponse.parts[0] : parts[1];
      assert.deepEqual(image.inlineData, { mimeType: 'image/png', data: 'cG5n' });
      assert.ok(!JSON.stringify(parts[0].functionResponse.response).includes('cG5n'));
    }
  });
}
test('SSE parser keeps split UTF-8, CRLF and multiple events', async () => {
  const encoded = Buffer.from('data: {"text":"樓層"}\r\n\r\ndata: {"done":true}\n\n');
  const split = encoded.indexOf(Buffer.from('樓')) + 1;
  const body = new ReadableStream({ start(controller) { controller.enqueue(encoded.subarray(0, split)); controller.enqueue(encoded.subarray(split)); controller.close(); } });
  const result = [];
  for await (const event of sseJson(new Response(body))) result.push(event);
  assert.deepEqual(result, [{ text: '樓層' }, { done: true }]);
});

test('Claude tool input fragments are completed before MCP call and results feed next turn', async () => {
  const bodies = [], calls = [], emitted = [];
  const provider = new ApiProvider({ provider: 'claude', apiKey: 'test-only-key', model: 'test-claude' }, {
    tools: [tool], call: async (name, args) => { calls.push({ name, args }); return { content: [{ type: 'text', text: '{"version":"2025"}' }] }; }
  }, event => emitted.push(event), async (_url, init) => {
    bodies.push(JSON.parse(init.body));
    if (bodies.length === 1) return stream([
      { type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: 'call-1', name: 'revit_health', input: {} } },
      { type: 'content_block_delta', index: 0, delta: { type: 'input_json_delta', partial_json: '{' } },
      { type: 'content_block_delta', index: 0, delta: { type: 'input_json_delta', partial_json: '}' } },
      { type: 'message_delta', delta: { stop_reason: 'tool_use' } }, { type: 'message_stop' }
    ]);
    return stream([{ type: 'content_block_start', index: 0, content_block: { type: 'text', text: '' } }, { type: 'content_block_delta', index: 0, delta: { type: 'text_delta', text: '連線正常' } }, { type: 'message_delta', delta: { stop_reason: 'end_turn' } }, { type: 'message_stop' }]);
  });
  await provider.send('版本?', new AbortController().signal);
  assert.deepEqual(calls, [{ name: 'revit_health', args: {} }]);
  assert.equal(bodies[1].messages[2].content[0].tool_use_id, 'call-1');
  assert.equal(emitted.find(event => event.event === 'textDelta').text, '連線正常');
});

test('truncated Claude tool stream does not execute anything', async () => {
  let called = false;
  const provider = new ApiProvider({ provider: 'claude', apiKey: 'test-key', model: 'test' }, { tools: [tool], call: async () => { called = true; } }, () => {}, async () => stream([
    { type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: 'one', name: 'revit_health', input: {} } }
  ]));
  await assert.rejects(provider.send('test', new AbortController().signal), /提早結束/);
  assert.equal(called, false);
});

test('Gemini preserves thought signatures and function IDs across MCP tool round', async () => {
  const bodies = [];
  const provider = new ApiProvider({ provider: 'gemini', apiKey: 'test-key', model: 'test-gemini' }, {
    tools: [tool], call: async () => ({ content: [{ type: 'text', text: 'ok' }] })
  }, () => {}, async (_url, init) => {
    bodies.push(JSON.parse(init.body));
    if (bodies.length === 1) return stream([{ candidates: [{ content: { parts: [{ thoughtSignature: 'opaque-signature', functionCall: { name: 'revit_health', args: {}, id: 'call-1' } }] }, finishReason: 'STOP' }] }]);
    return stream([{ candidates: [{ content: { parts: [{ text: '完成' }] }, finishReason: 'STOP' }] }]);
  });
  await provider.send('test', new AbortController().signal);
  assert.equal(bodies[1].contents[1].parts[0].thoughtSignature, 'opaque-signature');
  assert.equal(bodies[1].contents[2].parts[0].functionResponse.id, 'call-1');
  assert.ok(bodies[0].tools[0].functionDeclarations[0].parametersJsonSchema);
});

test('Gemini blocked or truncated responses never run tools', async () => {
  let called = false;
  const provider = new ApiProvider({ provider: 'gemini', apiKey: 'test-key', model: 'test' }, { tools: [tool], call: async () => { called = true; } }, () => {}, async () => stream([{ candidates: [{ content: { parts: [{ functionCall: { name: 'revit_health', args: {} } }] }, finishReason: 'MAX_TOKENS' }] }]));
  await assert.rejects(provider.send('test', new AbortController().signal), /未完整完成/);
  assert.equal(called, false);
});

test('cancellation before tool dispatch prevents a write', async () => {
  const controller = new AbortController();
  controller.abort();
  let fetched = false;
  const provider = new ApiProvider({ provider: 'claude', apiKey: 'test-key', model: 'test' }, { tools: [tool], call: async () => { throw new Error('must not run'); } }, () => {}, async () => { fetched = true; throw new Error('must not fetch'); });
  await assert.rejects(provider.send('test', controller.signal));
  assert.equal(fetched, false);
});
