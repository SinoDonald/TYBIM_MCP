using System;
using System.IO;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI;

namespace Tybim.Revit.Addin
{
    // Keep the manifest entry point free of Contracts/JSON/Study fields and
    // signatures: Revit may JIT OnStartup before it can execute a try/catch.
    public sealed class Application : IExternalApplication
    {
        private IExternalApplication implementation;

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                DependencyResolver.Install();
                implementation = CreateImplementation();
                var result = implementation.OnStartup(app);
                if (result != Result.Succeeded) DependencyResolver.Uninstall();
                return result;
            }
            catch (Exception error)
            {
                DependencyResolver.Uninstall();
                string detail = error.ToString();
                try
                {
                    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TYBIM", "McpLogs");
                    Directory.CreateDirectory(folder);
                    var path = Path.Combine(folder, "bootstrap-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                    File.WriteAllText(path, detail);
                    detail = error.GetBaseException().Message + "\n\nDetails: " + path;
                }
                catch { }
                TaskDialog.Show("TYBIM MCP", "TYBIM MCP failed to load dependencies: " + detail);
                return Result.Failed;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IExternalApplication CreateImplementation() => new RevitApplication();

        public Result OnShutdown(UIControlledApplication app)
        {
            try { return implementation == null ? Result.Succeeded : implementation.OnShutdown(app); }
            finally { implementation = null; DependencyResolver.Uninstall(); }
        }
    }
}
