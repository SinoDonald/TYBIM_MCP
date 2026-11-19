import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
const available = existsSync(resolve('build/private/wall-evidence.js'));

test('wall evidence reads generated PNGs only and refuses outside/invalid files', { skip: !available }, async t => {
  const { loadWallEvidence } = await import('../build/private/wall-evidence.js');
  const directory=await mkdtemp(join(tmpdir(),'tybim-wall-evidence-'));
  t.after(()=>rm(directory,{recursive:true,force:true}));
  const root=join(directory,'diagnostics'); await mkdir(root);
  const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6QmcAAAAASUVORK5CYII=','base64');
  const inside=join(root,'issue.png'), outside=join(directory,'private.png'), invalid=join(root,'invalid.png');
  await writeFile(inside,png); await writeFile(outside,png); await writeFile(invalid,'not an image');
  const result=await loadWallEvidence({screenshotPaths:[inside,outside,invalid]},root);
  assert.equal(result.images.length,1); assert.equal(result.images[0].mimeType,'image/png');
  assert.equal(result.images[0].data,png.toString('base64')); assert.equal(result.errors.length,2);
  const missing=await loadWallEvidence({screenshotPaths:[join(root,'missing.png')]},root);
  assert.equal(missing.images.length,0); assert.equal(missing.errors.length,1);
});

test('generic auto join requires unambiguous scope; creation defaults to post-commit auto joining', { skip: !available }, async t => {
  const { McpServer }=await import('@modelcontextprotocol/sdk/server/mcp.js');
  const { Client }=await import('@modelcontextprotocol/sdk/client/index.js');
  const { InMemoryTransport }=await import('@modelcontextprotocol/sdk/inMemory.js');
  const { registerPrivateTools }=await import('../build/private/register.js');
  const calls=[];
  const server=new McpServer({name:'auto-join-test',version:'1'});
  registerPrivateTools(server,{call:async(method,args)=>{calls.push({method,args});return {status:'completed',result:{success:true,response:{}}};}});
  const client=new Client({name:'auto-client',version:'1'});
  const [a,b]=InMemoryTransport.createLinkedPair();await server.connect(a);await client.connect(b);
  t.after(async()=>{await client.close();await server.close();});
  const base={documentId:'doc',operationId:'b'.repeat(32)};
  for(const scope of [{},{hostCategory:'walls'},{elementIds:['10','20'],hostCategory:'walls',targetCategories:['walls']},{elementIds:['10','10']}]) {
    const result=await client.callTool({name:'revit_private_auto_join',arguments:{...base,...scope}});
    assert.equal(result.isError,true);
  }
  assert.equal(calls.length,0);
  await client.callTool({name:'revit_private_auto_join',arguments:{...base,hostCategory:'walls',targetCategories:['columns','floors']}});
  assert.equal(calls[0].method,'upstream.private.auto_join');assert.equal(calls[0].args.floorPriority,'preserve');
  await client.callTool({name:'revit_private_auto_join',arguments:{...base,elementIds:['10','20'],action:'unjoin'}});
  assert.equal(calls[1].args.action,'unjoin');
  await client.callTool({name:'revit_private_create_walls_from_cad',arguments:base});
  assert.equal(calls[2].args.autoJoin,true);assert.equal(calls[2].args.placeDoors,false);assert.equal(calls[2].args.placeWindows,false);
  await client.callTool({name:'revit_private_create_walls_from_cad',arguments:{...base,autoJoin:false}});
  assert.equal(calls[3].args.autoJoin,false);
});

test('wall join schema enforces explicit scope and forwards per-pair failures without hiding partial success', { skip: !available }, async t => {
  const { McpServer }=await import('@modelcontextprotocol/sdk/server/mcp.js');
  const { Client }=await import('@modelcontextprotocol/sdk/client/index.js');
  const { InMemoryTransport }=await import('@modelcontextprotocol/sdk/inMemory.js');
  const { registerPrivateTools }=await import('../build/private/register.js');
  let calls=0;
  const server=new McpServer({name:'wall-test',version:'1'});
  registerPrivateTools(server,{call:async(method,args)=>{
    calls++; assert.equal(method,'upstream.private.join_walls');
    assert.deepEqual(args.wallIds,['10','20']); assert.equal(args.mode,'auto');
    return {status:'failed',nativeTransactions:true,partialChangesMayRemain:true,
      result:{success:false,response:{joinedPairs:1,failedPairs:1,issues:[{wallIds:['10','20'],reason:'join failed'}]}}};
  }});
  const client=new Client({name:'wall-client',version:'1'});
  const [a,b]=InMemoryTransport.createLinkedPair(); await server.connect(a); await client.connect(b);
  t.after(async()=>{await client.close();await server.close();});
  const base={documentId:'doc',operationId:'a'.repeat(32)};
  for(const wallIds of [undefined,[],['10'],['10','10']]) {
    const invalid=await client.callTool({name:'revit_private_join_walls',arguments:{...base,wallIds}});
    assert.equal(invalid.isError,true);
  }
  assert.equal(calls,0);
  const result=await client.callTool({name:'revit_private_join_walls',arguments:{...base,wallIds:['10','20']}});
  assert.equal(result.isError,true);
  const data=JSON.parse(result.content[0].text);
  assert.equal(data.partialChangesMayRemain,true); assert.equal(data.result.response.joinedPairs,1);
  assert.equal(data.result.response.issues[0].wallIds[1],'20'); assert.equal(calls,1);
});
