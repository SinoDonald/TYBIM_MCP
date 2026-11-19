using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Tybim.Mcp.Contracts;
using Tybim.Revit.Addin;

internal static class Program
{
    private static void Assert(bool pass,string text) {if(!pass) throw new Exception(text);Console.WriteLine("PASS: "+text);}
    private static BridgeRequest Request() => new BridgeRequest { Version=1,Id=Guid.NewGuid().ToString("N"),Token=new string('t',40),Method="counter",ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds() };
    private static async Task Main()
    {
        using(var dispatcher=new Dispatcher())
        {
            Assert((await dispatcher.Enqueue(Request())).Error=="MCP_SERVICE_DISABLED","startup loads registry without enabling requests");
            dispatcher.SetEnabled(true);
            var abandoned=dispatcher.Enqueue(Request()); dispatcher.SetEnabled(false); dispatcher.Execute(new UIApplication());
            Assert((await abandoned).Error=="MCP_SERVICE_DISABLED" && Tybim.Revit.Commands.CommandRegistry.Executed==0,"closing pane drains queued work without executing it");
            var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
            for(int cycle=0;cycle<3;cycle++)
            {
                dispatcher.SetEnabled(true);
                using(var listener=new BridgeListener(new BridgeSettings {Port=port,Token=new string('t',40)},dispatcher))
                {
                    listener.Start(); ExternalEvent.Raised.Reset();
                    using(var tcp=new TcpClient())
                    {
                        await tcp.ConnectAsync(IPAddress.Loopback,port);
                        var stream=tcp.GetStream();var writer=new StreamWriter(stream){AutoFlush=true};
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(Request()));
                        Assert(ExternalEvent.Raised.WaitOne(3000),"shown pane accepts loopback request cycle "+cycle);
                        dispatcher.Execute(new UIApplication());
                        var response=JsonConvert.DeserializeObject<BridgeResponse>(await new StreamReader(stream).ReadLineAsync());
                        Assert(response.Success,"enabled dispatcher returns result cycle "+cycle);
                    }
                    dispatcher.SetEnabled(false);
                }
                try {using(var tcp=new TcpClient()) await tcp.ConnectAsync(IPAddress.Loopback,port);throw new Exception("port remains listening");}
                catch(SocketException){Console.WriteLine("PASS: hidden pane closes loopback port cycle "+cycle);}
            }
            dispatcher.SetEnabled(true);
            using(var listener=new BridgeListener(new BridgeSettings {Port=port,Token=new string('t',40)},dispatcher))
            using(var tcp=new TcpClient())
            {
                listener.Start();ExternalEvent.Raised.Reset();await tcp.ConnectAsync(IPAddress.Loopback,port);
                var stream=tcp.GetStream();var writer=new StreamWriter(stream){AutoFlush=true};
                await writer.WriteLineAsync(JsonConvert.SerializeObject(Request()));
                Assert(ExternalEvent.Raised.WaitOne(3000),"queued TCP request reaches dispatcher before hide");
                dispatcher.SetEnabled(false);listener.StopAccepting();
                var refused=JsonConvert.DeserializeObject<BridgeResponse>(await new StreamReader(stream).ReadLineAsync());
                Assert(!refused.Success && refused.Error=="MCP_SERVICE_DISABLED","hiding drains a queued request with an explicit non-execution reply rather than dropping it");
                dispatcher.Execute(new UIApplication());
            }
            Assert(Tybim.Revit.Commands.CommandRegistry.Loaded==1 && Tybim.Revit.Commands.CommandRegistry.Executed==3,"registry loads once and survives repeated show/hide");
            dispatcher.SetEnabled(true);
            var blocked = dispatcher.Enqueue(Request());
            Assert((await blocked).Error == "REVIT_BUSY", "accepted ExternalEvent without Execute is refused promptly instead of expiring");
            dispatcher.Execute(new UIApplication());
            Assert(Tybim.Revit.Commands.CommandRegistry.Executed == 3, "busy request never executes later when Revit becomes idle");
            var recovered = dispatcher.Enqueue(Request()); dispatcher.Execute(new UIApplication());
            Assert((await recovered).Success, "fresh request succeeds after leaving the interactive command");
            var slow = Request(); slow.Method = "slow"; slow.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();
            var running = dispatcher.Enqueue(slow);
            var execution = Task.Run(() => dispatcher.Execute(new UIApplication()));
            Assert(Tybim.Revit.Commands.CommandRegistry.Started.WaitOne(3000), "long-running command starts");
            await Task.Delay(8500);
            bool preserved = !running.IsCompleted;
            Tybim.Revit.Commands.CommandRegistry.Release.Set(); await execution;
            Assert(preserved && (await running).Success, "watchdog never refuses or abandons an executing transaction");
        }
        await TestIndependentEndpoints();
        TestExternalConfiguration();
    }

    private static async Task TestIndependentEndpoints()
    {
        int FreePort() { var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop(); return port; }
        int firstPort = FreePort(), secondPort;
        do { secondPort = FreePort(); } while (secondPort == firstPort);
        var first = new BridgeSettings { Port = firstPort, Token = new string('a', 40) };
        var second = new BridgeSettings { Port = secondPort, Token = new string('b', 40) };
        using (var inside = new BridgeEndpoint())
        using (var outside = new BridgeEndpoint())
        {
            Assert(!inside.Enabled && !outside.Enabled, "both entries start disabled");
            inside.Enable(first); outside.Enable(second);
            var pendingInside = inside.Dispatcher.Enqueue(Request());
            var pendingOutside = outside.Dispatcher.Enqueue(Request());
            inside.Disable();
            Assert((await pendingInside).Error == "MCP_SERVICE_DISABLED", "internal close refuses only internal queue");
            outside.Dispatcher.Execute(new UIApplication());
            Assert((await pendingOutside).Success && outside.Enabled, "external request survives closing internal entry");
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(IPAddress.Loopback, secondPort);
                var stream = tcp.GetStream(); var writer = new StreamWriter(stream) { AutoFlush = true };
                ExternalEvent.Raised.Reset();
                var request = Request(); request.Token = second.Token;
                await writer.WriteLineAsync(JsonConvert.SerializeObject(request));
                Assert(ExternalEvent.Raised.WaitOne(3000), "external TCP entry receives request while internal entry is closed");
                outside.Dispatcher.Execute(new UIApplication());
                var response = JsonConvert.DeserializeObject<BridgeResponse>(await new StreamReader(stream).ReadLineAsync());
                Assert(response.Success, "external TCP entry returns command result while internal entry is closed");
            }
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(IPAddress.Loopback, secondPort);
                var stream = tcp.GetStream(); var writer = new StreamWriter(stream) { AutoFlush = true };
                var wrongToken = Request(); wrongToken.Token = first.Token;
                await writer.WriteLineAsync(JsonConvert.SerializeObject(wrongToken));
                var response = JsonConvert.DeserializeObject<BridgeResponse>(await new StreamReader(stream).ReadLineAsync());
                Assert(response.Error == "UNAUTHORIZED", "external entry refuses internal token");
            }
            inside.Enable(first);
            var pending = inside.Dispatcher.Enqueue(Request()); outside.Disable();
            inside.Dispatcher.Execute(new UIApplication());
            Assert((await pending).Success && inside.Enabled && !outside.Enabled, "external close leaves internal entry running");
            using (var occupied = new TcpListener(IPAddress.Loopback, secondPort))
            {
                occupied.Start();
                try { outside.Enable(second); throw new Exception("expected port conflict"); }
                catch (SocketException) { }
                Assert(!outside.Enabled && inside.Enabled, "port conflict does not disable the other entry");
            }
            outside.Enable(second);
            Assert(outside.Enabled, "external entry recovers after port conflict is resolved");
        }
    }

    private static void TestExternalConfiguration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tybim-external-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var internalSettings = new BridgeSettings { Port = 17825, Token = new string('i', 40) };
            var settings = ExternalAiConfiguration.Load(directory, "2025", internalSettings);
            Assert(settings.Port == 21825 && settings.Token != internalSettings.Token, "external default uses independent port 21825 and random token");
            Assert(ExternalAiConfiguration.Load(directory, "2025", internalSettings).Token == settings.Token, "external token persists across restarts");
            var configPath = Path.Combine(directory, "external.bridge.config.json");
            foreach (int forbidden in new[] { 8964, 17825 })
            {
                File.WriteAllText(configPath, JsonConvert.SerializeObject(new { port = forbidden, token = settings.Token }));
                try { ExternalAiConfiguration.Load(directory, "2025", internalSettings); throw new Exception("accepted forbidden port"); }
                catch (InvalidDataException) { Console.WriteLine("PASS: forbidden/colliding external port rejected"); }
            }
            File.WriteAllText(Path.Combine(directory, "chat.config.json"), JsonConvert.SerializeObject(new { nodeExecutable = "node.exe" }));
            File.WriteAllText(Path.Combine(directory, "node.exe"), "test");
            Directory.CreateDirectory(Path.Combine(directory, "runtime", "build"));
            File.WriteAllText(Path.Combine(directory, "runtime", "build", "index.js"), "test");
            ExternalAiConfiguration.ExportClient(directory, "2025");
            var client = File.ReadAllText(Path.Combine(directory, "external-ai.mcp.json"));
            Assert(client.Contains("external.bridge.config.json") && client.Contains("tybim_revit_2025_external") && !client.Contains(settings.Token), "client export points to external config without embedding token");
        }
        finally { Directory.Delete(directory, true); }
    }
}
namespace Autodesk.Revit.UI
{
    public sealed class UIApplication { }
    public interface IExternalEventHandler {void Execute(UIApplication app);string GetName();}
    public enum ExternalEventRequest {Accepted,Pending}
    public sealed class ExternalEvent : IDisposable
    {
        public static readonly AutoResetEvent Raised=new AutoResetEvent(false);
        public static ExternalEvent Create(IExternalEventHandler handler)=>new ExternalEvent();
        public ExternalEventRequest Raise(){Raised.Set();return ExternalEventRequest.Accepted;}
        public void Dispose() { }
    }
}
namespace Autodesk.Revit.UI.Events { public sealed class IdlingEventArgs : EventArgs { } }
namespace Tybim.Revit.Commands
{
    public sealed class CommandRegistry
    {
        public static int Loaded,Executed;
        public static readonly AutoResetEvent Started=new AutoResetEvent(false), Release=new AutoResetEvent(false);
        public static CommandRegistry CreateDefault(){Loaded++;return new CommandRegistry();}
        public object Execute(string method,UIApplication app,Newtonsoft.Json.Linq.JObject p){Executed++;if(method=="slow"){Started.Set();Release.WaitOne(15000);}return new{count=Executed};}
    }
}
