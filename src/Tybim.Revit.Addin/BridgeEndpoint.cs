using System;
using Tybim.Mcp.Contracts;

namespace Tybim.Revit.Addin
{
    // Each entry has its own queue and lifecycle. Revit executes both queues on
    // its UI thread, using the same command implementations and document guards.
    internal sealed class BridgeEndpoint : IDisposable
    {
        internal Dispatcher Dispatcher { get; } = new Dispatcher();
        private BridgeListener listener;
        internal bool Enabled => listener != null;

        internal void Enable(BridgeSettings settings)
        {
            if (Enabled) return;
            settings.Validate();
            var next = new BridgeListener(settings, Dispatcher);
            try
            {
                Dispatcher.SetEnabled(true);
                next.Start();
                listener = next;
            }
            catch { Dispatcher.SetEnabled(false); next.Dispose(); throw; }
        }

        internal void Disable()
        {
            Dispatcher.SetEnabled(false);
            var previous = listener;
            listener = null;
            // Return explicit refusal for queued work; allow an executing command
            // to finish with its real result rather than abandon its transaction.
            previous?.StopAccepting();
        }

        public void Dispose()
        {
            var previous = listener;
            Disable();
            previous?.Dispose();
            Dispatcher.Dispose();
        }
    }
}
