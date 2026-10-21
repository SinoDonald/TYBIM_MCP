using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Tybim.Mcp.Contracts;
using Tybim.Revit.Commands;

namespace Tybim.Revit.Addin
{
    internal sealed class Dispatcher : IExternalEventHandler, IDisposable
    {
        private sealed class Work
        {
            public BridgeRequest Request;
            public TaskCompletionSource<BridgeResponse> Completion = new TaskCompletionSource<BridgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private readonly ConcurrentQueue<Work> queue = new ConcurrentQueue<Work>();
        private readonly CommandRegistry commands = CommandRegistry.CreateDefault();
        private readonly object gate = new object();
        private readonly ExternalEvent externalEvent;
        private bool stopped;
        public Dispatcher() { externalEvent = ExternalEvent.Create(this); }

        public Task<BridgeResponse> Enqueue(BridgeRequest request)
        {
            lock (gate)
            {
                if (stopped) return Task.FromResult(BridgeResponse.Fail(request.Id, "SHUTTING_DOWN"));
                if (queue.Count >= 64) return Task.FromResult(BridgeResponse.Fail(request.Id, "QUEUE_FULL"));
                var work = new Work { Request = request };
                queue.Enqueue(work);
                var status = externalEvent.Raise();
                if (status != ExternalEventRequest.Accepted && status != ExternalEventRequest.Pending)
                    while (queue.TryDequeue(out var rejected)) rejected.Completion.TrySetResult(BridgeResponse.Fail(rejected.Request.Id, "REVIT_BUSY"));
                return work.Completion.Task;
            }
        }
        public void Execute(UIApplication app)
        {
                // Same lock protects the queue; Idling also re-signals any pending work.
            lock (gate)
            {
                while (queue.TryDequeue(out var work))
                {
                    var request = work.Request;
                    try
                    {
                        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= request.ExpiresAt)
                            work.Completion.TrySetResult(BridgeResponse.Fail(request.Id, "REQUEST_EXPIRED"));
                        else work.Completion.TrySetResult(new BridgeResponse { Id = request.Id, Success = true,
                            Data = commands.Execute(request.Method, app, request.Parameters) });
                    }
                    catch (Exception ex) { work.Completion.TrySetResult(BridgeResponse.Fail(request.Id, ex.Message)); }
                }
            }
        }
        public string GetName() => "TYBIM MCP command queue";
        public void OnIdling(object sender, IdlingEventArgs args)
        {
            lock (gate)
                if (!stopped && !queue.IsEmpty) externalEvent.Raise();
        }
        public void Dispose()
        {
            lock (gate)
            {
                stopped = true;
                while (queue.TryDequeue(out var work)) work.Completion.TrySetResult(BridgeResponse.Fail(work.Request.Id, "SHUTTING_DOWN"));
                externalEvent.Dispose();
            }
        }
    }
}
