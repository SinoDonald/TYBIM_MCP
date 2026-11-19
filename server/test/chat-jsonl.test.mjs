import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseChatRequest } from '../build/chat/jsonl.js';

test('chat JSONL accepts .NET Framework UTF-8 preamble and preserves Chinese request content', () => {
  const request = { id: 1, method: 'send', params: { text: '建柱 FL1～FL2 🚪' } };
  assert.deepEqual(parseChatRequest('\uFEFF' + JSON.stringify(request)), request);
  assert.deepEqual(parseChatRequest(JSON.stringify(request)), request);
});
test('chat JSONL still rejects malformed or oversized requests', () => {
  assert.throws(() => parseChatRequest('\uFEFF{broken'), SyntaxError);
  assert.throws(() => parseChatRequest(' '.repeat(1024 * 1024 + 1)), /too large/);
});
