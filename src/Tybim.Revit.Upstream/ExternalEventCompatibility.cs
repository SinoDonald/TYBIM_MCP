// TYBIM adapter, not the upstream RevitMCPSDK implementation.
// Our Dispatcher has already entered Revit's API thread. A nested external event
// followed by WaitOne would deadlock; execute the imported handler synchronously.
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace RevitMCPSDK.API.Interfaces
{
    public interface IWaitableExternalEventHandler
    {
        bool WaitForCompletion(int timeoutMilliseconds = 10000);
    }
}

namespace RevitMCPSDK.API.Base
{
    public abstract class ExternalEventCommandBase : IDisposable
    {
        protected IExternalEventHandler Handler { get; }
        private readonly UIApplication application;
        public abstract string CommandName { get; }
        protected ExternalEventCommandBase(IExternalEventHandler handler, UIApplication app)
        { Handler = handler; application = app; }
        public abstract object Execute(JObject parameters, string requestId);
        protected bool RaiseAndWaitForCompletion(int timeoutMilliseconds)
        { Handler.Execute(application); return true; }
        public void Dispose()
        {
            foreach (var field in Handler.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                if (field.GetValue(Handler) is System.Threading.WaitHandle handle) handle.Dispose();
        }
    }
}
