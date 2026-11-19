using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Tybim.Revit.Addin
{
    internal static class ExternalAiSetup
    {
        private static readonly Dictionary<string, Process> Running = new Dictionary<string, Process>(StringComparer.OrdinalIgnoreCase);
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        internal static bool ShowExisting(Process process)
        {
            process.Refresh();
            if (process.HasExited) return false;
            var window = process.MainWindowHandle;
            if (window == IntPtr.Zero)
                throw new InvalidOperationException("AI 連線設定精靈仍在啟動中，請稍候再點選。");
            ShowWindowAsync(window, 9); // SW_RESTORE also restores a minimized wizard.
            SetForegroundWindow(window);
            return true;
        }
        internal static void Dispose()
        {
            foreach(var process in Running.Values) process.Dispose();
            Running.Clear();
        }
        internal static string ReceiptPath(string year) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TYBIM", "McpClientSetup", year, "completed.json");

        internal static bool IsConfigured(string directory, string year, string receiptPath)
        {
            try
            {
                var receipt = JObject.Parse(File.ReadAllText(receiptPath));
                int? schema = (int?)receipt["schema"];
                if ((schema != 1 && schema != 2) || (string)receipt["year"] != year ||
                    !SamePath((string)receipt["installDirectory"],directory)) return false;
                var clients = receipt["clients"] as JArray;
                if (clients == null || clients.Count == 0) return false;
                var entry = JObject.Parse(File.ReadAllText(Path.Combine(directory,"external-ai.mcp.json")))
                    ["mcpServers"]?["tybim_revit_"+year+"_external"];
                if (entry == null) return false;
                foreach (var client in clients)
                {
                    string path=(string)client["path"], name=(string)client["client"];
                    // Old Desktop receipts validated only the legacy Roaming location.
                    if (name == "ClaudeDesktop" && schema == 1) return false;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
                    var text=File.ReadAllText(path);
                    if (name=="Codex")
                    {
                        string key="tybim_revit_"+year;
                        string table=Regex.Match(text,@"(?ms)^\[mcp_servers\."+Regex.Escape(key)+@"\]\s*\r?\n(.*?)(?=^\[|\z)").Value;
                        string env=Regex.Match(text,@"(?ms)^\[mcp_servers\."+Regex.Escape(key)+@"\.env\]\s*\r?\n(.*?)(?=^\[|\z)").Value;
                        if (!table.Contains(JsonConvert.SerializeObject((string)entry["command"])) ||
                            !table.Contains(JsonConvert.SerializeObject((string)entry["args"]?[0])) ||
                            !env.Contains(JsonConvert.SerializeObject((string)entry["env"]?["TYBIM_MCP_CONFIG"])) ||
                            !Regex.IsMatch(table,@"(?m)^enabled\s*=\s*true\s*$")) return false;
                    }
                    else if (name=="ClaudeCode" || name=="ClaudeDesktop")
                    {
                        var actual=JObject.Parse(text)["mcpServers"]?["tybim_revit_"+year+"_external"];
                        if(actual==null || !SamePath((string)actual["command"],(string)entry["command"]) ||
                            !SamePath((string)actual["args"]?[0],(string)entry["args"]?[0]) ||
                            !SamePath((string)actual["env"]?["TYBIM_MCP_CONFIG"],(string)entry["env"]?["TYBIM_MCP_CONFIG"])) return false;
                    }
                    else return false;
                }
                return true;
            }
            catch { return false; } // Missing/stale receipt means offer setup again, never stop the service.
        }
        private static bool SamePath(string a,string b) => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
            string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);

        internal static bool Launch(string directory,string year,bool force=false)
        {
            var receipt=ReceiptPath(year);
            if(Running.TryGetValue(directory,out var previous))
            {
                if(!previous.HasExited)
                {
                    if (force) ShowExisting(previous);
                    return true;
                }
                previous.Dispose();Running.Remove(directory);
            }
            if (!force && IsConfigured(directory,year,receipt)) return false;
            var script=Path.Combine(directory,"client-setup","Setup-ExternalAi.ps1");
            if(!File.Exists(script)) throw new FileNotFoundException("找不到 AI 連線設定精靈；請更新完整的 TYBIM MCP 安裝包。",script);
            var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var powerShell=Path.Combine(windows,"System32","WindowsPowerShell","v1.0","powershell.exe");
            // Launch a separate UI process; never block Revit's ExternalEvent queue.
            // Each argument is a controlled path/year; prohibit quotes before quoting.
            if(script.Contains("\"") || directory.Contains("\"")) throw new InvalidDataException("安裝路徑含不支援的引號。");
            var process=Process.Start(new ProcessStartInfo(powerShell,
                "-NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File \""+script+"\" -RevitVersion "+year+
                " -InstallDirectory \""+directory+"\"")
                { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(script) });
            if(process==null) throw new InvalidOperationException("無法啟動 AI 連線設定精靈。");
            if (process.WaitForExit(1000))
            {
                int exitCode = process.ExitCode;
                process.Dispose();
                if (exitCode != 0) throw new InvalidOperationException("AI 連線設定精靈啟動失敗，結束代碼：" + exitCode);
                return true;
            }
            Running.Add(directory,process);return true;
        }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class ExternalAiSetupCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            try { (RevitApplication.Current ?? throw new InvalidOperationException("TYBIM MCP 尚未啟動。")).OpenExternalAiSetup();return Result.Succeeded; }
            catch(Exception error) { message="AI 連線設定無法開啟："+error.Message;return Result.Failed; }
        }
    }
}
