using System;

namespace HoardersForge
{
    public static class ForgeMath
    {
        public static double CalculateBaseUnits(int voxelCount, bool hasSmithingPlus)
        {
            if (voxelCount <= 0) return 0.0;

            if (hasSmithingPlus)
            {
                double rawUnits = voxelCount * (100.0 / 42.0);
                double units = Math.Floor(rawUnits / 5.0) * 5.0;
                if (units < 5.0 && rawUnits > 0) units = 5.0; // Enforce minimum of 5 units for valid smithed items
                return units;
            }
            else
            {
                if (voxelCount <= 42)
                {
                    return 100.0;
                }
                else
                {
                    return 200.0;
                }
            }
        }

        public static double CalculateDurabilityYield(double baseUnits, double durabilityRatio)
        {
            if (durabilityRatio < 0.0) durabilityRatio = 0.0;
            if (durabilityRatio > 1.0) durabilityRatio = 1.0;
            return Math.Floor((baseUnits * durabilityRatio) / 5.0) * 5.0;
        }
    }
}
