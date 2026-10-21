using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Tybim.Revit.Commands
{
    public interface IRevitCommand
    {
        string Name { get; }
        object Execute(UIApplication app, JObject parameters);
    }

    public sealed partial class CommandRegistry
    {
        private readonly Dictionary<string, IRevitCommand> commands = new Dictionary<string, IRevitCommand>(StringComparer.Ordinal);
        // Optional implementation lives in ignored private/revit source files.
        partial void RegisterPrivateCommands();
        public CommandRegistry Register(IRevitCommand command)
        {
            commands.Add(command.Name, command);
            return this;
        }
        public object Execute(string name, UIApplication app, JObject parameters)
        {
            if (!commands.TryGetValue(name, out var command)) throw new InvalidOperationException("UNKNOWN_COMMAND");
            return command.Execute(app, parameters);
        }
        public static CommandRegistry CreateDefault()
        {
            var registry = new CommandRegistry()
            .Register(new HealthCommand()).Register(new ProjectInfoCommand())
            .Register(new SelectionCommand()).Register(new LevelsCommand())
            .Register(new HelloWorldCommand())
            .Register(new DoorPlacementContextCommand()).Register(new PreviewDoorsAtWallCentersCommand())
            .Register(new ExecuteDoorsAtWallCentersCommand()).Register(new DoorPlacementStatusCommand())
            .Register(new PreviewDoorsAtWallCentersCommand(true)).Register(new ExecuteDoorsAtWallCentersCommand(true));
            registry.RegisterPrivateCommands();
            UpstreamCommand.Register(registry);
            return registry;
        }
    }
}
