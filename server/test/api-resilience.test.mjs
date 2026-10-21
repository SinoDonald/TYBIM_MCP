import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ApiProvider } from '../build/chat/api-providers.js';
import { fetchWithRetry, checkResponse } from '../build/chat/http.js';
import { ChatRuntime } from '../build/chat/runtime.js';

test('Claude lists active supported families, follows pagination and prefers Sonnet then Haiku', async () => {
  const urls = [];
  const provider = new ApiProvider({ provider: 'claude', apiKey: 'test-key' }, { tools: [] }, () => {}, async url => {
    urls.push(url);
    return Response.json(urls.length === 1 ? {
      data: [{ id: 'claude-opus-5-5', lifecycle: 'active' }, { id: 'claude-sonnet-5-5', lifecycle: 'active' }, { id: 'claude-sonnet-4-5', lifecycle: 'retired' }], has_more: true, last_id: 'claude-sonnet-4-5'
    } : { data: [{ id: 'claude-haiku-5-5', lifecycle: 'active' }, { id: 'claude-3-opus-20240229' }, { id: 'claude-sonnet-5-preview', lifecycle: 'active' }], has_more: false });
  });
  assert.deepEqual((await provider.models()).map(model => model.id), ['claude-sonnet-5-5', 'claude-haiku-5-5', 'claude-opus-5-5']);
  assert.match(urls[1], /after_id=claude-sonnet-4-5/);
});

test('Claude configure replaces unavailable saved model using the live filtered list', async t => {
  t.mock.method(ApiProvider.prototype, 'models', async () => [{ id: 'claude-sonnet-5-5', name: 'Sonnet' }]);
  const runtime = new ChatRuntime('unused', 'unused', () => {});
  t.after(() => runtime.close());
  const result = await runtime.configure({ provider: 'claude', apiKey: 'test-key', model: 'retired-model' });
  assert.equal(result.selectedModel, 'claude-sonnet-5-5');
});

test('temporary HTTP overload retries are bounded and reuse the same request and deadline', async () => {
  const waits = [], notifications = [], seen = [];
  const init = { method: 'POST', body: '{"test":true}', signal: new AbortController().signal };
  const response = await fetchWithRetry(async (_url, request) => {
    seen.push(request);
    return new Response('busy', { status: 529 });
  }, 'https://example.invalid', init, (...args) => notifications.push(args), async ms => { waits.push(ms); });
  assert.equal(response.status, 529);
  assert.equal(seen.length, 3);
  assert.ok(seen.every(request => request === init));
  assert.equal(notifications.length, 2);
  assert.ok(waits[1] > waits[0]);
});

test('authentication, missing model and quota without retry-after never loop', async () => {
  for (const status of [400, 401, 402, 403, 404, 429]) {
    let requests = 0;
    const response = await fetchWithRetry(async () => { requests++; return Response.json({ error: { message: 'test failure' } }, { status }); },
      'https://example.invalid', {}, () => { throw new Error('must not retry'); });
    assert.equal(requests, 1);
    await assert.rejects(checkResponse(response), new RegExp('AI HTTP ' + status));
  }
});

test('short retry-after is honored, while a long quota wait is returned without retry', async () => {
  let requests = 0, waited = 0;
  const response = await fetchWithRetry(async () => ++requests === 1
    ? new Response('', { status: 429, headers: { 'retry-after': '3' } }) : new Response('ok'),
    'https://example.invalid', {}, () => {}, async ms => { waited = ms; });
  assert.equal(response.status, 200); assert.ok(waited >= 3000);
  requests = 0;
  await fetchWithRetry(async () => { requests++; return new Response('', { status: 429, headers: { 'retry-after': '120' } }); },
    'https://example.invalid', {}, () => { throw new Error('must not retry long quota wait'); });
  assert.equal(requests, 1);
});

test('cancelling overload backoff prevents another HTTP request', async () => {
  const controller = new AbortController(); let requests = 0;
  await assert.rejects(fetchWithRetry(async () => { requests++; return new Response('', { status: 503 }); },
    'https://example.invalid', { signal: controller.signal }, () => {}, async () => { controller.abort(); controller.signal.throwIfAborted(); }));
  assert.equal(requests, 1);
});

test('Claude midstream overload never executes a partial tool and never retries the stream', async () => {
  let requests = 0, calls = 0;
  const provider = new ApiProvider({ provider: 'claude', apiKey: 'test-key', model: 'claude-sonnet-5-5' }, {
    tools: [{ name: 'revit_upstream_create_level', inputSchema: { type: 'object' } }], call: async () => { calls++; }
  }, () => {}, async () => {
    requests++;
    return new Response([
      { type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: 'partial', name: 'revit_upstream_create_level', input: {} } },
      { type: 'error', error: { type: 'overloaded_error', message: 'busy' } }
    ].map(event => 'data: ' + JSON.stringify(event) + '\n\n').join(''));
  });
  await assert.rejects(provider.send('create level', new AbortController().signal), /overloaded_error/);
  assert.equal(requests, 1); assert.equal(calls, 0);
});
