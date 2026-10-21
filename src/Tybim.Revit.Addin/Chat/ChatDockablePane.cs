using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Tybim.Revit.Addin.Chat
{
    internal sealed class ChatDockablePane : IDockablePaneProvider
    {
        public static readonly DockablePaneId Id = new DockablePaneId(new Guid("8E444975-3EC6-4334-963E-61C3DE8F6DC9"));
        public ChatPanel Panel { get; }
        public ChatDockablePane(string configPath) { Panel = new ChatPanel(configPath); }
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = Panel;
            data.VisibleByDefault = true;
            data.InitialState = new DockablePaneState { DockPosition = DockPosition.Right };
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class ShowChatCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var pane = data.Application.GetDockablePane(ChatDockablePane.Id);
            if (pane.IsShown()) pane.Hide(); else pane.Show();
            return Result.Succeeded;
        }
    }
}
