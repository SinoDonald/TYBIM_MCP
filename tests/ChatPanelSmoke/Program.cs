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
        try { RunChecks(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetType().FullName + ": " + error.Message);
            Environment.ExitCode = 1;
        }
    }
    private static void RunChecks(string[] args)
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
#if REVIT2024
            NodeExecutable = Uri.UnescapeDataString(new Uri(state + Path.DirectorySeparatorChar).MakeRelativeUri(new Uri(@"C:\Program Files\nodejs\node.exe")).ToString()),
#else
            NodeExecutable = Path.GetRelativePath(state, @"C:\Program Files\nodejs\node.exe"),
#endif
            RuntimeScript = "runtime.js", StateDirectory = state
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
        var openedUrls = new List<string>();
        using (var panel = new ChatPanel(config, url => openedUrls.Add(url)))
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
            Assert(!combos[1].IsEditable, "Claude model selector only allows the filtered model list");
            combos[0].SelectedIndex = 2;
            Assert(controls.OfType<PasswordBox>().Single().Password == "", "provider keys remain isolated");
            Assert(!combos[1].IsEditable, "Gemini model selector only allows the filtered model list");
            combos[0].SelectedIndex = 0;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var pendingAnswer = (TextBox)typeof(ChatPanel).GetMethod("AddMessage", flags).Invoke(panel, new object[] { "AI", "等待 AI 回應…" });
            typeof(ChatPanel).GetField("currentAnswer", flags).SetValue(panel, pendingAnswer);
            typeof(ChatPanel).GetField("answerPending", flags).SetValue(panel, true);
            var handleEvent = typeof(ChatPanel).GetMethod("HandleEvent", flags);
            var loginButton = (Button)typeof(ChatPanel).GetField("login", flags).GetValue(panel);
            var reopenButton = (Button)typeof(ChatPanel).GetField("reopenLogin", flags).GetValue(panel);
            var messageList = (StackPanel)typeof(ChatPanel).GetField("messages", flags).GetValue(panel);
            int originalMessages = messageList.Children.Count;
            var loginEvent = Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "loginUrl", url = "https://auth.openai.com/test-login?state=ui-test" });
            for (int repeat = 0; repeat < 20; repeat++) handleEvent.Invoke(panel, new object[] { loginEvent });
            Assert(openedUrls.Count == 1, "duplicate login URL events open the browser only once");
            Assert(messageList.Children.Count == originalMessages && reopenButton.Visibility == Visibility.Visible,
                "login URL uses one fixed button without appending conversation entries");
            int bubbledClicks = 0;
            panel.AddHandler(Button.ClickEvent, new RoutedEventHandler((_, __) => bubbledClicks++));
            for (int repeat = 0; repeat < 20; repeat++)
            {
                loginButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                reopenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Assert(openedUrls.Count == 41 && messageList.Children.Count == originalMessages,
                "repeated login and reopen clicks reuse the current login URL without new buttons or runtime requests");
            Assert(bubbledClicks == 0, "login clicks are handled inside the panel and do not bubble to Revit");
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "loginUrl", url = "https://auth.openai.com/new-login?state=ui-test" }) });
            reopenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(openedUrls.Last().Contains("new-login") && messageList.Children.Count == originalMessages,
                "a new login session replaces the URL on the same button");
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "loginCompleted", success = true }) });
            Assert(reopenButton.Visibility == Visibility.Collapsed && typeof(ChatPanel).GetField("loginUrl", flags).GetValue(panel) == null,
                "completed login removes the stale login link");
            handleEvent.Invoke(panel, new object[] { loginEvent });
            combos[0].SelectedIndex = 1;
            int browserCount = openedUrls.Count;
            handleEvent.Invoke(panel, new object[] { loginEvent });
            Assert(reopenButton.Visibility == Visibility.Collapsed && openedUrls.Count == browserCount,
                "provider switch clears Codex login and ignores late login URLs");
            combos[0].SelectedIndex = 0;
            handleEvent.Invoke(panel, new object[] { loginEvent });
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "disconnected", text = "UI test disconnect" }) });
            Assert(reopenButton.Visibility == Visibility.Collapsed, "runtime disconnect removes the stale login link");
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "status", text = "等待 Gemini 回應…" }) });
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "textDelta", text = "" }) });
            Assert(pendingAnswer.Text == "等待 Gemini 回應…", "waiting status stays visible until actual answer text arrives");
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "toolStarted", name = "revit_project_info" }) });
            Assert(pendingAnswer.Text.Contains("revit_project_info"), "pending answer identifies the running Revit tool");
            var turnWatch = (System.Diagnostics.Stopwatch)typeof(ChatPanel).GetField("turnWatch", flags).GetValue(panel);
            turnWatch.Start();
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "status", text = "等待 Gemini 回應…" }) });
            Assert(pendingAnswer.Text.Contains("本回合") && pendingAnswer.Text.Contains("秒"), "waiting progress includes elapsed time without inventing model output");
            turnWatch.Stop();
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "textDelta", text = "房間共 3 間" }) });
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "status", text = "後續狀態" }) });
            Assert(pendingAnswer.Text == "房間共 3 間", "real answer replaces placeholder and subsequent status cannot overwrite it");
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
            var longApproval = Newtonsoft.Json.Linq.JObject.FromObject(new {
                id = "long-approval", preview = new { kind = "upstream", title = "確認版面測試", tool = "revit_study_select_element",
                    note = "以下為本次操作參數，尚未執行。請核對元素 ID、尺寸、範圍或完整程式碼後確認。",
                    arguments = new { documentId = "layout-test-only", elementIds = Enumerable.Range(669574, 300).ToArray() } }
            });
            typeof(ChatPanel).GetMethod("ShowApproval", flags).Invoke(panel, new object[] { longApproval });
            var host = new Border { Child = panel };
            var approveButton = Descendants(panel).OfType<Button>().Single(button => (string)button.Content == "執行此計畫");
            var declineButton = Descendants(panel).OfType<Button>().Single(button => (string)button.Content == "取消");
            var sendButton = (Button)typeof(ChatPanel).GetField("send", flags).GetValue(panel);
            var stopButton = (Button)typeof(ChatPanel).GetField("stop", flags).GetValue(panel);
            var footerScroll = Ancestor<ScrollViewer>(sendButton);
            var bodyScroll = (ScrollViewer)typeof(ChatPanel).GetField("scroll", flags).GetValue(panel);
            foreach (var scenario in new[] {
                new[] {300.0, 820, 1}, new[] {300.0, 600, 1}, new[] {260.0, 400, 1},
                new[] {300.0, 240, 1}, new[] {450.0, 900, 1.5}, new[] {600.0, 1000, 2}
            })
            {
                var viewport = new Size(scenario[0], scenario[1]);
                panel.LayoutTransform = new ScaleTransform(scenario[2], scenario[2]);
                host.Measure(viewport); host.Arrange(new Rect(viewport)); host.UpdateLayout();
                bodyScroll.ScrollToEnd(); footerScroll.ScrollToTop(); host.UpdateLayout();
                var label = scenario[0] + "x" + scenario[1] + " scale=" + scenario[2];
                Assert(Within(approveButton, host) && Within(declineButton, host) && Within(approveButton, footerScroll) && Within(declineButton, footerScroll),
                    "approval execute/cancel fully inside the visible viewport: " + label);
                Assert(bodyScroll.ScrollableHeight > 0 && Descendants(panel).OfType<TextBox>().Any(box => box.Text.Contains("669873")),
                    "all 300 element IDs remain available in scrollable details: " + label);
                if (scenario[0] == 300 && scenario[1] == 820)
                {
                    var preview = new RenderTargetBitmap(300, 820, 96, 96, PixelFormats.Pbgra32); preview.Render(host);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(preview));
                    using (var stream = File.Create(Path.Combine(root, "artifacts", "chat-panel-confirmation-300x820.png"))) png.Save(stream);
                }
                footerScroll.ScrollToEnd(); host.UpdateLayout();
                Assert(Within(sendButton, host) && Within(stopButton, host) && Within(sendButton, footerScroll) && Within(stopButton, footerScroll),
                    "send/stop remain reachable by scrolling the short footer: " + label);
            }
            handleEvent.Invoke(panel, new object[] { Newtonsoft.Json.Linq.JObject.FromObject(new { @event = "approvalClosed", id = "long-approval" }) });
            Assert(!LayoutVisible(approveButton) && !LayoutVisible(declineButton), "closing approval hides both the details and fixed actions");
            host.Child = null; panel.LayoutTransform = null;
            panel.Measure(size); panel.Arrange(new Rect(size)); panel.UpdateLayout();
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
    private static T Ancestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(element); current != null; current = VisualTreeHelper.GetParent(current))
            if (current is T match) return match;
        throw new InvalidOperationException("Missing layout ancestor " + typeof(T).Name);
    }
    private static bool Within(FrameworkElement element, FrameworkElement viewport)
    {
        var bounds = element.TransformToAncestor(viewport).TransformBounds(new Rect(element.RenderSize));
        bool inside = LayoutVisible(element) && bounds.Width > 0 && bounds.Height > 0 && bounds.Left >= -1 && bounds.Top >= -1
            && bounds.Right <= viewport.ActualWidth + 1 && bounds.Bottom <= viewport.ActualHeight + 1;
        if (!inside) Console.WriteLine("LAYOUT: " + (element as Button)?.Content + " visible=" + element.IsVisible + " bounds=" + bounds + " viewport=" + viewport.RenderSize);
        return inside;
    }
    private static bool LayoutVisible(DependencyObject element)
    {
        // Offscreen render tests have no PresentationSource, so IsVisible is
        // false even for arranged controls. Check the actual visibility chain.
        for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject item)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++)
        {
            var child = VisualTreeHelper.GetChild(item, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
