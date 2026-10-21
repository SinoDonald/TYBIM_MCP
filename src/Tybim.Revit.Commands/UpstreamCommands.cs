using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using Tybim.Revit.Upstream;
using Tybim.Revit.Study;
using RevitMCPSDK.API.Base;

namespace Tybim.Revit.Commands
{
    public sealed class UpstreamCommand : IRevitCommand
    {
        private readonly string command;
        public string Name => "upstream." + command;
        public UpstreamCommand(string command) { this.command = command; }
        private sealed class Operation
        {
            public string Command;
            public JObject Parameters;
            public object Result;
        }
        private static readonly Dictionary<string, Operation> Operations = new Dictionary<string, Operation>();
        private static readonly Dictionary<string, Func<UIApplication, ExternalEventCommandBase>> Factories = UpstreamCatalog.Factories.ToDictionary(p => p.Key, p => p.Value);
        private static readonly HashSet<string> ReadOnly = new HashSet<string>(StringComparer.Ordinal)
        { "get_current_view_info", "get_current_view_elements", "get_available_family_types", "get_selected_elements",
          "get_material_quantities", "ai_element_filter", "analyze_model_statistics", "export_room_data", "say_hello" };
        public static bool IsReadOnly(string name) => ReadOnly.Contains(name);
        public static void RegisterExtension(string name, bool readOnly, Func<UIApplication, JObject, object> execute)
        {
            if (!name.StartsWith("private.", StringComparison.Ordinal)) throw new ArgumentException("Private extension names must start with private.");
            Factories[name] = app => new PrivateDelegateCommand(app, name, execute);
            if (readOnly) ReadOnly.Add(name); else ReadOnly.Remove(name);
        }
        private sealed class PrivateDelegateCommand : ExternalEventCommandBase
        {
            private readonly UIApplication app;
            private readonly Func<UIApplication, JObject, object> execute;
            public override string CommandName { get; }
            public PrivateDelegateCommand(UIApplication app, string name, Func<UIApplication, JObject, object> execute) : base(new UnusedEvent(), app)
            { this.app = app; this.execute = execute; CommandName = name; }
            public override object Execute(JObject parameters, string requestId) => execute(app, parameters);
            private sealed class UnusedEvent : IExternalEventHandler
            {
                public void Execute(UIApplication app) { throw new InvalidOperationException("Private extension uses TYBIM Dispatcher"); }
                public string GetName() => "TYBIM Private extension";
            }
        }
        public static void Register(CommandRegistry registry)
        {
            foreach (var item in StudyCatalog.Commands)
            {
                var name = item.Key;
                if (!Factories.ContainsKey("study." + name)) Factories.Add("study." + name, app => new StudyCommand(app, name));
                if (item.Value) ReadOnly.Add("study." + name);
            }
            foreach (var name in Factories.Keys) registry.Register(new UpstreamCommand(name));
            registry.Register(new UpstreamOperationStatus());
        }
        private static bool Failed(JToken token) => token is JObject obj && obj.Properties().Any(p =>
            (string.Equals(p.Name, "success", StringComparison.OrdinalIgnoreCase) && p.Value.Type == JTokenType.Boolean && !p.Value.Value<bool>())
            || (p.Value is JObject || p.Value is JArray) && Failed(p.Value))
            || token is JArray array && array.Any(Failed);

        public object Execute(UIApplication app, JObject parameters)
        {
            DoorPlacement.Required(parameters, "documentId");
            var documentId = DoorPlacement.Required(parameters, "documentId");
            bool study = command.StartsWith("study.", StringComparison.Ordinal);
            var studyName = study ? command.Substring(6) : null;
            bool applicationScope = study && StudyCatalog.ApplicationScoped(studyName) && app.ActiveUIDocument?.Document == null;
            var doc = applicationScope ? null : Context.Document(app, parameters);
            if (applicationScope && documentId != "application") throw new InvalidOperationException("USE_APPLICATION_DOCUMENT_ID_WHEN_NO_MODEL_IS_OPEN");
            var payload = (JObject)parameters.DeepClone();
            payload.Remove("documentId"); payload.Remove("operationId");
            if (IsReadOnly(command))
            {
                using (var native = Factories[command](app)) return native.Execute(payload, Guid.NewGuid().ToString("N"));
            }
            bool externalEffects = study && StudyCatalog.ExternalEffects.Contains(studyName);
            if (!study && !externalEffects && (doc == null || doc.IsFamilyDocument || doc.IsReadOnly || doc.IsModifiable)) throw new InvalidOperationException("DOCUMENT_NOT_EDITABLE_PROJECT");
            var operationId = DoorPlacement.Required(parameters, "operationId");
            if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidOperationException("OPERATION_ID_REQUIRES_32_HEX_CHARACTERS");
            var key = documentId + ":" + operationId;
            if (Operations.TryGetValue(key, out var old))
            {
                if (old.Command != command || !JToken.DeepEquals(old.Parameters, payload)) throw new InvalidOperationException("OPERATION_ID_REUSED_WITH_DIFFERENT_ARGUMENTS");
                return old.Result ?? new { operationId, documentId, status = "running" };
            }
            if (Operations.Count >= 1000) throw new InvalidOperationException("UPSTREAM_OPERATION_STORE_FULL_RESTART_SESSION");
            var state = new Operation { Command = command, Parameters = payload };
            Operations.Add(key, state);
            // Study commands manage their own transactions and may switch/open
            // documents. Preserve that contract rather than putting them inside
            // a group that prevents Save/Open or gives false rollback guarantees.
            if (study)
            {
                try
                {
                    object result;
                    using (var native = Factories[command](app)) result = native.Execute(payload, operationId);
                    if (result == null || Failed(JToken.FromObject(result)))
                        state.Result = new { operationId, documentId, status = "failed", result, nativeTransactions = true, partialChangesMayRemain = true, externalEffectsMayRemain = externalEffects };
                    else state.Result = new { operationId, documentId, status = "completed", result, nativeTransactions = true, externalEffectsMayRemain = externalEffects };
                }
                catch (Exception ex) { state.Result = new { operationId, documentId, status = "failed", error = ex.Message, nativeTransactions = true, partialChangesMayRemain = true, externalEffectsMayRemain = externalEffects }; }
                return state.Result;
            }
            var failures = new List<object>();
            object nativeResult = null;
            EventHandler<FailuresProcessingEventArgs> onFailures = (_, e) =>
            {
                var accessor = e.GetFailuresAccessor();
                if (!accessor.GetDocument().Equals(doc)) return;
                var messages = accessor.GetFailureMessages();
                foreach (var failure in messages) failures.Add(new { description = failure.GetDescriptionText(), severity = failure.GetSeverity().ToString(),
                    elementIds = failure.GetFailingElementIds().Select(id => id.Value.ToString()).ToArray() });
                if (messages.Count > 0)
                {
                    accessor.SetFailureHandlingOptions(accessor.GetFailureHandlingOptions().SetClearAfterRollback(true));
                    e.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack);
                }
            };
            using (var group = new TransactionGroup(doc, "TYBIM MCP - " + command))
            {
                app.Application.FailuresProcessing += onFailures;
                try
                {
                    if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("TRANSACTION_GROUP_START_FAILED");
                    object result;
                    using (var native = Factories[command](app)) result = native.Execute(payload, operationId);
                    nativeResult = result;
                    if (result == null) throw new InvalidOperationException("UPSTREAM_RETURNED_NO_RESULT");
                    var json = result == null ? JValue.CreateNull() : JToken.FromObject(result);
                    if (failures.Count > 0 || Failed(json)) throw new InvalidOperationException("UPSTREAM_OPERATION_FAILED");
                    if (group.Assimilate() != TransactionStatus.Committed) throw new InvalidOperationException("TRANSACTION_GROUP_NOT_COMMITTED");
                    state.Result = new { operationId, documentId = Context.Id(doc), status = "completed", result, undoName = "TYBIM MCP - " + command };
                }
                catch (Exception ex)
                {
                    if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    state.Result = new { operationId, documentId = Context.Id(doc), status = "rolledBack", error = ex.Message, failures = failures.ToArray(), result = nativeResult };
                }
                finally { app.Application.FailuresProcessing -= onFailures; }
            }
            return state.Result;
        }
        private sealed class UpstreamOperationStatus : IRevitCommand
        {
            public string Name => "upstream.operation_status";
            public object Execute(UIApplication app, JObject parameters)
            {
                DoorPlacement.Required(parameters, "documentId");
                var documentId = DoorPlacement.Required(parameters, "documentId");
                var id = DoorPlacement.Required(parameters, "operationId");
                if (!Operations.TryGetValue(documentId + ":" + id, out var operation))
                    throw new InvalidOperationException("OPERATION_NOT_FOUND_IN_CURRENT_REVIT_SESSION_DO_NOT_RETRY_AUTOMATICALLY");
                return operation.Result ?? new { operationId = id, documentId, status = "running" };
            }
        }
    }
}
