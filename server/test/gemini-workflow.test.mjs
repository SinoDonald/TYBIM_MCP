import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { createServer } from '../build/tools.js';
import { ApiProvider } from '../build/chat/api-providers.js';
import { ApiTools, searchToolName } from '../build/chat/api-tools.js';
import { McpGateway } from '../build/chat/gateway.js';
import { ChatRuntime } from '../build/chat/runtime.js';

const stream = parts => new Response('data: ' + JSON.stringify({ candidates: [{ content: { parts }, finishReason: 'STOP' }] }) + '\n\n');

test('Gemini list excludes TTS, media, legacy, preview and models without generateContent', async () => {
  const ids = ['gemini-3.5-flash-lite', 'gemini-3.8-flash-tts', 'gemini-3.8-flash', 'gemini-2.5-flash', 'gemini-3.1-flash-image', 'gemini-3-flash-preview'];
  const provider = new ApiProvider({ provider: 'gemini', apiKey: 'test-key' }, { tools: [] }, () => {}, async () => Response.json({
    models: ids.map(id => ({ name: 'models/' + id, supportedGenerationMethods: ['generateContent'] }))
      .concat({ name: 'models/gemini-3.8-flash', supportedGenerationMethods: ['countTokens'] })
  }));
  assert.deepEqual((await provider.models()).map(model => model.id), ['gemini-3.8-flash', 'gemini-3.5-flash-lite']);
});

test('Gemini configure replaces saved unsupported model with available recommended model', async t => {
  t.mock.method(ApiProvider.prototype, 'models', async () => [{ id: 'gemini-3.8-flash', name: 'Flash' }]);
  const runtime = new ChatRuntime('unused', 'unused', () => {});
  t.after(() => runtime.close());
  const result = await runtime.configure({ provider: 'gemini', apiKey: 'test-key', model: 'gemini-3.8-flash-tts' });
  assert.equal(result.selectedModel, 'gemini-3.8-flash');
});

async function setup(t) {
  const dispatched = [], approvals = [], events = [];
  const server = createServer({ call: async (method, args) => {
    dispatched.push({ method, args });
    if (method === 'revit.project_info') return { documentId: 'doc', title: 'Test model' };
    if (method === 'revit.levels') return { levels: [{ name: 'FL5', elevationMm: 17500 }] };
    return { status: 'completed', createdCount: 5 };
  } });
  const client = new Client({ name: 'gemini-workflow-test', version: '1' });
  const [a, b] = InMemoryTransport.createLinkedPair();
  await server.connect(b); await client.connect(a);
  t.after(async () => { await client.close(); await server.close(); });
  const tools = (await client.listTools()).tools;
  const gateway = new McpGateway(event => events.push(event), async request => { approvals.push(request); return true; }, client);
  gateway.tools = tools;
  return { tools, gateway, dispatched, approvals, events };
}

test('full catalog stays discoverable while level request omits most unrelated schemas', async t => {
  const { tools } = await setup(t);
  const toolbox = new ApiTools(tools);
  toolbox.load('幫我建立樓層 FL6-FL10，每層間距350cm');
  assert.equal(tools.length, 258);
  assert.ok(toolbox.definitions().length <= 16);
  const originalBytes = Buffer.byteLength(JSON.stringify(tools.map(tool => tool.inputSchema)));
  const selectedBytes = Buffer.byteLength(JSON.stringify(toolbox.definitions().map(tool => tool.inputSchema)));
  assert.ok(selectedBytes < originalBytes / 4, `${selectedBytes}/${originalBytes} schema bytes`);
  for (const tool of tools) {
    const result = toolbox.search({ query: tool.name });
    assert.equal(result.isError, false);
    assert.ok(toolbox.has(tool.name), tool.name + ' remains discoverable');
  }
  console.log(JSON.stringify({ catalogTools: tools.length, originalSchemaBytes: originalBytes, levelRequestSchemaBytes: selectedBytes }));
});

for (const providerName of ['gemini', 'claude']) test(providerName + ' batch level request crosses real MCP schemas and approval gateway once', async t => {
  const { gateway, dispatched, approvals, events } = await setup(t);
  const levels = Array.from({ length: 5 }, (_, i) => ({ name: 'FL' + (i + 6), elevation: 21000 + i * 3500, createFloorPlan: false, createCeilingPlan: false }));
  const requests = [];
  const plan = [
    { name: 'revit_project_info', args: {} },
    { name: 'revit_get_levels', args: { documentId: 'doc' } },
    { name: 'revit_upstream_create_level', args: { documentId: 'doc', operationId: 'a'.repeat(32), data: levels } }
  ];
  const provider = new ApiProvider({ provider: providerName, apiKey: 'test-key', model: providerName === 'gemini' ? 'gemini-3.8-flash' : 'claude-sonnet-5-5' }, gateway, event => events.push(event), async (_url, init) => {
    const body = JSON.parse(init.body);
    requests.push(body);
    if (providerName === 'gemini') assert.equal(body.generationConfig.thinkingConfig.thinkingLevel, 'low');
    assert.ok((providerName === 'gemini' ? body.tools[0].functionDeclarations : body.tools).length <= 16);
    const call = plan[requests.length - 1];
    if (providerName === 'claude') return new Response((call ? [
      { type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: 'call-' + requests.length, name: call.name, input: call.args } },
      { type: 'message_delta', delta: { stop_reason: 'tool_use' } }, { type: 'message_stop' }
    ] : [{ type: 'message_delta', delta: { stop_reason: 'end_turn' } }, { type: 'message_stop' }]).map(event => 'data: ' + JSON.stringify(event) + '\n\n').join(''));
    return stream(call ? [{ functionCall: { ...call, id: 'call-' + requests.length }, thoughtSignature: 'opaque-' + requests.length }] : [{ text: '完成' }]);
  });
  await provider.send('建立樓層FL6到FL10，每層350cm', new AbortController().signal);
  assert.equal(approvals.length, 1);
  assert.deepEqual(approvals[0].arguments.data, levels);
  const writes = dispatched.filter(call => call.method === 'upstream.create_level');
  assert.equal(writes.length, 1);
  assert.deepEqual(writes[0].args.data.map(level => level.elevation), [21000, 24500, 28000, 31500, 35000]);
  assert.ok(events.some(event => event.event === 'toolCompleted' && event.data?.createdCount === 5));
  if (providerName === 'gemini') assert.equal(requests[3].contents[5].parts[0].thoughtSignature, 'opaque-3');
});

test('Gemini discovers additional tools locally then calls through the gateway', async () => {
  const tool = { name: 'revit_private_custom_task', description: 'Custom private task', inputSchema: { type: 'object', properties: {} }, annotations: { readOnlyHint: true } };
  const calls = [], requests = [];
  const provider = new ApiProvider({ provider: 'gemini', apiKey: 'test-key', model: 'gemini-3.8-flash' }, {
    tools: [tool], call: async name => { calls.push(name); return { content: [{ type: 'text', text: 'ok' }] }; }
  }, () => {}, async (_url, init) => {
    requests.push(JSON.parse(init.body));
    if (requests.length === 1) return stream([{ functionCall: { name: searchToolName, args: { query: tool.name } } }]);
    if (requests.length === 2) {
      assert.ok(requests[1].tools[0].functionDeclarations.some(definition => definition.name === tool.name));
      return stream([{ functionCall: { name: tool.name, args: {} } }]);
    }
    return stream([{ text: 'done' }]);
  });
  await provider.send('hello', new AbortController().signal);
  assert.deepEqual(calls, [tool.name]);
});
