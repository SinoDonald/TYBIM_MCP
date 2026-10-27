using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.DB.Events;
using Newtonsoft.Json;
using Tybim.Mcp.Contracts;
using Tybim.Revit.Addin.Chat;

namespace Tybim.Revit.Addin
{
    internal sealed class RevitApplication : IExternalApplication
    {
        internal static RevitApplication Current { get; private set; }
        private BridgeSettings bridgeSettings;
        private Dispatcher dispatcher;
        private BridgeListener listener;
        private ChatDockablePane chat;
        private RevitMCP.Application study;
        private UIControlledApplication uiHost;
        private int startupLayoutAttempts;
        private UIApplication startupUiApp;
        private bool startupLayoutDirty = true;
        private bool aiPaneInitialized;
        private DateTime nextLayoutAttempt;
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                uiHost = app;
                var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var bridgePath = Path.Combine(directory, "bridge.config.json");
                if (!File.Exists(bridgePath))
                {
                    // Per-user MSI installs never contain a shared developer token.
                    var legacyBridge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "Addins", app.ControlledApplication.VersionNumber, "Tybim.Mcp", "bridge.config.json");
                    var bytes = new byte[32];
                    using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(bytes);
                    var fresh = new BridgeSettings { Port = 15800 + int.Parse(app.ControlledApplication.VersionNumber), Token = Convert.ToBase64String(bytes) };
                    try
                    {
                        using (var file = new FileStream(bridgePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (var writer = new StreamWriter(file)) writer.Write(File.Exists(legacyBridge)
                            ? File.ReadAllText(legacyBridge)
                            : JsonConvert.SerializeObject(new { port = fresh.Port, token = fresh.Token }));
                    }
                    catch (IOException) when (File.Exists(bridgePath)) { /* Another instance created it first. */ }
                }
                var settings = JsonConvert.DeserializeObject<BridgeSettings>(File.ReadAllText(bridgePath));
                if (settings == null) throw new InvalidOperationException("Missing bridge settings.");
                settings.Validate();
                dispatcher = new Dispatcher(); // Created in a valid Revit API context.
                bridgeSettings = settings;
                app.Idling += dispatcher.OnIdling;
                chat = new ChatDockablePane(Path.Combine(directory, "chat.config.json"));
                app.RegisterDockablePane(ChatDockablePane.Id, "TYBIM AI 助手", chat);
                Current = this;
                app.DockableFrameVisibilityChanged += OnPaneVisibilityChanged;
                RibbonUi.Register(app);
                study = new RevitMCP.Application();
                study.CameraPaneShown += OnStartupCameraPaneShown;
                study.OnStartup(app);
                app.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
                app.ViewActivated += OnStartupViewActivated;
                // Remain armed for late layout restoration until the user opens
                // the camera command. Idle performs no pane calls unless dirty.
                app.Idling += ApplyStartupPaneLayout;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Current = null;
                app.DockableFrameVisibilityChanged -= OnPaneVisibilityChanged;
                DetachStartupLayout();
                try { study?.OnShutdown(app); } catch { }
                chat?.Panel.Dispose();
                if (dispatcher != null) app.Idling -= dispatcher.OnIdling;
                listener?.Dispose(); dispatcher?.Dispose();
                // Preserve the complete exception chain for startup diagnostics;
                // TypeInitializationException.Message alone hides the real cause.
                string logPath = null;
                try
                {
                    var logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TYBIM", "McpLogs");
                    Directory.CreateDirectory(logs);
                    logPath = Path.Combine(logs, "startup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                    File.WriteAllText(logPath, ex.ToString());
                }
                catch { /* Diagnostics must not mask the original startup failure. */ }
                TaskDialog.Show("TYBIM MCP", "TYBIM MCP failed to start: " + ex.GetBaseException().Message
                    + (logPath == null ? "" : "\n\nDetails: " + logPath));
                return Result.Failed;
            }
        }
        private void OnApplicationInitialized(object sender, ApplicationInitializedEventArgs args)
        {
            if (sender is Autodesk.Revit.ApplicationServices.Application application)
                startupUiApp = new UIApplication(application);
            startupLayoutDirty = true;
            try { if (startupUiApp != null) SyncPaneService(startupUiApp.GetDockablePane(ChatDockablePane.Id).IsShown()); }
            catch (Exception error) { LogPaneLayout("AI pane not ready: " + error.Message); }
        }
        private void OnPaneVisibilityChanged(object sender, DockableFrameVisibilityChangedEventArgs args)
        {
            if (args.PaneId.Guid == ChatDockablePane.Id.Guid) SyncPaneService(args.DockableFrameShown);
        }
        private void SyncPaneService(bool shown)
        {
            try { SetBridgeEnabled(shown); }
            catch (Exception error) { chat?.Panel.NotifyServiceState(false, "MCP 服務啟動失敗：" + error.Message); }
        }
        internal void SetBridgeEnabled(bool enabled)
        {
            if (!enabled)
            {
                dispatcher?.SetEnabled(false);
                var previous = listener; listener = null; previous?.StopAccepting();
                chat?.Panel.NotifyServiceState(false);
                return;
            }
            if (listener != null) return;
            var next = new BridgeListener(bridgeSettings, dispatcher);
            try
            {
                dispatcher.SetEnabled(true);
                next.Start(); listener = next;
                chat?.Panel.NotifyServiceState(true);
            }
            catch { dispatcher.SetEnabled(false); next.Dispose(); throw; }
        }
        private void OnStartupViewActivated(object sender, ViewActivatedEventArgs args)
        {
            if (sender is UIApplication uiApp) startupUiApp = uiApp;
            startupLayoutDirty = true;
        }
        private void OnStartupCameraPaneShown() { startupLayoutDirty = true; }
        private void DetachStartupLayout()
        {
            if (uiHost == null) return;
            uiHost.Idling -= ApplyStartupPaneLayout;
            uiHost.ViewActivated -= OnStartupViewActivated;
            uiHost.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
            if (study != null) study.CameraPaneShown -= OnStartupCameraPaneShown;
        }
        private static void LogPaneLayout(string text)
        {
            try
            {
                var logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TYBIM", "McpLogs");
                Directory.CreateDirectory(logs);
                File.AppendAllText(Path.Combine(logs, "pane-layout.log"), DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine);
            }
            catch { }
        }
        private void ApplyStartupPaneLayout(object sender, IdlingEventArgs args)
        {
            if (RevitMCP.Application.CameraPaneRequested)
            {
                DetachStartupLayout();
                LogPaneLayout("User opened camera manager; startup visibility guard released.");
                return;
            }
            if (!startupLayoutDirty || DateTime.UtcNow < nextLayoutAttempt) return;
            if (sender is UIApplication uiApp) startupUiApp = uiApp;
            else if (sender is Autodesk.Revit.ApplicationServices.Application application) startupUiApp = new UIApplication(application);
            if (startupUiApp == null) return;
            try
            {
                var camera = startupUiApp.GetDockablePane(RevitMCP.Application.CameraPaneId);
                bool wasShown = camera.IsShown();
                if (wasShown) camera.Hide();
                if (!aiPaneInitialized)
                {
                    startupUiApp.GetDockablePane(ChatDockablePane.Id).Show();
                    aiPaneInitialized = true;
                }
                SyncPaneService(startupUiApp.GetDockablePane(ChatDockablePane.Id).IsShown());
                bool stillShown = camera.IsShown();
                startupLayoutDirty = stillShown;
                startupLayoutAttempts = 0;
                LogPaneLayout("Camera shown before=" + wasShown + ", after=" + stillShown + "; AI initialized=" + aiPaneInitialized);
                nextLayoutAttempt = DateTime.UtcNow.AddSeconds(1);
            }
            catch (Exception ex)
            {
                // Keep waiting for pane creation; do not abandon setup after
                // three early idle callbacks before Revit finishes restoring UI.
                nextLayoutAttempt = DateTime.UtcNow.AddSeconds(1);
                if (++startupLayoutAttempts >= 3)
                {
                    LogPaneLayout("Layout not ready: " + ex);
                    startupLayoutAttempts = 0;
                    nextLayoutAttempt = DateTime.UtcNow.AddSeconds(10);
                }
            }
        }
        public Result OnShutdown(UIControlledApplication app)
        {
            app.DockableFrameVisibilityChanged -= OnPaneVisibilityChanged;
            var shutdownListener = listener;
            SetBridgeEnabled(false); shutdownListener?.Dispose(); Current = null;
            DetachStartupLayout();
            if (dispatcher != null) app.Idling -= dispatcher.OnIdling;
            chat?.Panel.Dispose();
            study?.OnShutdown(app);
            listener?.Dispose(); dispatcher?.Dispose(); return Result.Succeeded;
        }
    }
}
