// Rebuild metadata from our immutable source snapshot, never from a user's
// private code. Only upstream definitions are evaluated, in a local staging dir.
import ts from 'typescript';
import { readFile, writeFile, mkdir, readdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve, join } from 'node:path';
const root = fileURLToPath(new URL('../../', import.meta.url));
const original = join(root, 'third_party/revit-mcp-study/original');
const source = join(original, 'MCP-Server/src/tools');
const stage = join(root, 'artifacts/study-catalog');
await mkdir(join(stage, 'tools'), { recursive: true });
await mkdir(join(stage, 'apps'), { recursive: true });
await writeFile(join(stage, 'package.json'), '{"type":"module"}');
await writeFile(join(stage, 'apps/register-apps.js'), 'export const withAppUi = tool => tool;\n');
for (const file of await readdir(source)) {
  if (!file.endsWith('.ts') || file === 'revit-tools.ts') continue;
  const content = await readFile(join(source, file), 'utf8');
  const result = ts.transpileModule(content, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } });
  await writeFile(join(stage, 'tools', file.replace(/\.ts$/, '.js')), result.outputText);
}
const oldProfile = process.env.MCP_PROFILE; process.env.MCP_PROFILE = 'full';
const { registerRevitTools } = await import(pathToFileURL(join(stage, 'tools/index.js')).href);
const tools = registerRevitTools();
if (oldProfile === undefined) delete process.env.MCP_PROFILE; else process.env.MCP_PROFILE = oldProfile;
const nativeSource = await readFile(join(original, 'MCP/Core/CommandExecutor.cs'), 'utf8');
const dispatchStart = nativeSource.indexOf('switch (request.CommandName.ToLower())');
const dispatch = nativeSource.slice(dispatchStart, nativeSource.indexOf('default:', dispatchStart));
const commands = new Set([...dispatch.matchAll(/case\s+"([^"]+)"\s*:/g)].map(m => m[1]));
const externalEffects = new Set(['open_document', 'save_document', 'close_document', 'reload_links', 'read_source_file_sheets',
  'copy_sheets_from_file', 'sync_sheet_parameters_from_source', 'inject_green_material_into_family', 'capture_view_image',
  'export_clash_report', 'export_families', 'export_smoke_review_excel', 'grade_toposolid']);
const entries = tools.map(tool => {
  const command = tool.name === 'query_elements_with_filter' ? 'query_elements' : tool.name;
  // File exports must be confirmed even when upstream calls them model-read-only.
  if (externalEffects.has(command) || command === 'import_excel_to_drafting_views') tool.annotations = { ...tool.annotations, readOnlyHint: false };
  return { tool, command, implemented: commands.has(command), externalEffects: externalEffects.has(command) };
});
if (new Set(entries.map(e => e.tool.name)).size !== entries.length) throw new Error('Duplicate upstream tool names');
const dest = join(root, 'server/src/integrations/revit-mcp-study');
await mkdir(dest, { recursive: true });
await writeFile(join(dest, 'catalog.json'), JSON.stringify(entries, null, 2) + '\n');
await writeFile(join(root, 'third_party/revit-mcp-study/catalog.json'), JSON.stringify({ tools: entries.map(e => ({ name: e.tool.name, command: e.command, implemented: e.implemented, readOnly: e.tool.annotations?.readOnlyHint === true })), nativeCommands: [...commands].sort() }, null, 2) + '\n');
const implemented = entries.filter(e => e.implemented);
// Multiple MCP tools can be aliases for one native command. Dictionary.Add
// rejects duplicate keys at CLR startup, even though C# compilation succeeds.
// Keep both public tool schemas but register each native command exactly once.
// If aliases disagree on permissions, require confirmation for the shared call.
const nativePermissions = new Map();
for (const entry of implemented) {
  const readOnly = entry.tool.annotations?.readOnlyHint === true;
  nativePermissions.set(entry.command, (nativePermissions.get(entry.command) ?? true) && readOnly);
}
const lines = [...nativePermissions].map(([command, readOnly]) => `            { "${command}", ${readOnly} },`);
const external = [...new Set(implemented.filter(e => e.externalEffects).map(e => e.command))].map(command => `"${command}"`).join(', ');
await writeFile(join(root, 'src/Tybim.Revit.Study/StudyCatalog.cs'), `// Generated from the pinned upstream tool catalog. Rebuild with build-study-catalog.mjs.\nusing System.Collections.Generic;\nnamespace Tybim.Revit.Study\n{\n    public static class StudyCatalog\n    {\n        public static readonly IReadOnlyDictionary<string, bool> Commands = new Dictionary<string, bool>\n        {\n${lines.join('\n')}\n        };\n        public static readonly HashSet<string> ExternalEffects = new HashSet<string> { ${external} };\n        public static bool ApplicationScoped(string name) => name == "open_document" || name == "list_open_documents";\n    }\n}\n`);
console.log(JSON.stringify({ toolDefinitions: entries.length, matchedCommands: entries.filter(e => e.implemented).length, missing: entries.filter(e => !e.implemented).map(e => e.command), nativeCommandCases: commands.size }));
