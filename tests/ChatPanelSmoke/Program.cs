using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Tybim.Revit.Addin.Chat;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : Environment.CurrentDirectory);
        var state = Path.Combine(root, "artifacts", "qa", "chat-panel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(state);
        var preferences = new ChatPreferences();
        const string testKey = "test-only-not-a-real-api-key-樓層";
        preferences.SetKey("claude", testKey);
        preferences.Save(state);
        Assert(!File.ReadAllText(Path.Combine(state, "ui.settings.json")).Contains(testKey), "API key is not stored as plaintext");
        Assert(ChatPreferences.Load(state).Key("claude") == testKey, "DPAPI credential roundtrip");
        File.WriteAllText(Path.Combine(state, "runtime.js"), "// UI smoke only: never started");
        File.WriteAllText(Path.Combine(state, "bridge.config.json"), "{}");
        var config = Path.Combine(state, "chat.config.json");
        File.WriteAllText(config, JsonConvert.SerializeObject(new ChatLaunchSettings
        {
            NodeExecutable = Path.GetRelativePath(state, @"C:\Program Files\nodejs\node.exe"), RuntimeScript = "runtime.js", StateDirectory = state
        }));
        Assert(ChatLaunchSettings.Load(config).NodeExecutable == @"C:\Program Files\nodejs\node.exe", "portable Node path resolves relative to installed chat.config.json");
        File.WriteAllText(Path.Combine(state, "runtime.js"), @"
const readline = require('node:readline');
console.log(JSON.stringify({event:'ready'}));
const reader = readline.createInterface({input:process.stdin});
reader.on('line', line => {
  const request = JSON.parse(line);
  if (request.method === 'shutdown') process.exit(0);
  console.log(JSON.stringify({id:request.id,result:{received:request.params.text}}));
});
");
        using (var transport = new ChatProcessClient())
        {
            transport.Start(ChatLaunchSettings.Load(config)).GetAwaiter().GetResult();
            const string unicode = "跨程序繁體中文／牆長 2/3 🚪";
            var reply = transport.Request("echo", new { text = unicode }).GetAwaiter().GetResult();
            Assert(reply.Value<string>("received") == unicode, "C# to Node JSONL preserves UTF-8 including Traditional Chinese");
        }
        using (var panel = new ChatPanel(config))
        {
            var size = new Size(440, 960);
            panel.Measure(size); panel.Arrange(new Rect(size)); panel.UpdateLayout();
            var controls = Descendants(panel).ToArray();
            var combos = controls.OfType<ComboBox>().ToArray();
            Assert(combos[0].Items.Count == 3, "three AI providers are available");
            Assert(!controls.OfType<PasswordBox>().Single().IsEnabled, "Codex uses login rather than API key");
            combos[0].SelectedIndex = 1;
            Assert(controls.OfType<PasswordBox>().Single().IsEnabled, "Claude enables API key field");
            Assert(controls.OfType<PasswordBox>().Single().Password == testKey, "provider switch restores encrypted key");
            combos[0].SelectedIndex = 2;
            Assert(controls.OfType<PasswordBox>().Single().Password == "", "provider keys remain isolated");
            combos[0].SelectedIndex = 0;
            var approvalMessage = Newtonsoft.Json.Linq.JObject.FromObject(new {
                id = "smoke-approval", preview = new { kind = "upstream", title = "測試專案", tool = "revit_upstream_send_code_to_revit",
                    note = "參數確認，尚未試放", arguments = new { documentId = "doc", code = "return 1;" } }
            });
            typeof(ChatPanel).GetMethod("ShowApproval", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(panel, new object[] { approvalMessage });
            panel.Measure(size); panel.Arrange(new Rect(size));
            panel.UpdateLayout();
            Assert(Descendants(panel).OfType<TextBox>().Any(box => box.IsReadOnly && box.Text.Contains("return 1;")), "upstream confirmation displays complete code arguments");
            Assert(Descendants(panel).OfType<TextBlock>().Any(block => block.Text.Contains("revit_upstream_send_code_to_revit") && block.Text.Contains("測試專案")), "upstream confirmation identifies tool and project");
            var screenshot = new RenderTargetBitmap(80, 40, 96, 96, PixelFormats.Pbgra32);
            screenshot.Render(panel);
            var screenshotEncoder = new PngBitmapEncoder(); screenshotEncoder.Frames.Add(BitmapFrame.Create(screenshot));
            string base64;
            using (var memory = new MemoryStream()) { screenshotEncoder.Save(memory); base64 = Convert.ToBase64String(memory.ToArray()); }
            var imageMessage = Newtonsoft.Json.Linq.JObject.FromObject(new {
                name = "revit_study_capture_view_image", result = new { content = new object[] {
                    new { type = "text", text = "capture metadata" }, new { type = "image", data = base64, mimeType = "image/png" } } }
            });
            typeof(ChatPanel).GetMethod("AddToolResult", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(panel, new object[] { imageMessage });
            var imageResult = Descendants(panel).OfType<Expander>().Last(); imageResult.IsExpanded = true;
            panel.Measure(size); panel.Arrange(new Rect(size)); panel.UpdateLayout();
            Assert(Descendants(imageResult).OfType<Image>().Any(imageControl => imageControl.Source != null), "Study image result renders as WPF image");
            Assert(!Descendants(imageResult).OfType<TextBox>().Any(box => box.Text.Contains(base64)), "image base64 is not dumped into result text");
            var bitmap = new RenderTargetBitmap(440, 960, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(panel);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var image = Path.Combine(root, "artifacts", "chat-panel-upstream-preview.png");
            using (var stream = File.Create(image)) encoder.Save(stream);
            Console.WriteLine("UI preview: " + image);
        }
    }
    private static void Assert(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); Console.WriteLine("PASS: " + description); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject item)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++)
        {
            var child = VisualTreeHelper.GetChild(item, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
