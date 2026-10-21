using System;
using Tybim.Revit.Commands;

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS: " + label);
}
Check(DoorPositionRules.HasEndClearance(3000, 900, 50, 2.0 / 3), "900 mm door fits at two thirds of a 3 m wall");
Check(DoorPositionRules.HasEndClearance(1600, 900, 50, 2.0 / 3), "end clearance remains positive above threshold");
Check(!DoorPositionRules.HasEndClearance(1400, 900, 50, 2.0 / 3), "door near wall end rejected even when it would fit at midpoint");
Check(DoorPositionRules.HasEndClearance(1400, 900, 50, 0.5), "same wall has sufficient midpoint clearance");
Check(DoorPositionRules.HasEndClearance(3000, 900, 50, 1.0 / 3) == DoorPositionRules.HasEndClearance(3000, 900, 50, 2.0 / 3), "reversed wall direction preserves end-clearance decision");
foreach (double invalid in new[] { 0.0, 1.0, -0.1, 1.1, double.NaN, double.PositiveInfinity })
{
    bool rejected = false;
    try { DoorPositionRules.ValidateFraction(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, "invalid position rejected: " + invalid);
}
