using System;
using System.Collections.Generic;
using System.Linq;
using EliteInfoPanel.Core.EliteInfoPanel.Core;


namespace EliteInfoPanel.Util
{
    public static class FsdJumpRangeCalculator
    {
        private static readonly Dictionary<string, (double OptimalMass, double MaxFuelPerJump)> FsdSpecs = new()
        {
            { "5A", (OptimalMass: 1320, MaxFuelPerJump: 8.0) },
            { "6A", (OptimalMass: 2160, MaxFuelPerJump: 8.0) },
            { "7A", (OptimalMass: 3600, MaxFuelPerJump: 8.0) },
            // Add more as needed
        };

        public static double EstimateMaxJumpRange(LoadoutJson loadout)
        {
            var fsd = loadout.Modules?.FirstOrDefault(m => m.Slot?.Contains("FrameShiftDrive", StringComparison.OrdinalIgnoreCase) == true);
            if (fsd == null || fsd.Class <= 0 || string.IsNullOrEmpty(fsd.Rating))
                return 0;

            string fsdKey = $"{fsd.Class}{fsd.Rating.ToUpper()}";
            if (!FsdSpecs.TryGetValue(fsdKey, out var specs))
                return 0;

            double mass = loadout.HullValue + loadout.ModulesValue + loadout.CargoCapacity;
            double fuelPerJump = specs.MaxFuelPerJump;
            double optimalMass = specs.OptimalMass;

            // Simple jump range estimation formula
            return 2.0 * (fuelPerJump / optimalMass) * Math.Pow(mass / 1000.0, 0.5) * 100;
        }
    }
}
