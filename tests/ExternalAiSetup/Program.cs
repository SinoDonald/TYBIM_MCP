using Newtonsoft.Json.Linq;
using Tybim.Revit.Addin;

static class Program
{
    static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS: "+label);}
    static void Main()
    {
        var directory=Path.Combine(Path.GetTempPath(),"tybim-setup-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var receipt=Path.Combine(directory,"completed.json");
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"fresh user is offered setup");
            var node=Path.Combine(directory,"node.exe");var script=Path.Combine(directory,"index.js");var bridge=Path.Combine(directory,"external.bridge.config.json");
            var entry=JObject.FromObject(new{command=node,args=new[]{script},env=new{TYBIM_MCP_CONFIG=bridge}});
            File.WriteAllText(Path.Combine(directory,"external-ai.mcp.json"),new JObject{["mcpServers"]=new JObject{["tybim_revit_2025_external"]=entry}}.ToString());
            var config=Path.Combine(directory,"claude.json");
            File.WriteAllText(config,new JObject{["mcpServers"]=new JObject{["tybim_revit_2025_external"]=entry.DeepClone()}}.ToString());
            var state=JObject.FromObject(new{schema=1,year="2025",installDirectory=directory,clients=new[]{new{client="ClaudeCode",path=config}}});
            File.WriteAllText(receipt,state.ToString());
            Check(ExternalAiSetup.IsConfigured(directory,"2025",receipt),"successful selected client setup suppresses repeated first-run prompts");
            Check(!ExternalAiSetup.IsConfigured(directory,"2024",receipt),"another Revit year has independent setup");
            state["clients"]=JArray.FromObject(new[]{new{client="ClaudeDesktop",path=config}});
            File.WriteAllText(receipt,state.ToString());
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"legacy Desktop receipt reopens setup for profile discovery");
            state["schema"]=2;
            File.WriteAllText(receipt,state.ToString());
            Check(ExternalAiSetup.IsConfigured(directory,"2025",receipt),"new Desktop receipt accepts a verified profile path");
            var secondDesktop=Path.Combine(directory,"desktop-second.json");
            File.WriteAllText(secondDesktop,"{}");
            state["clients"]=JArray.FromObject(new[]{new{client="ClaudeDesktop",path=config},new{client="ClaudeDesktop",path=secondDesktop}});
            File.WriteAllText(receipt,state.ToString());
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"all coexisting Desktop profile registrations must remain valid");
            state["clients"]=JArray.FromObject(new[]{new{client="ClaudeCode",path=config}});
            File.WriteAllText(receipt,state.ToString());
            File.WriteAllText(config,"{}");
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"removed Claude server causes setup to be offered again");
            var codex=Path.Combine(directory,"config.toml");
            File.WriteAllText(codex,"[mcp_servers.tybim_revit_2025]\r\ncommand = "+entry["command"].ToString(Newtonsoft.Json.Formatting.None)+"\r\nargs = ["+entry["args"][0].ToString(Newtonsoft.Json.Formatting.None)+"]\r\nenabled = true\r\n[mcp_servers.tybim_revit_2025.env]\r\nTYBIM_MCP_CONFIG = "+entry["env"]["TYBIM_MCP_CONFIG"].ToString(Newtonsoft.Json.Formatting.None)+"\r\n");
            state["clients"]=JArray.FromObject(new[]{new{client="Codex",path=codex}});File.WriteAllText(receipt,state.ToString());
            Check(ExternalAiSetup.IsConfigured(directory,"2025",receipt),"Codex TOML and external bridge are verified against installed export");
            File.WriteAllText(codex,File.ReadAllText(codex).Replace("enabled = true","enabled = false"));
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"disabled Codex entry is not treated as configured");
            File.Delete(codex);
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"deleted client config is detected");
            File.WriteAllText(receipt,"not JSON");
            Check(!ExternalAiSetup.IsConfigured(directory,"2025",receipt),"damaged receipt does not crash service startup");
        }
        finally {Directory.Delete(directory,true);}
    }
}
namespace Autodesk.Revit.Attributes { public enum TransactionMode{Manual}public sealed class TransactionAttribute:Attribute { public TransactionAttribute(TransactionMode mode){} } }
namespace Autodesk.Revit.DB { public sealed class ElementSet{} }
namespace Autodesk.Revit.UI
{
    public enum Result{Succeeded,Failed}public sealed class ExternalCommandData{}
    public interface IExternalCommand {Result Execute(ExternalCommandData data,ref string message,Autodesk.Revit.DB.ElementSet elements);}
}
namespace Tybim.Revit.Addin {internal sealed class RevitApplication {internal static RevitApplication Current;internal void OpenExternalAiSetup(){}}}
