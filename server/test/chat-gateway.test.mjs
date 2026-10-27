import { test } from 'node:test';
import assert from 'node:assert/strict';
import { McpGateway } from '../build/chat/gateway.js';
const previewName = 'revit_preview_doors_along_walls';
const executeName = 'revit_execute_doors_along_walls';
const statusName = 'revit_get_door_operation_status';
const args = { documentId: 'doc', planId: 'plan' };
const preview = { ...args, title: 'Test model', ready: 1, skipped: 0 };
const frame = data => ({ content: [{ type: 'text', text: JSON.stringify(data) }] });
test('explicit native service refusal is not confused with an unknown committed write', async () => {
  let attempts=0;
  const client={callTool:async request=>{
    if(request.name==='revit_project_info')return frame({documentId:'doc',title:'Model'});
    attempts++;return attempts===1 ? {isError:true,content:[{type:'text',text:'MCP_SERVICE_DISABLED'}]} : frame({status:'completed'});
  },close:async()=>{}};
  const gateway=new McpGateway(()=>{},async()=>true,client);
  gateway.tools=[{name:'revit_private_create_walls_from_cad',annotations:{readOnlyHint:false}}];
  const input={documentId:'doc',operationId:'b'.repeat(32)};
  const refused=await gateway.call('revit_private_create_walls_from_cad',input,new AbortController().signal);
  assert.equal(JSON.parse(refused.content[0].text).notExecuted,true);
  assert.equal((await gateway.call('revit_private_create_walls_from_cad',input,new AbortController().signal)).isError,false);
  assert.equal(attempts,2);
});
function upstreamSetup(approve, handle) {
  const calls = [];
  const client = { callTool: async request => {
    calls.push(request);
    return request.name === 'revit_project_info' ? frame({ documentId: 'doc', title: 'Test model' }) : handle(request);
  }, close: async () => {} };
  const gateway = new McpGateway(() => {}, approve, client);
  gateway.tools = ['revit_upstream_create_level', 'revit_upstream_operation_status'].map(name => ({ name, inputSchema: { type: 'object' }, annotations: { readOnlyHint: name.endsWith('operation_status') } }));
  return { gateway, calls };
}
test('upstream writes require concrete parameter confirmation tied to current project', async () => {
  let confirmation;
  const { gateway, calls } = upstreamSetup(async request => { confirmation = request; return true; }, () => frame({ status: 'completed' }));
  const input = { documentId: 'doc', operationId: 'c'.repeat(32), levels: [{ name: 'FL2', elevation: 3500 }] };
  await gateway.call('revit_upstream_create_level', input, new AbortController().signal);
  assert.equal(confirmation.preview.kind, 'upstream');
  assert.equal(confirmation.preview.title, 'Test model');
  assert.deepEqual(confirmation.preview.arguments, input);
  assert.deepEqual(calls.map(c => c.name), ['revit_project_info', 'revit_upstream_create_level']);
});
test('declining an upstream operation never dispatches the write', async () => {
  const { gateway, calls } = upstreamSetup(async () => false, () => { throw new Error('must not dispatch'); });
  const result = await gateway.call('revit_upstream_create_level', { documentId: 'doc', operationId: 'c'.repeat(32) }, new AbortController().signal);
  assert.equal(result.isError, true);
  assert.deepEqual(calls.map(c => c.name), ['revit_project_info']);
});
test('upstream timeout blocks writes until status is known, including a confirmed rollback error', async () => {
  let dispatches = 0;
  const { gateway } = upstreamSetup(async () => true, request => {
    if (request.name.endsWith('operation_status')) return { ...frame({ status: 'rolledBack' }), isError: true };
    dispatches++; if (dispatches === 1) throw new Error('REVIT_TIMEOUT');
    return frame({ status: 'completed' });
  });
  const input = { documentId: 'doc', operationId: 'c'.repeat(32) }, signal = new AbortController().signal;
  await gateway.call('revit_upstream_create_level', input, signal);
  const blocked = await gateway.call('revit_upstream_create_level', input, signal);
  assert.match(blocked.content[0].text, /operationId/); assert.equal(dispatches, 1);
  await gateway.call('revit_upstream_operation_status', input, signal);
  await gateway.call('revit_upstream_create_level', { ...input, operationId: 'd'.repeat(32) }, signal);
  assert.equal(dispatches, 2);
});
function setup(approve = async () => true, handle) {
  const calls = [], events = [];
  const client = { callTool: async request => { calls.push(request); return handle ? handle(request) : frame(request.name === previewName ? preview : { ...args, status: 'completed', createdCount: 1 }); }, close: async () => {} };
  const gateway = new McpGateway(event => events.push(event), approve, client);
  gateway.tools = [previewName, executeName, statusName, 'revit_health'].map(name => ({ name, inputSchema: { type: 'object' }, annotations: { readOnlyHint: name !== executeName } }));
  return { gateway, calls, events };
}
test('write requires a matching preview from the same conversation and document', async () => {
  const { gateway, calls } = setup();
  const result = await gateway.call(executeName, args, new AbortController().signal);
  assert.equal(result.isError, true); assert.equal(calls.length, 0);
  await gateway.call(previewName, { documentId: 'doc' }, new AbortController().signal);
  const wrongDocument = await gateway.call(executeName, { ...args, documentId: 'other' }, new AbortController().signal);
  assert.equal(wrongDocument.isError, true); assert.equal(calls.length, 1);
});
test('declined confirmation never dispatches a model write', async () => {
  let approvals = 0;
  const { gateway, calls } = setup(async () => { approvals++; return false; });
  await gateway.call(previewName, {}, new AbortController().signal);
  assert.equal((await gateway.call(executeName, args, new AbortController().signal)).isError, true);
  assert.equal(approvals, 1); assert.equal(calls.length, 1);
});
test('confirmation receives concrete preview, then exact approved plan is dispatched', async () => {
  let request;
  const { gateway, calls } = setup(async r => { request = r; return true; });
  await gateway.call(previewName, {}, new AbortController().signal);
  await gateway.call(executeName, args, new AbortController().signal);
  assert.equal(request.preview.title, 'Test model');
  assert.deepEqual(calls[1].arguments, args);
});
test('a timed out write blocks retries and new plans until status is checked', async () => {
  const { gateway, calls } = setup(undefined, request => {
    if (request.name === previewName) return frame(preview);
    if (request.name === executeName) return { isError: true, content: [{ type: 'text', text: 'REVIT_TIMEOUT' }] };
    return frame({ ...args, status: 'completed', createdCount: 1 });
  });
  const signal = new AbortController().signal;
  await gateway.call(previewName, {}, signal);
  await gateway.call(executeName, args, signal);
  assert.equal((await gateway.call(executeName, args, signal)).isError, true);
  assert.equal((await gateway.call(previewName, {}, signal)).isError, true);
  assert.equal(calls.length, 2);
  await gateway.call(statusName, args, signal);
  assert.ok(!(await gateway.call(previewName, {}, signal)).isError);
});
test('stop during an already-dispatched write still waits for its real result', async () => {
  let finishWrite;
  const controller = new AbortController();
  const { gateway, events } = setup(undefined, request => request.name === previewName ? frame(preview) : new Promise(resolve => { finishWrite = resolve; }));
  await gateway.call(previewName, {}, controller.signal);
  const writing = gateway.call(executeName, args, controller.signal);
  while (!finishWrite) await new Promise(resolve => setImmediate(resolve));
  controller.abort();
  let idle = false;
  const idlePromise = gateway.idle().then(() => { idle = true; });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(idle, false);
  finishWrite(frame({ ...args, status: 'completed', createdCount: 1 }));
  await writing; await idlePromise;
  assert.ok(events.some(e => e.event === 'toolCompleted' && e.data?.createdCount === 1));
});
test('stop while preview confirmation is open prevents dispatch even after acceptance', async () => {
  const controller = new AbortController();
  const { gateway, calls } = setup(async () => { controller.abort(); return true; });
  await gateway.call(previewName, {}, controller.signal);
  await assert.rejects(gateway.call(executeName, args, controller.signal));
  assert.equal(calls.length, 1);
});
test('unknown tools are rejected and concurrent reads remain serialized', async () => {
  let running = 0, peak = 0;
  const { gateway, calls } = setup(undefined, async () => { peak = Math.max(peak, ++running); await new Promise(resolve => setImmediate(resolve)); running--; return frame({ ok: true }); });
  const signal = new AbortController().signal;
  assert.equal((await gateway.call('shell', {}, signal)).isError, true);
  await Promise.all([gateway.call('revit_health', {}, signal), gateway.call('revit_health', {}, signal)]);
  assert.equal(calls.length, 2); assert.equal(peak, 1);
});
