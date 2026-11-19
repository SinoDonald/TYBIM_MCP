using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tybim.Mcp.Contracts;

namespace Tybim.Revit.Addin
{
    internal static class ExternalAiConfiguration
    {
        internal static BridgeSettings Load(string directory, string year, BridgeSettings internalSettings)
        {
            var path = Path.Combine(directory, "external.bridge.config.json");
            if (!File.Exists(path))
            {
                var bytes = new byte[32];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                var fresh = new { port = 19800 + int.Parse(year), token = Convert.ToBase64String(bytes) };
                try
                {
                    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                        writer.Write(JsonConvert.SerializeObject(fresh, Formatting.Indented));
                }
                catch (IOException) when (File.Exists(path)) { }
            }
            var settings = JsonConvert.DeserializeObject<BridgeSettings>(File.ReadAllText(path));
            if (settings == null) throw new InvalidDataException("外部 AI 連線設定無效。");
            settings.Validate();
            if (settings.Port == 8964 || settings.Port == internalSettings.Port)
                throw new InvalidDataException("外部 AI 埠號不可為 8964，也不可與 AI助手的埠號相同。");
            if (settings.Token == internalSettings.Token)
                throw new InvalidDataException("外部 AI 必須使用獨立的連線權杖。");
            return settings;
        }

        internal static void ExportClient(string directory, string year)
        {
            var chat = JObject.Parse(File.ReadAllText(Path.Combine(directory, "chat.config.json")));
            var node = (string)chat.GetValue("nodeExecutable", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(node)) throw new InvalidDataException("chat.config.json 未設定 NodeExecutable。");
            node = Path.GetFullPath(Path.Combine(directory, node));
            var script = Path.Combine(directory, "runtime", "build", "index.js");
            if (!File.Exists(node) || !File.Exists(script))
                throw new FileNotFoundException("找不到 Node.js 或 MCP runtime，請確認完整的外掛部署內容。");
            var entry = new JObject
            {
                ["command"] = node,
                ["args"] = new JArray(script),
                ["env"] = new JObject { ["TYBIM_MCP_CONFIG"] = Path.Combine(directory, "external.bridge.config.json") }
            };
            var client = new JObject { ["mcpServers"] = new JObject { ["tybim_revit_" + year + "_external"] = entry } };
            // Only executable/config paths are exported, never bridge tokens or AI keys.
            File.WriteAllText(Path.Combine(directory, "external-ai.mcp.json"), client.ToString(), new UTF8Encoding(false));
        }
    }
}
