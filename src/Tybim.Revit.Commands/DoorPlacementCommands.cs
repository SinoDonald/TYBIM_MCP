using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Tybim.Revit.Commands
{
    internal static class DoorPlacement
    {
        internal sealed class Target
        {
            public string WallUniqueId;
            public string WallId;
            public string Snapshot;
            public XYZ Point;
            public ElementId LevelId;
            public string Skip;
            public object[] Diagnostics = Array.Empty<object>();
        }
        internal sealed class Plan
        {
            public string Id = Guid.NewGuid().ToString("N");
            public string DocumentId;
            public string SymbolUniqueId;
            public Guid SymbolVersion;
            public ElementId PhaseId;
            public double PositionFraction;
            public DateTime Expires = DateTime.UtcNow.AddMinutes(10);
            public List<Target> Targets;
            public string State = "preview";
            public object Result;
        }
        internal static readonly Dictionary<string, Plan> Plans = new Dictionary<string, Plan>();
        internal static double Mm(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        internal static double Feet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        internal static double Dimension(FamilySymbol symbol, BuiltInParameter parameter) => symbol.get_Parameter(parameter)?.AsDouble() ?? 0;
        internal static bool IsWallDoor(FamilySymbol symbol) => symbol.Category?.Id.Value == (long)BuiltInCategory.OST_Doors &&
            symbol.Family.FamilyPlacementType == FamilyPlacementType.OneLevelBasedHosted &&
            symbol.Family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR)?.AsInteger() == 1;
        internal static string Required(JObject p, string name)
        {
            if (p[name]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(p.Value<string>(name))) throw new InvalidOperationException("MISSING_" + name);
            return p.Value<string>(name);
        }
        internal static Document Document(UIApplication app, JObject p)
        {
            Required(p, "documentId");
            var doc = Context.Document(app, p);
            if (doc.IsFamilyDocument || doc.IsReadOnly || doc.IsModifiable) throw new InvalidOperationException("DOCUMENT_NOT_EDITABLE_PROJECT");
            return doc;
        }
        internal static Phase Phase(Document doc)
        {
            var id = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId();
            return (id == null ? null : doc.GetElement(id) as Phase) ?? throw new InvalidOperationException("ACTIVE_VIEW_HAS_NO_PHASE");
        }
        internal static string Snapshot(Document doc, Wall wall)
        {
            var level = doc.GetElement(wall.LevelId);
            return string.Join("|", new[] { wall.VersionGuid.ToString(), wall.WallType.VersionGuid.ToString(), level?.VersionGuid.ToString() ?? "" }
                .Concat(wall.FindInserts(true, false, true, true).Select(id => doc.GetElement(id)).Where(e => e != null)
                .OrderBy(e => e.UniqueId).Select(e => e.UniqueId + ":" + e.VersionGuid)));
        }
        internal static Target Inspect(Document doc, Wall wall, FamilySymbol symbol, Phase phase, double fraction)
        {
            DoorPositionRules.ValidateFraction(fraction);
            var target = new Target { WallUniqueId = wall.UniqueId, WallId = wall.Id.Value.ToString() };
            string Skip(string reason) { target.Skip = reason; return reason; }
            if (wall.WallType.Kind != WallKind.Basic || wall.IsStackedWall || wall.IsStackedWallMember) { Skip("UNSUPPORTED_WALL_KIND"); return target; }
            if (wall.GroupId != ElementId.InvalidElementId) { Skip("WALL_IN_GROUP"); return target; }
            if (wall.DesignOption != null) { Skip("WALL_IN_DESIGN_OPTION"); return target; }
            if (wall.CrossSection != WallCrossSection.Vertical) { Skip("SLANTED_OR_TAPERED_WALL"); return target; }
            var phaseStatus = wall.GetPhaseStatus(phase.Id);
            if (phaseStatus != ElementOnPhaseStatus.New && phaseStatus != ElementOnPhaseStatus.Existing) { Skip("WALL_NOT_PRESENT_IN_VIEW_PHASE"); return target; }
            var curve = (wall.Location as LocationCurve)?.Curve;
            var level = doc.GetElement(wall.LevelId) as Level;
            if (!(curve is Line || curve is Arc) || level == null || Math.Abs(curve.GetEndPoint(0).Z - curve.GetEndPoint(1).Z) > Feet(1))
            { Skip("UNSUPPORTED_LOCATION_OR_LEVEL"); return target; }
            double width = Dimension(symbol, BuiltInParameter.DOOR_WIDTH), height = Dimension(symbol, BuiltInParameter.DOOR_HEIGHT);
            if (width <= 0 || height <= 0) { Skip("DOOR_TYPE_DIMENSIONS_UNKNOWN"); return target; }
            if (!DoorPositionRules.HasEndClearance(curve.Length, width, Feet(50), fraction)) { Skip("INSUFFICIENT_CLEARANCE_TO_WALL_END"); return target; }
            var position = curve.Evaluate(fraction, true);
            target.Point = new XYZ(position.X, position.Y, level.ProjectElevation + (wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0));
            target.LevelId = level.Id;
            var box = wall.get_BoundingBox(null);
            if (box == null || target.Point.Z + height > box.Max.Z + Feet(1)) { Skip("WALL_TOO_LOW"); return target; }
            var tangent = curve.ComputeDerivatives(fraction, true).BasisX.Normalize();
            foreach (var insertId in wall.FindInserts(true, false, true, true))
            {
                var insert = doc.GetElement(insertId);
                if (insert == null) continue;
                if (insert is FamilyInstance instance)
                {
                    var status = instance.GetPhaseStatus(phase.Id);
                    if (status == ElementOnPhaseStatus.Demolished || status == ElementOnPhaseStatus.Future || status == ElementOnPhaseStatus.Temporary) continue;
                    var location = (instance.Location as LocationPoint)?.Point;
                    if (instance.Category?.Id.Value == (long)BuiltInCategory.OST_Doors && location != null &&
                        new XYZ(location.X, location.Y, target.Point.Z).DistanceTo(target.Point) <= Feet(10) && Math.Abs(location.Z - target.Point.Z) <= Feet(10))
                    { Skip("DOOR_ALREADY_AT_TARGET"); return target; }
                }
                var bounds = insert.get_BoundingBox(null);
                if (bounds == null) { Skip("INSERT_WITHOUT_BOUNDS"); return target; }
                var center = (bounds.Min + bounds.Max) * 0.5;
                var delta = center - target.Point;
                double halfSpan = (Math.Abs(tangent.X) * (bounds.Max.X - bounds.Min.X) + Math.Abs(tangent.Y) * (bounds.Max.Y - bounds.Min.Y)) * 0.5;
                if (Math.Abs(delta.DotProduct(tangent)) <= width * 0.5 + halfSpan + Feet(10) && bounds.Max.Z > target.Point.Z + Feet(1) && bounds.Min.Z < target.Point.Z + height)
                { Skip("EXISTING_INSERT_OVERLAPS_TARGET"); return target; }
            }
            target.Snapshot = Snapshot(doc, wall);
            return target;
        }
        internal static object Describe(Target t) => new { wallId = t.WallId, wallUniqueId = t.WallUniqueId,
            status = t.Skip == null ? "ready" : "skipped", reason = t.Skip, diagnostics = t.Diagnostics,
            pointMm = t.Point == null ? null : new { x = Mm(t.Point.X), y = Mm(t.Point.Y), z = Mm(t.Point.Z) } };

        internal sealed class RollbackFailures : IFailuresPreprocessor
        {
            public readonly List<string> Messages = new List<string>();
            public readonly List<object> Details = new List<object>();
            public string HostWallId;
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                var doc = accessor.GetDocument();
                object DescribeElement(ElementId id)
                {
                    var element = doc.GetElement(id);
                    return new { id = id.Value.ToString(), uniqueId = element?.UniqueId, category = element?.Category?.Name,
                        hostWallId = (element as FamilyInstance)?.Host?.Id.Value.ToString() };
                }
                foreach (var failure in accessor.GetFailureMessages())
                {
                    Messages.Add(failure.GetDescriptionText());
                    Details.Add(new { description = failure.GetDescriptionText(), severity = failure.GetSeverity().ToString(),
                        failureId = failure.GetFailureDefinitionId().Guid.ToString(), candidateWallId = HostWallId,
                        failingElements = failure.GetFailingElementIds().Select(DescribeElement).ToArray(),
                        additionalElements = failure.GetAdditionalElementIds().Select(DescribeElement).ToArray() });
                }
                return Messages.Count > 0 ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
            }
        }

        internal static FamilyInstance CreateDoor(Document doc, FamilySymbol symbol, Target target, ElementId phaseId)
        {
            if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
            var wall = (Wall)doc.GetElement(target.WallUniqueId);
            var level = (Level)doc.GetElement(target.LevelId);
            var door = doc.Create.NewFamilyInstance(target.Point, symbol, wall, level, StructuralType.NonStructural);
            if (door == null || door.Host?.Id != wall.Id) throw new InvalidOperationException("DOOR_HOST_MISMATCH: " + target.WallId);
            if (door.CreatedPhaseId != phaseId) door.CreatedPhaseId = phaseId;
            var sill = door.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            if (sill != null && !sill.IsReadOnly) sill.Set(target.Point.Z - level.ProjectElevation);
            doc.Regenerate();
            var actual = (door.Location as LocationPoint)?.Point;
            if (actual == null || actual.DistanceTo(target.Point) > Feet(2)) throw new InvalidOperationException("DOOR_LOCATION_MISMATCH: " + target.WallId);
            return door;
        }

        // Commit each candidate to run Revit's end-of-transaction join checks.
        // Earlier successful candidates remain in the trial to detect interactions.
        // The enclosing group ALWAYS rolls back, including on exceptions/time limits.
        internal static void ValidateByTrial(Document doc, string symbolUniqueId, ElementId phaseId, List<Target> targets)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using (var group = new TransactionGroup(doc, "TYBIM MCP - Door preview (rolled back)"))
            {
                if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("PREVIEW_GROUP_START_FAILED");
                try
                {
                    foreach (var target in targets.Where(t => t.Skip == null))
                    {
                        if (watch.Elapsed.TotalSeconds > 35) { target.Skip = "TRIAL_NOT_RUN_TIME_LIMIT_REDUCE_BATCH"; continue; }
                        var failures = new RollbackFailures { HostWallId = target.WallId };
                        using (var transaction = new Transaction(doc, "TYBIM MCP - Trial wall " + target.WallId))
                        {
                            try
                            {
                                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("TRIAL_START_FAILED");
                                transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetFailuresPreprocessor(failures).SetClearAfterRollback(true));
                                CreateDoor(doc, (FamilySymbol)doc.GetElement(symbolUniqueId), target, phaseId);
                                if (transaction.Commit() != TransactionStatus.Committed) target.Skip = "REVIT_TRIAL_PLACEMENT_FAILED";
                            }
                            catch (Autodesk.Revit.Exceptions.RegenerationFailedException) { throw; }
                            catch (Exception ex)
                            {
                                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                                target.Skip = "REVIT_TRIAL_PLACEMENT_FAILED";
                                failures.Details.Add(new { candidateWallId = target.WallId, error = ex.Message });
                            }
                            target.Diagnostics = failures.Details.ToArray();
                        }
                    }
                }
                finally
                {
                    if (group.GetStatus() == TransactionStatus.Started && group.RollBack() != TransactionStatus.RolledBack)
                        throw new InvalidOperationException("PREVIEW_GROUP_ROLLBACK_FAILED");
                }
            }
            // Trial commits/rollback can change element wrappers; recapture from the
            // restored document before storing the approval plan.
            foreach (var target in targets.Where(t => t.Skip == null)) target.Snapshot = Snapshot(doc, (Wall)doc.GetElement(target.WallUniqueId));
        }
        internal static Plan GetPlan(UIApplication app, JObject p)
        {
            Required(p, "documentId");
            var doc = Context.Document(app, p);
            if (!Plans.TryGetValue(Required(p, "planId"), out var plan) || plan.DocumentId != Context.Id(doc)) throw new InvalidOperationException("PLAN_NOT_FOUND_FOR_DOCUMENT");
            return plan;
        }
    }

    public sealed class DoorPlacementContextCommand : IRevitCommand
    {
        public string Name => "revit.doors.context";
        public object Execute(UIApplication app, JObject p)
        {
            var doc = Context.Document(app, p);
            if (doc.IsFamilyDocument) throw new InvalidOperationException("PROJECT_DOCUMENT_REQUIRED");
            var scope = p.Value<string>("scope") ?? "all";
            IEnumerable<Wall> walls;
            if (scope == "all") walls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>();
            else if (scope == "view") walls = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Wall)).Cast<Wall>();
            else if (scope == "selection") walls = app.ActiveUIDocument.Selection.GetElementIds().Select(id => doc.GetElement(id)).OfType<Wall>();
            else throw new InvalidOperationException("INVALID_SCOPE");
            var list = walls.OrderBy(w => w.Id.Value).ToArray();
            var symbols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName).ThenBy(s => s.Name).ToArray();
            int offset = Context.Integer(p, "offset", 0, 0, int.MaxValue), limit = Context.Integer(p, "limit", 100, 1, 1000);
            return new { documentId = Context.Id(doc), title = doc.Title, scope, totalWalls = list.Length, offset,
                walls = list.Skip(offset).Take(limit).Select(w => new { id = w.Id.Value.ToString(), uniqueId = w.UniqueId, type = w.WallType.Name, kind = w.WallType.Kind.ToString() }).ToArray(),
                totalDoorTypes = symbols.Length, doorTypesTruncated = symbols.Length > 500,
                doorTypes = symbols.Take(500).Select(s => new { id = s.Id.Value.ToString(), uniqueId = s.UniqueId, family = s.FamilyName, type = s.Name,
                    wallHosted = DoorPlacement.IsWallDoor(s), widthMm = DoorPlacement.Mm(DoorPlacement.Dimension(s, BuiltInParameter.DOOR_WIDTH)),
                    heightMm = DoorPlacement.Mm(DoorPlacement.Dimension(s, BuiltInParameter.DOOR_HEIGHT)) }).ToArray() };
        }
    }

    public sealed class PreviewDoorsAtWallCentersCommand : IRevitCommand
    {
        private readonly bool alongWall;
        public PreviewDoorsAtWallCentersCommand(bool alongWall = false) { this.alongWall = alongWall; }
        public string Name => alongWall ? "revit.doors.preview_along" : "revit.doors.preview_centers";
        public object Execute(UIApplication app, JObject p)
        {
            var doc = DoorPlacement.Document(app, p);
            double fraction = 0.5;
            if (alongWall)
            {
                if (p["positionFraction"]?.Type != JTokenType.Float && p["positionFraction"]?.Type != JTokenType.Integer)
                    throw new InvalidOperationException("POSITION_FRACTION_REQUIRED");
                fraction = DoorPositionRules.ValidateFraction(p.Value<double>("positionFraction"));
            }
            if (!(doc.GetElement(DoorPlacement.Required(p, "doorTypeUniqueId")) is FamilySymbol symbol) || !DoorPlacement.IsWallDoor(symbol))
                throw new InvalidOperationException("WALL_HOSTED_DOOR_TYPE_REQUIRED");
            var ids = p["wallUniqueIds"] as JArray;
            if (ids == null || ids.Count < 1 || ids.Count > 100 || ids.Any(id => id.Type != JTokenType.String) || ids.Select(id => (string)id).Distinct().Count() != ids.Count)
                throw new InvalidOperationException("REQUIRE_1_TO_100_DISTINCT_WALL_UNIQUE_IDS");
            var phase = DoorPlacement.Phase(doc);
            var targets = new List<DoorPlacement.Target>();
            foreach (var id in ids.Values<string>())
            {
                if (!(doc.GetElement(id) is Wall wall)) throw new InvalidOperationException("WALL_NOT_FOUND: " + id);
                targets.Add(DoorPlacement.Inspect(doc, wall, symbol, phase, fraction));
            }
            var symbolUniqueId = symbol.UniqueId;
            var phaseId = phase.Id;
            DoorPlacement.ValidateByTrial(doc, symbolUniqueId, phaseId, targets);
            symbol = (FamilySymbol)doc.GetElement(symbolUniqueId);
            phase = (Phase)doc.GetElement(phaseId);
            foreach (var stale in DoorPlacement.Plans.Where(pair => pair.Value.Expires < DateTime.UtcNow && pair.Value.State == "preview").Select(pair => pair.Key).ToArray()) DoorPlacement.Plans.Remove(stale);
            if (DoorPlacement.Plans.Count >= 1000) throw new InvalidOperationException("PLAN_STORE_FULL");
            var plan = new DoorPlacement.Plan { DocumentId = Context.Id(doc), SymbolUniqueId = symbol.UniqueId, SymbolVersion = symbol.VersionGuid, PhaseId = phase.Id, PositionFraction = fraction, Targets = targets };
            DoorPlacement.Plans.Add(plan.Id, plan);
            return new { planId = plan.Id, documentId = plan.DocumentId, title = doc.Title, expiresAtUtc = plan.Expires,
                doorType = symbol.FamilyName + ": " + symbol.Name, phase = phase.Name, positionFraction = plan.PositionFraction, reference = "LocationCurve start endpoint 0 toward endpoint 1",
                requested = targets.Count, ready = targets.Count(t => t.Skip == null), skipped = targets.Count(t => t.Skip != null),
                validation = "Revit trial commits inside rolled-back transaction group", modelChangesRetained = false,
                targets = targets.Select(DoorPlacement.Describe).ToArray() };
        }
    }

    public sealed class ExecuteDoorsAtWallCentersCommand : IRevitCommand
    {
        private readonly bool alongWall;
        public ExecuteDoorsAtWallCentersCommand(bool alongWall = false) { this.alongWall = alongWall; }
        public string Name => alongWall ? "revit.doors.execute_along" : "revit.doors.execute_centers";
        public object Execute(UIApplication app, JObject p)
        {
            var doc = DoorPlacement.Document(app, p);
            var plan = DoorPlacement.GetPlan(app, p);
            // The plan ID is also the idempotent operation ID for this Revit session.
            if (plan.Result != null) return plan.Result;
            if (plan.State != "preview") throw new InvalidOperationException("OPERATION_ALREADY_RUNNING");
            if (DateTime.UtcNow > plan.Expires) throw new InvalidOperationException("PLAN_EXPIRED");
            if (!(doc.GetElement(plan.SymbolUniqueId) is FamilySymbol symbol) || symbol.VersionGuid != plan.SymbolVersion) throw new InvalidOperationException("DOOR_TYPE_CHANGED_REPREVIEW");
            var phase = DoorPlacement.Phase(doc);
            if (phase.Id != plan.PhaseId) throw new InvalidOperationException("VIEW_PHASE_CHANGED_REPREVIEW");
            foreach (var target in plan.Targets.Where(t => t.Skip == null))
            {
                if (!(doc.GetElement(target.WallUniqueId) is Wall wall) || DoorPlacement.Snapshot(doc, wall) != target.Snapshot)
                    throw new InvalidOperationException("WALL_OR_INSERTS_CHANGED_REPREVIEW: " + target.WallId);
                if (DoorPlacement.Inspect(doc, wall, symbol, phase, plan.PositionFraction).Skip != null) throw new InvalidOperationException("PLACEMENT_CONDITIONS_CHANGED_REPREVIEW: " + target.WallId);
            }
            plan.State = "running";
            var created = new List<object>();
            if (plan.Targets.All(t => t.Skip != null))
            {
                plan.State = "completed";
                plan.Result = new { planId = plan.Id, documentId = plan.DocumentId, status = plan.State, createdCount = 0,
                    created = created.ToArray(), skipped = plan.Targets.Select(DoorPlacement.Describe).ToArray() };
                return plan.Result;
            }
            var warnings = new DoorPlacement.RollbackFailures();
            string currentWallId = null;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            const string undoName = "TYBIM MCP - Doors along walls";
            using (var transaction = new Transaction(doc, undoName))
            {
                try
                {
                    if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("TRANSACTION_START_FAILED");
                    transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetFailuresPreprocessor(warnings).SetClearAfterRollback(true));
                    if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
                    foreach (var target in plan.Targets.Where(t => t.Skip == null))
                    {
                        currentWallId = target.WallId;
                        if (stopwatch.Elapsed.TotalSeconds > 40) throw new InvalidOperationException("BATCH_TIME_LIMIT_REDUCE_BATCH_SIZE");
                        var door = DoorPlacement.CreateDoor(doc, symbol, target, plan.PhaseId);
                        var actual = (door.Location as LocationPoint)?.Point;
                        if (actual == null || actual.DistanceTo(target.Point) > DoorPlacement.Feet(2)) throw new InvalidOperationException("DOOR_LOCATION_MISMATCH: " + target.WallId);
                        created.Add(new { wallId = target.WallId, doorId = door.Id.Value.ToString(), doorUniqueId = door.UniqueId, positionFraction = plan.PositionFraction,
                            pointMm = new { x = DoorPlacement.Mm(actual.X), y = DoorPlacement.Mm(actual.Y), z = DoorPlacement.Mm(actual.Z) } });
                    }
                    if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("TRANSACTION_NOT_COMMITTED");
                    plan.State = "completed";
                    plan.Result = new { planId = plan.Id, documentId = plan.DocumentId, status = plan.State, createdCount = created.Count,
                        positionFraction = plan.PositionFraction, created = created.ToArray(), skipped = plan.Targets.Where(t => t.Skip != null).Select(DoorPlacement.Describe).ToArray(), undoName };
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    plan.State = "rolledBack";
                    plan.Result = new { planId = plan.Id, documentId = plan.DocumentId, status = plan.State, createdCount = 0, error = ex.Message,
                        lastAttemptedWallId = currentWallId, failures = warnings.Messages.ToArray(), failureDetails = warnings.Details.ToArray(),
                        attemptedWalls = created.ToArray() };
                }
            }
            return plan.Result;
        }
    }

    public sealed class DoorPlacementStatusCommand : IRevitCommand
    {
        public string Name => "revit.doors.operation_status";
        public object Execute(UIApplication app, JObject p)
        {
            var plan = DoorPlacement.GetPlan(app, p);
            return plan.Result ?? new { planId = plan.Id, documentId = plan.DocumentId, status = plan.State, expiresAtUtc = plan.Expires };
        }
    }
}
