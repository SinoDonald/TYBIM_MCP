// Records ONLY files already copied into the pristine snapshots. Does not fetch
// or overwrite upstream sources, adaptations or private extensions.
import { readdir, readFile, writeFile, access } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve, relative, join } from 'node:path';
const root = resolve(import.meta.dirname, '..');
async function walk(path) {
  const result = [];
  for (const entry of await readdir(path, { withFileTypes: true })) {
    if (entry.isDirectory()) result.push(...await walk(join(path, entry.name)));
    else result.push(join(path, entry.name));
  }
  return result;
}
const sources = [
  { name: 'mcp-servers-for-revit', repository: 'https://github.com/mcp-servers-for-revit/mcp-servers-for-revit', commit: '86cf7057785f2423fc0d0892c9a5d9c93deb1aa3', scopes: [{ directory: 'commandset', extensions: ['.cs', '.csproj'] }, { directory: 'server/src/tools', extensions: ['.ts'] }, { directory: 'server/src/database', extensions: ['.ts'] }] },
  { name: 'revit-mcp-study', repository: 'https://github.com/shuotao/REVIT_MCP_study', commit: '9a6ebf52958f2c5c69bcf570ca1d5ff5e2a88f68', scopes: [{ directory: 'MCP', extensions: ['.cs', '.csproj', '.png', '.json'] }, { directory: 'MCP-Server/src/tools', extensions: ['.ts'] }] }
];
const hash = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase();
for (const source of sources) {
  const base = join(root, 'third_party', source.name), snapshot = join(base, 'original');
  const files = [];
  for (const file of (await walk(snapshot)).sort()) {
    const sourcePath = relative(snapshot, file).replaceAll('\\', '/');
    let adaptedPath;
    if (source.name === 'mcp-servers-for-revit') {
      if (sourcePath.startsWith('commandset/')) adaptedPath = 'integrations/' + source.name + '/revit/' + sourcePath.slice(11);
      if (sourcePath.startsWith('server/src/')) adaptedPath = 'server/src/integrations/' + source.name + '/' + sourcePath.slice(11);
    } else {
      if (sourcePath.startsWith('MCP/')) adaptedPath = 'integrations/' + source.name + '/revit/' + sourcePath.slice(4);
      if (sourcePath.endsWith('ezdxf_worker.py')) adaptedPath = 'integrations/' + source.name + '/ezdxf_worker.py';
    }
    const entry = { sourcePath, snapshotPath: 'original/' + sourcePath, originalSha256: hash(await readFile(file)) };
    if (adaptedPath) {
      try { await access(join(root, adaptedPath)); entry.adaptedPath = adaptedPath; entry.adaptedSha256 = hash(await readFile(join(root, adaptedPath))); } catch { /* archived or generated elsewhere */ }
    }
    files.push(entry);
  }
  await writeFile(join(base, 'UPSTREAM.json'), JSON.stringify({ ...source, importedOn: '2026-10-21', snapshotRoot: 'original', adaptedRoot: 'integrations/' + source.name,
    license: source.name === 'revit-mcp-study' ? 'MIT declared in upstream README and package.json; see NOTICE.md' : 'MIT; see LICENSE',
    updatePolicy: 'Compare against this pinned baseline, then merge adaptations deliberately. Never overwrite TYBIM or private code.', files }, null, 2) + '\n');
  console.log(source.name + ': ' + files.length + ' snapshot files recorded');
}
