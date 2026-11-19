using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Tybim.Revit.Addin
{
    [Transaction(TransactionMode.Manual)]
    public sealed class ExternalAiCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                var app = RevitApplication.Current ?? throw new InvalidOperationException("TYBIM MCP 尚未啟動。");
                app.ToggleExternalAi();
                return Result.Succeeded;
            }
            catch (Exception error)
            {
                message = "外部AI服務切換失敗：" + error.Message;
                return Result.Failed;
            }
        }
    }
}
