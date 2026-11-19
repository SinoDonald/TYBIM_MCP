using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCP.Core
{
    public static class ColumnTypeSelection
    {
        public static bool IsColumn(FamilySymbol symbol) => symbol?.Category != null &&
            (symbol.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralColumns ||
             symbol.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Columns);

        public static List<FamilySymbol> Loaded(Document doc) => new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().Where(IsColumn)
            .OrderBy(s => s.FamilyName).ThenBy(s => s.Name).ToList();

        public static string AvailableMessage(Document doc) => Choices(Loaded(doc));

        public static Parameter Dimension(FamilySymbol symbol, string dimension)
        {
            string[] names = dimension == "b"
                ? new[] { "b", "B", "Width", "width", "寬度", "寬", "柱寬", "斷面寬", "B1" }
                : dimension == "h"
                    ? new[] { "h", "H", "Depth", "depth", "深度", "深", "柱深", "斷面深", "Height", "高度", "H1" }
                    : new[] { "d", "D", "Diameter", "直徑" };
            return names.Select(symbol.LookupParameter)
                .FirstOrDefault(p => p != null && p.StorageType == StorageType.Double && p.HasValue);
        }

        public static FamilySymbol Resolve(Document doc, long? typeId, string name)
        {
            var types = Loaded(doc);
            if (typeId.HasValue)
            {
                var selected = types.FirstOrDefault(s => s.Id.GetIdValue() == typeId.Value);
                if (selected != null) return selected;
                throw new InvalidOperationException("指定 ID 不是目前專案可用的柱 Type。" + Choices(types));
            }
            if (types.Count == 0) throw new InvalidOperationException("專案中沒有已載入的結構柱或建築柱 Type。");
            var matches = types;
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = name.Trim();
                var exact = types.Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.FamilyName + "：" + s.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
                matches = exact.Count > 0 ? exact : types.Where(s =>
                    s.FamilyName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(s.Category.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (matches.Count == 1) return matches[0];
            if (matches.Count == 0)
                throw new InvalidOperationException("柱型名稱條件沒有符合項目，但專案仍有可用柱型。" + Choices(types));
            throw new InvalidOperationException("有多個可用柱 Type，請讓使用者選擇後傳入 columnTypeId。" + Choices(matches));
        }

        private static string Choices(IEnumerable<FamilySymbol> types) => " 可選柱型：" +
            string.Join("；", types.Select(s => $"{s.Id.GetIdValue()} = {s.FamilyName}：{s.Name}（{s.Category.Name}）"));
    }
}
