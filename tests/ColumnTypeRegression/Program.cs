using Autodesk.Revit.DB;
using RevitMCP.Core;

static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    static void Refused(Document doc, long? id, string name, string expected)
    {
        try { ColumnTypeSelection.Resolve(doc, id, name); }
        catch (InvalidOperationException error)
        {
            Check(error.Message.Contains(expected), "selection diagnostic contains " + expected);
            return;
        }
        throw new Exception("Expected explicit selection request");
    }

    static void Main()
    {
        var concrete = new FamilySymbol(154486, "混凝土柱-矩形", "30 x 50cm", BuiltInCategory.OST_StructuralColumns);
        concrete.Parameters["柱寬"] = new Parameter(.3 / .3048);
        concrete.Parameters["柱深"] = new Parameter(.5 / .3048);
        var otherConcrete = new FamilySymbol(255901, "混凝土柱-矩形", "80x70", BuiltInCategory.OST_StructuralColumns);
        var architectural = new FamilySymbol(200, "建築柱", "30 x 50cm", BuiltInCategory.OST_Columns);
        var doc = new Document();
        doc.Symbols.AddRange(new[] { concrete, otherConcrete, architectural,
            new FamilySymbol(300, "門", "M1", BuiltInCategory.OST_Doors) });
        Check(ColumnTypeSelection.Loaded(doc).Count == 3, "both column categories are listed and doors are excluded");
        Check(Math.Abs(ColumnTypeSelection.Dimension(concrete, "b").AsDouble() * 304.8 - 300) < .001,
            "localized 柱寬 produces the actual 300 mm width");
        Check(Math.Abs(ColumnTypeSelection.Dimension(concrete, "h").AsDouble() * 304.8 - 500) < .001,
            "localized 柱深 produces the actual 500 mm depth");
        Check(ColumnTypeSelection.Resolve(doc, 154486, null) == concrete, "exact Type ID selects concrete");
        Check(ColumnTypeSelection.Resolve(doc, 200, null) == architectural, "exact Type ID selects architectural column");
        Check(ColumnTypeSelection.Resolve(doc, null, "80x70") == otherConcrete, "legacy unique Type name remains supported");
        Check(ColumnTypeSelection.Resolve(doc, null, "混凝土柱-矩形：30 x 50cm") == concrete,
            "family plus Type name distinguishes identical Type names");
        Refused(doc, null, "30 x 50cm", "columnTypeId");
        Refused(doc, null, "結構柱", "154486");
        Refused(doc, null, null, "200");
        Refused(doc, null, "missing", "專案仍有可用柱型");
        Refused(doc, 300, null, "不是目前專案可用的柱 Type");
        Refused(new Document(), null, null, "沒有已載入");
        Check(concrete.Name == "30 x 50cm" && doc.Symbols.Count == 4, "selection and diagnostics do not modify Types");
    }
}

namespace Autodesk.Revit.DB
{
    public enum BuiltInCategory { OST_Columns, OST_StructuralColumns, OST_Doors }
    public enum StorageType { Double }
    public sealed class ElementId
    {
        readonly long value;
        public ElementId(long id) { value = id; }
        public long GetIdValue() => value;
    }
    public sealed class Category
    {
        public ElementId Id;
        public string Name;
    }
    public sealed class Parameter
    {
        readonly double value;
        public StorageType StorageType => StorageType.Double;
        public bool HasValue => true;
        public Parameter(double number) { value = number; }
        public double AsDouble() => value;
    }
    public sealed class FamilySymbol
    {
        public ElementId Id;
        public Category Category;
        public string Name, FamilyName;
        public Dictionary<string, Parameter> Parameters = new Dictionary<string, Parameter>();
        public FamilySymbol(long id, string family, string name, BuiltInCategory category)
        {
            Id = new ElementId(id); Name = name; FamilyName = family;
            Category = new Category { Id = new ElementId((long)category), Name = category == BuiltInCategory.OST_StructuralColumns ? "結構柱" : "建築柱" };
        }
        public Parameter LookupParameter(string name) => Parameters.TryGetValue(name, out var parameter) ? parameter : null;
    }
    public sealed class Document { public List<FamilySymbol> Symbols = new List<FamilySymbol>(); }
    public sealed class FilteredElementCollector : List<FamilySymbol>
    {
        public FilteredElementCollector(Document doc) { AddRange(doc.Symbols); }
        public FilteredElementCollector OfClass(Type type) => this;
    }
}
