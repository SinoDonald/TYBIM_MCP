using System;
using System.IO;
using System.Reflection;

// No project references: dependency DLLs must stay outside the host's app base.
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var folder = Path.GetFullPath(args[0]);
            bool baseline = args.Length > 1 && args[1] == "baseline";
            var entryPath = Path.Combine(folder, "Tybim.Revit.Addin.dll");
            var entry = args.Length > 1 && args[1] == "loadfrom" ? Assembly.LoadFrom(entryPath) : Assembly.LoadFile(entryPath);
            if (!baseline)
            {
                // Exercise the production resolver without starting Revit,
                // listeners, panes, or model transactions. Revit API assemblies
                // contain native dependencies and require a real Revit process.
                entry.GetType("Tybim.Revit.Addin.DependencyResolver", true)
                    .GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
            var fixture = Assembly.LoadFile(Path.Combine(folder, "LoadFixture.dll"));
            var request = fixture.GetType("LoadFixture", true).GetMethod("CreateRequest").Invoke(null, null);
            if (baseline) throw new Exception("Expected the old LoadFile startup to fail, but it passed.");
            foreach (var file in Directory.GetFiles(folder, "Tybim*.dll"))
            {
                var identity = AssemblyName.GetAssemblyName(file);
                var assembly = Assembly.LoadFile(file);
                if (assembly.GetName().Version != entry.GetName().Version) throw new Exception("Mixed release: " + file);
                string target = null;
                foreach (var attribute in assembly.GetCustomAttributesData())
                    if (attribute.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
                        target = (string)attribute.ConstructorArguments[0].Value;
#if REVIT2024
                const string expectedTarget = ".NETFramework,Version=v4.8";
#else
                const string expectedTarget = ".NETCoreApp,Version=v8.0";
#endif
                if (target != expectedTarget) throw new Exception("Wrong target framework: " + file + " / " + target);
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
#if REVIT2024
                    const int apiMajor = 24;
#elif REVIT2025
                    const int apiMajor = 25;
#else
                    const int apiMajor = 26;
#endif
                    if ((reference.Name == "RevitAPI" || reference.Name == "RevitAPIUI") && reference.Version.Major != apiMajor)
                        throw new Exception("Wrong Revit API reference: " + file + " / " + reference.FullName);
                }
            }
            var contracts = request.GetType().Assembly;
            if (request.GetType().GetProperty("Parameters").GetValue(request, null) == null)
                throw new Exception("Contracts -> Newtonsoft.Json dependency failed");
            // Do not hijack a dependency request from another add-in.
            var resolve = entry.GetType("Tybim.Revit.Addin.DependencyResolver", true)
                .GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static);
            if (resolve.Invoke(null, new object[] { null, new ResolveEventArgs(contracts.FullName, typeof(Program).Assembly) }) != null)
                throw new Exception("Resolver intercepted an unrelated host");
            Console.WriteLine("PASS: isolated dependency JIT (" + (args.Length > 1 ? args[1] : "loadfile") + "), production resolver, five DLL identities, Contracts/JSON, requester isolation: " + folder);
            return 0;
        }
        catch (Exception ex)
        {
            var root = ex.GetBaseException();
            if (args.Length > 1 && args[1] == "baseline" && root is FileNotFoundException
                && root.Message.Contains("Tybim.Mcp.Contracts"))
            {
                Console.WriteLine("PASS: reproduced old startup failure: " + root.Message);
                return 0;
            }
            Console.Error.WriteLine(ex);
            if (ex is ReflectionTypeLoadException types)
                foreach (var error in types.LoaderExceptions) Console.Error.WriteLine(error);
            return 1;
        }
    }
}
