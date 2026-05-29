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

        public static double CalculateDurabilityYield(double baseUnits, double durabilityRatio, double lossPercentage = 5.0)
        {
            if (durabilityRatio < 0.0) durabilityRatio = 0.0;
            if (durabilityRatio > 1.0) durabilityRatio = 1.0;

            double originalValue = baseUnits * durabilityRatio;
            double lossyValue = originalValue * (1.0 - lossPercentage / 100.0);
            double roundedValue = Math.Floor(lossyValue / 5.0) * 5.0;

            if (roundedValue < 5.0)
            {
                return (5.0 <= originalValue) ? 5.0 : 0.0;
            }
            return roundedValue;
        }

        public static string GetRecipePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            int colonIndex = path.IndexOf(':');
            string domain = "";
            string cleanPath = path;
            if (colonIndex >= 0)
            {
                domain = path.Substring(0, colonIndex + 1);
                cleanPath = path.Substring(colonIndex + 1);
            }

            // If it's already a workitem, arrowhead, or contains head/blade (but doesn't start with blade-), return as is
            if (cleanPath.StartsWith("workitem-") || 
                cleanPath.Contains("head-") || 
                cleanPath.Contains("arrowhead") ||
                (cleanPath.Contains("blade-") && !cleanPath.StartsWith("blade-")))
            {
                return path;
            }

            // Map fully assembled tools to their smithed components
            string[] toolNames = { "pickaxe", "axe", "shovel", "hoe", "scythe", "hammer", "spear" };
            foreach (var tool in toolNames)
            {
                if (cleanPath.StartsWith(tool + "-"))
                {
                    return domain + cleanPath.Replace(tool + "-", tool + "head-");
                }
            }

            // Map knife-copper -> knifeblade-copper
            if (cleanPath.StartsWith("knife-"))
            {
                return domain + cleanPath.Replace("knife-", "knifeblade-");
            }

            // Map sword-copper -> swordblade-copper
            if (cleanPath.StartsWith("sword-"))
            {
                return domain + cleanPath.Replace("sword-", "swordblade-");
            }

            // Map blade-falx-copper -> bladehead-falx-copper
            if (cleanPath.StartsWith("blade-"))
            {
                return domain + cleanPath.Replace("blade-", "bladehead-");
            }

            return path;
        }
    }
}
