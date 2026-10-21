using System;

namespace Tybim.Revit.Commands
{
    internal static class DoorPositionRules
    {
        public static double ValidateFraction(double fraction)
        {
            if (double.IsNaN(fraction) || double.IsInfinity(fraction) || fraction <= 0 || fraction >= 1)
                throw new ArgumentOutOfRangeException(nameof(fraction), "Position fraction must be strictly between 0 and 1.");
            return fraction;
        }

        // For the supported line and arc curves, normalized parameter equals length fraction.
        public static bool HasEndClearance(double length, double width, double clearance, double fraction)
        {
            ValidateFraction(fraction);
            return length > 0 && width > 0 && clearance >= 0 &&
                Math.Min(fraction, 1 - fraction) * length > width * 0.5 + clearance;
        }
    }
}
