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
        }
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
        public static CommandRegistry CreateDefault(){Loaded++;return new CommandRegistry();}
        public object Execute(string method,UIApplication app,Newtonsoft.Json.Linq.JObject p){Executed++;return new{count=Executed};}
    }
}
