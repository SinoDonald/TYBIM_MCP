using System;
namespace Tybim.Revit.Upstream
{
    public static class UpstreamDiagnostics
    {
        // Imported handlers used blocking error dialogs and sometimes returned
        // empty/success results afterwards. Surface errors through MCP instead.
        public static void Report(string title, string message)
        { throw new InvalidOperationException(title + ": " + message); }
    }
}
