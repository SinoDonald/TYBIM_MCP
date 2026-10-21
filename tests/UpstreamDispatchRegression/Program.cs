using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPSDK.API.Interfaces;
using Autodesk.Revit.UI;

var handler = new Handler();
using (var command = new Command(handler))
{
    command.Execute(new JObject(), "request");
    Check(handler.Count == 1, "imported handler executes exactly once");
    Check(handler.Thread == Environment.CurrentManagedThreadId, "handler stays on the already-entered Revit API thread");
    Check(!handler.Waited, "adapter never blocks the Revit thread on another external event");
}
Check(handler.HandleDisposed, "imported wait handle is disposed after the command");
using (var command = new Command(new Handler { Fail = true }))
{
    bool propagated = false;
    try { command.Execute(new JObject(), "request"); } catch (InvalidOperationException ex) { propagated = ex.Message == "native error"; }
    Check(propagated, "native handler errors reach the outer MCP transaction wrapper");
}
static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS: " + label); }

class Command : ExternalEventCommandBase
{
    public Command(Handler handler) : base(handler, new UIApplication()) { }
    public override string CommandName => "regression";
    public override object Execute(JObject parameters, string requestId) => RaiseAndWaitForCompletion(10);
}
class Handler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent handle = new(false);
    public int Count, Thread;
    public bool Waited, Fail;
    public bool HandleDisposed { get { try { handle.Set(); return false; } catch (ObjectDisposedException) { return true; } } }
    public void Execute(UIApplication app) { Count++; Thread = Environment.CurrentManagedThreadId; if (Fail) throw new InvalidOperationException("native error"); }
    public string GetName() => "regression";
    public bool WaitForCompletion(int timeoutMilliseconds = 10000) { Waited = true; throw new Exception("must not wait"); }
}
namespace Autodesk.Revit.UI
{
    // Only the queue dispatch contract is simulated here; not Revit geometry.
    public class UIApplication { }
    public interface IExternalEventHandler { void Execute(UIApplication app); string GetName(); }
}
