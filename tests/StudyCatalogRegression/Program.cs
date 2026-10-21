using Tybim.Revit.Study;

// Execute the actual generated C# static initializer. TypeScript tool discovery
// and compilation alone do not instantiate this dictionary during startup.
var commands = StudyCatalog.Commands;
if (commands.Count != 219) throw new Exception($"Expected 219 unique native commands, found {commands.Count}");
Console.WriteLine("PASS: actual StudyCatalog static initializer loads 219 unique native commands");
if (!commands.TryGetValue("query_elements", out var readOnly) || !readOnly) throw new Exception("Shared query alias mapping failed");
Console.WriteLine("PASS: two MCP query aliases share one read-only native command");
if (commands["open_document"] || !StudyCatalog.ExternalEffects.Contains("open_document")) throw new Exception("Open document must remain a confirmed external operation");
Console.WriteLine("PASS: file/document commands retain write confirmation classification");
if (commands.ContainsKey("check_sanitary_fixture_requirements")) throw new Exception("Unimplemented tool must not be registered");
Console.WriteLine("PASS: missing native implementation is excluded");
