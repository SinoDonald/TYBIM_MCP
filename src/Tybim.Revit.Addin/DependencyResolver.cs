using System;
using System.IO;
using System.Reflection;
#if REVIT2025_OR_GREATER
using System.Runtime.Loader;
#endif

namespace Tybim.Revit.Addin
{
    // Resolve only dependencies requested by this add-in or its sibling DLLs.
    // Never search other years, the working directory, or replace Revit APIs.
    internal static class DependencyResolver
    {
        private static readonly Assembly Entry = typeof(DependencyResolver).Assembly;
        private static readonly string Folder = Path.GetDirectoryName(Entry.Location);
        private static bool installed;
#if REVIT2025_OR_GREATER
        private static readonly AssemblyLoadContext Context = AssemblyLoadContext.GetLoadContext(Entry);
#endif
        internal static void Install()
        {
            if (installed) return;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
#if REVIT2025_OR_GREATER
            if (Context != AssemblyLoadContext.Default) Context.Resolving += ResolveContext;
#endif
            installed = true;
        }

        internal static void Uninstall()
        {
            if (!installed) return;
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
#if REVIT2025_OR_GREATER
            if (Context != AssemblyLoadContext.Default) Context.Resolving -= ResolveContext;
#endif
            installed = false;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var requester = args.RequestingAssembly;
            if (requester == null || string.IsNullOrEmpty(requester.Location)
                || !string.Equals(Path.GetDirectoryName(requester.Location), Folder, StringComparison.OrdinalIgnoreCase)) return null;
            return LoadSibling(new AssemblyName(args.Name));
        }

#if REVIT2025_OR_GREATER
        private static Assembly ResolveContext(AssemblyLoadContext context, AssemblyName name) => LoadSibling(name);
#endif
        private static Assembly LoadSibling(AssemblyName requested)
        {
            var name = requested.Name;
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0
                || name.StartsWith("RevitAPI", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
            var path = Path.Combine(Folder, name + ".dll");
            if (!File.Exists(path)) return null;
            var actual = AssemblyName.GetAssemblyName(path);
            // Do not silently bind to a stale DLL from another release.
            if (!string.Equals(actual.FullName, requested.FullName, StringComparison.OrdinalIgnoreCase)) return null;
#if REVIT2025_OR_GREATER
            return Context.LoadFromAssemblyPath(path);
#else
            return Assembly.LoadFrom(path);
#endif
        }
    }
}
