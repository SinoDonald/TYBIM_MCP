using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Tybim.Revit.Commands
{
    public sealed class HelloWorldCommand : IRevitCommand
    {
        public string Name => "revit.hello_world";

        public object Execute(UIApplication app, JObject parameters)
        {
            var dialog = new TaskDialog("TYBIM MCP")
            {
                MainInstruction = "Hello World！",
                CommonButtons = TaskDialogCommonButtons.Ok,
                TitleAutoPrefix = false
            };
            dialog.Show();
            return new { message = "Hello World！", closed = true };
        }
    }
}
