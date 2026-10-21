using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Tybim.Revit.Commands
{
    internal static class Context
    {
        public static Document Document(UIApplication app, JObject parameters)
        {
            var doc = app.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
            var expected = parameters.Value<string>("documentId");
            if (!string.IsNullOrEmpty(expected) && expected != Id(doc)) throw new InvalidOperationException("DOCUMENT_CHANGED");
            return doc;
        }
        private static readonly List<KeyValuePair<Document, string>> identities = new List<KeyValuePair<Document, string>>();
        public static string Id(Document doc)
        {
            // Revit may return different managed wrappers for one native document.
            // Document.Equals/GetHashCode compare the native session document.
            identities.RemoveAll(pair => !pair.Key.IsValidObject);
            var id = identities.FirstOrDefault(pair => pair.Key.Equals(doc)).Value;
            if (id == null) { id = Guid.NewGuid().ToString("N"); identities.Add(new KeyValuePair<Document, string>(doc, id)); }
            return id;
        }
        public static int Integer(JObject p, string name, int fallback, int min, int max)
        {
            var value = p[name];
            if (value == null) return fallback;
            if (value.Type != JTokenType.Integer) throw new InvalidOperationException("INVALID_PARAMETERS");
            var number = value.Value<long>();
            if (number < min || number > max) throw new InvalidOperationException("INVALID_PARAMETERS");
            return (int)number;
        }
    }
    public sealed class HealthCommand : IRevitCommand
    {
        public string Name => "revit.health";
        public object Execute(UIApplication app, JObject p) => new
        { protocolVersion = 1, revitVersion = app.Application.VersionNumber, processId = System.Diagnostics.Process.GetCurrentProcess().Id,
          hasActiveDocument = app.ActiveUIDocument != null, capabilities = new[] { "read", "dialog", "doors.preview", "doors.execute", "operations.status", "upstream.commands", "upstream.operations.status" } };
    }
    public sealed class ProjectInfoCommand : IRevitCommand
    {
        public string Name => "revit.project_info";
        public object Execute(UIApplication app, JObject p)
        {
            var doc = Context.Document(app, p);
            return new { documentId = Context.Id(doc), title = doc.Title, isFamilyDocument = doc.IsFamilyDocument,
                isReadOnly = doc.IsReadOnly, isWorkshared = doc.IsWorkshared,
                activeView = new { id = doc.ActiveView.Id.Value.ToString(), name = doc.ActiveView.Name } };
        }
    }
    public sealed class SelectionCommand : IRevitCommand
    {
        public string Name => "revit.selection";
        public object Execute(UIApplication app, JObject p)
        {
            var doc = Context.Document(app, p);
            var limit = Context.Integer(p, "limit", 100, 1, 1000);
            var ids = app.ActiveUIDocument.Selection.GetElementIds();
            return new { documentId = Context.Id(doc), total = ids.Count, truncated = ids.Count > limit,
                elements = ids.Take(limit).Select(id => doc.GetElement(id)).Where(e => e != null)
                    .Select(e => new { id = e.Id.Value.ToString(), uniqueId = e.UniqueId, name = e.Name, category = e.Category?.Name }).ToArray() };
        }
    }
    public sealed class LevelsCommand : IRevitCommand
    {
        public string Name => "revit.levels";
        public object Execute(UIApplication app, JObject p)
        {
            var doc = Context.Document(app, p);
            var offset = Context.Integer(p, "offset", 0, 0, int.MaxValue);
            var limit = Context.Integer(p, "limit", 100, 1, 1000);
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ThenBy(l => l.Id.Value).ToArray();
            return new { documentId = Context.Id(doc), total = levels.Length, offset, unit = "mm",
                levels = levels.Skip(offset).Take(limit).Select(l => new { id = l.Id.Value.ToString(), name = l.Name,
                    elevation = UnitUtils.ConvertFromInternalUnits(l.Elevation, UnitTypeId.Millimeters) }).ToArray() };
        }
    }
}
