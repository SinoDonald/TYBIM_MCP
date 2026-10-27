using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Tybim.Revit.Addin.Chat
{
    public sealed class ChatLaunchSettings
    {
        public string NodeExecutable { get; set; }
        public string RuntimeScript { get; set; }
        public string CodexExecutable { get; set; }
        public string StateDirectory { get; set; }
        [JsonIgnore] public string BridgeConfig { get; private set; }
        public static ChatLaunchSettings Load(string configPath)
        {
            var directory = Path.GetDirectoryName(configPath);
            var settings = JsonConvert.DeserializeObject<ChatLaunchSettings>(File.ReadAllText(configPath)) ?? throw new InvalidDataException("Invalid chat.config.json");
            if (!string.IsNullOrWhiteSpace(settings.NodeExecutable))
                settings.NodeExecutable = Path.GetFullPath(Path.Combine(directory, settings.NodeExecutable));
            settings.RuntimeScript = Path.GetFullPath(Path.Combine(directory, settings.RuntimeScript ?? "runtime/build/chat/index.js"));
            settings.BridgeConfig = Path.Combine(directory, "bridge.config.json");
            settings.StateDirectory = Environment.ExpandEnvironmentVariables(settings.StateDirectory ?? "%LOCALAPPDATA%\\TYBIM\\McpChat\\2025");
            if (!File.Exists(settings.NodeExecutable) || !File.Exists(settings.RuntimeScript) || !File.Exists(settings.BridgeConfig))
                throw new FileNotFoundException("找不到 Node.js 或對話 runtime，請使用完整的 Revit 對話套件安裝。");
            Directory.CreateDirectory(settings.StateDirectory);
            return settings;
        }
        public string FindCodex(string preferred)
            => FindCodexCandidates(preferred).FirstOrDefault() ?? "";

        public List<string> FindCodexCandidates(string preferred)
        {
            var result = new List<string>();
            void Add(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                var full = Path.GetFullPath(path);
                if (!result.Contains(full, StringComparer.OrdinalIgnoreCase)) result.Add(full);
            }
            foreach (var candidate in new[] { preferred, CodexExecutable }) Add(candidate);
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { Add(Path.Combine(directory.Trim('"'), "codex.exe")); }
                catch (ArgumentException) { }
            }
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
            if (Directory.Exists(root))
            {
                try
                {
                    foreach (var found in Directory.GetFiles(root, "codex.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc)) Add(found);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return result;
        }
    }

    public sealed class ChatPreferences
    {
        public string Provider { get; set; } = "codex";
        public string CodexExecutable { get; set; }
        public Dictionary<string, string> Models { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ProtectedKeys { get; set; } = new Dictionary<string, string>();
        public static ChatPreferences Load(string directory)
        {
            var path = Path.Combine(directory, "ui.settings.json");
            return File.Exists(path) ? JsonConvert.DeserializeObject<ChatPreferences>(File.ReadAllText(path)) ?? new ChatPreferences() : new ChatPreferences();
        }
        public string Key(string provider)
        {
            if (!ProtectedKeys.TryGetValue(provider, out var encoded)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encoded), null, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException) { return ""; }
            catch (FormatException) { return ""; }
        }
        public void SetKey(string provider, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) ProtectedKeys.Remove(provider);
            else ProtectedKeys[provider] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
        }
        public void Save(string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "ui.settings.json"), JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
        }
    }
}
