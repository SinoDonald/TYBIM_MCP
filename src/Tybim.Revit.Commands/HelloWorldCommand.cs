using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Tybim.Revit.Commands
{
    public sealed class HelloWorldCommand : IRevitCommand
    {
        public string Name => "revit.hello_world";

        public object Execute(UIApplication app, JObject parameters)
        {
            Tybim.Revit.Upstream.TybimWindow.ShowMessage("TYBIM MCP","Hello World！",true);
            return new { message = "Hello World！", closed = true };
        }
    }
}
