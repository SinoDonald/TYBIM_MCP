using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCP.Core;
using RevitMCP.Models;

namespace Tybim.Revit.Study
{
    public sealed class StudyCommand : ExternalEventCommandBase
    {
        private readonly UIApplication app;
        private readonly string name;
        public override string CommandName => "study." + name;
        public StudyCommand(UIApplication app, string name) : base(new NoEvent(), app) { this.app = app; this.name = name; }
        public override object Execute(JObject parameters, string requestId)
        {
            var response = new CommandExecutor(app).ExecuteCommand(new RevitCommandRequest { CommandName = name, Parameters = parameters, RequestId = requestId });
            if (!response.Success) throw new System.InvalidOperationException(response.Error ?? "STUDY_COMMAND_FAILED");
            return response.Data;
        }
        private sealed class NoEvent : IExternalEventHandler
        {
            public void Execute(UIApplication app) { throw new System.InvalidOperationException("Study uses the existing TYBIM Dispatcher"); }
            public string GetName() => "TYBIM Study adapter";
        }
    }
}
