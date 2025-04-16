using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Util
{
    public static class FsdJumpRangeCalculator
    {
        // Rating Constants for FSD drives
        private static readonly Dictionary<char, double> RatingConstants = new()
        {
            { 'A', 12.0 },
            { 'B', 10.0 },
            { 'C', 8.0 },
            { 'D', 10.0 },
            { 'E', 11.0 }
        };

        // Class Constants for FSD drives
        private static readonly Dictionary<int, double> ClassConstants = new()
        {
            { 2, 2.00 },
            { 3, 2.15 },
            { 4, 2.30 },
            { 5, 2.45 },
            { 6, 2.60 },
            { 7, 2.75 },
            { 8, 2.90 }
        };

        // Base FSD specs
        private static readonly Dictionary<string, (double OptimalMass, double MaxFuelPerJump)> FsdSpecs = new()
        {
            { "2E", (200, 4.0) }, { "2D", (400, 5.0) }, { "2C", (600, 6.0) }, { "2B", (800, 7.0) }, { "2A", (1000, 8.0) },
            { "3E", (300, 4.0) }, { "3D", (600, 5.0) }, { "3C", (900, 6.0) }, { "3B", (1200, 7.0) }, { "3A", (1500, 8.0) },
            { "4E", (400, 4.0) }, { "4D", (800, 5.0) }, { "4C", (1200, 6.0) }, { "4B", (1600, 7.0) }, { "4A", (2000, 8.0) },
            { "5E", (500, 4.0) }, { "5D", (1000, 5.0) }, { "5C", (1500, 6.0) }, { "5B", (2000, 7.0) }, { "5A", (2500, 8.0) },
            { "6E", (600, 4.0) }, { "6D", (1200, 5.0) }, { "6C", (1800, 6.0) }, { "6B", (2400, 7.0) }, { "6A", (3000, 8.0) },
            { "7E", (700, 4.0) }, { "7D", (1400, 5.0) }, { "7C", (2100, 6.0) }, { "7B", (2800, 7.0) }, { "7A", (3500, 8.0) },
            { "8E", (800, 4.0) }, { "8D", (1600, 5.0) }, { "8C", (2400, 6.0) }, { "8B", (3200, 7.0) }, { "8A", (4000, 8.0) }
        };

        /// <summary>
        /// Calculates jump ranges using the game's formula and dynamically scales based on the ship's reported max jump range
        /// </summary>
        public static (double Current, double Max) CalculateJumpRanges(LoadoutModule fsd, LoadoutJson loadout, StatusJson status, CargoJson cargo)
        {
            try
            {
                // Calculate unscaled jump ranges first
                double unscaledMax = CalculateUnscaledJumpRange(fsd, loadout, status, cargo, maxFuel: true);
                double unscaledCurrent = CalculateUnscaledJumpRange(fsd, loadout, status, cargo, maxFuel: false);

                // Safety check
                if (unscaledMax <= 0 || loadout.MaxJumpRange <= 0)
                {
                    return (0, 0);
                }

                // Calculate a dynamic scaling factor based on the ship's reported max jump range
                double scalingFactor = loadout.MaxJumpRange / unscaledMax;

                // Apply the scaling factor to both values
                double scaledMax = unscaledMax * scalingFactor;
                double scaledCurrent = unscaledCurrent * scalingFactor;

                Log.Information("🚀 Jump Range: Unscaled (Max={0:0.00}, Current={1:0.00}), " +
                               "Scaling Factor={2:0.000}, " +
                               "Final (Max={3:0.00}, Current={4:0.00}), " +
                               "Game Max={5:0.00}",
                               unscaledMax, unscaledCurrent, scalingFactor, scaledMax, scaledCurrent, loadout.MaxJumpRange);

                return (scaledCurrent, scaledMax);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calculating jump ranges");
                return (0, 0);
            }
        }

        /// <summary>
        /// Calculates an unscaled jump range using the formula
        /// </summary>
        private static double CalculateUnscaledJumpRange(LoadoutModule fsd, LoadoutJson loadout, StatusJson status, CargoJson cargo, bool maxFuel)
        {
            // Parse FSD key to get size and rating
            var fsdKey = GetFsdSpecKeyFromItem(fsd.Item);
            if (string.IsNullOrEmpty(fsdKey) || !FsdSpecs.TryGetValue(fsdKey, out var specs))
                return 0;

            // Extract size and rating from key (e.g., "6A" => size=6, rating='A')
            if (!int.TryParse(fsdKey[0].ToString(), out int size))
                return 0;

            char rating = fsdKey[1];

            // Get the constants for this FSD class and rating
            if (!ClassConstants.TryGetValue(size, out double classConstant) ||
                !RatingConstants.TryGetValue(rating, out double ratingConstant))
                return 0;

            // Get optimal mass and max fuel per jump
            double optimalMass = specs.OptimalMass;
            double maxFuelPerJump = specs.MaxFuelPerJump;

            // Apply engineering modifiers if applicable
            // Apply engineering modifiers if applicable
            var optMassModifier = fsd.Engineering?.Modifiers?
                .FirstOrDefault(m => m.Label.Equals("FSDOptimalMass", StringComparison.OrdinalIgnoreCase));
            if (optMassModifier != null)
                optimalMass = optMassModifier.Value;

            // Check for jump range boost (Mass Manager, etc.)
            double jumpBoost = 0;
            var experimental = fsd.Engineering?.ExperimentalEffect?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(experimental) &&
                (experimental.Contains("mass_manager") || experimental.Contains("fsd") || experimental.Contains("range")))
            {
                jumpBoost = 1.0; // Conservative flat bonus, EDSY sometimes estimates ~1.0 LY
            }


            // Calculate the fuel used for this jump
            double fuelUsed = maxFuel
                ? maxFuelPerJump
                : Math.Min(status?.Fuel?.FuelMain ?? maxFuelPerJump, maxFuelPerJump);

            // Calculate ship mass
            double shipMass;
            if (maxFuel)
            {
                // Max jump range uses unladen mass (no cargo)
                shipMass = loadout.UnladenMass;
            }
            else
            {
                // Current jump range includes cargo and fuel
                double cargoMass = cargo?.Inventory?.Sum(i => i.Count) ?? 0;
                double fuelMass = (status?.Fuel?.FuelMain ?? 0) * 0.95;

                shipMass = loadout.UnladenMass + cargoMass + fuelMass;
            }

            // Apply the formula without any scaling
            double jumpRange = ((Math.Pow(100, 1 / classConstant) * optimalMass *
                        Math.Pow(fuelUsed / ratingConstant, 1 / classConstant)) / shipMass) + jumpBoost;


            Log.Information("⚙ {0} Jump Range → Size: {1}, Rating: {2}, ClassConst: {3}, RatingConst: {4}, " +
                          "OptMass: {5}, FuelUsed: {6}, ShipMass: {7} = {8:0.00} LY (unscaled)",
                          maxFuel ? "MAX" : "CURRENT", size, rating, classConstant, ratingConstant,
                          optimalMass, fuelUsed, shipMass, jumpRange);

            return jumpRange;
        }

        // Legacy methods for backward compatibility
        public static double CalculateMaxJumpRange(LoadoutModule fsd, LoadoutJson loadout, StatusJson status, CargoJson cargo)
        {
            var (_, max) = CalculateJumpRanges(fsd, loadout, status, cargo);
            return max;
        }
        //public static double EstimateFuelUsage(LoadoutModule fsd, LoadoutJson loadout, double distance, CargoJson? cargo = null)
        //{
        //    var fsdKey = GetFsdSpecKeyFromItem(fsd.Item);
        //    if (string.IsNullOrEmpty(fsdKey) || !FsdSpecs.TryGetValue(fsdKey, out var specs))
        //        return 0;

        //    double optimalMass = specs.OptimalMass;
        //    double maxFuelPerJump = specs.MaxFuelPerJump;

        //    var optMassModifier = fsd.Engineering?.Modifiers?
        //        .FirstOrDefault(m => m.Label.Equals("FSDOptimalMass", StringComparison.OrdinalIgnoreCase));
        //    if (optMassModifier != null)
        //        optimalMass = optMassModifier.Value;

        //    double ratingConstant = GetRatingConstant(fsdKey[1]);
        //    double exponent = GetClassExponent(fsdKey[0] - '0'); // '6' → 6

        //    if (ratingConstant == 0 || exponent == 0 || optimalMass == 0)
        //        return 0;

        //    // Estimate ship mass including cargo
        //    double cargoMass = cargo?.Inventory?.Sum(i => i.Count) ?? 0;
        //    double shipMass = loadout.UnladenMass + cargoMass;

        //    // Rearranged from the jump range formula: D = (fuel / R)^1/E * M / mass
        //    // Solve for fuel:
        //    double ratio = (distance * shipMass / optimalMass);
        //    double fuel = Math.Pow(ratio, exponent) * ratingConstant;

        //    fuel = Math.Max(0.01, fuel); // minimum usage
        //    fuel = Math.Min(fuel, maxFuelPerJump); // clamp max

        //    // Optionally apply a fudge factor to match in-game performance
        //    fuel *= 0.85;
        //    return fuel;

        //}
        public static double EstimateFuelUsage(LoadoutModule fsd, LoadoutJson loadout, double distance, CargoJson? cargo = null)
        {
            var fsdKey = GetFsdSpecKeyFromItem(fsd.Item);
            if (string.IsNullOrEmpty(fsdKey) || !FsdSpecs.TryGetValue(fsdKey, out var specs))
                return 0;

            double optimalMass = specs.OptimalMass;
            double maxFuelPerJump = specs.MaxFuelPerJump;

            var optMassModifier = fsd.Engineering?.Modifiers?
                .FirstOrDefault(m => m.Label.Equals("FSDOptimalMass", StringComparison.OrdinalIgnoreCase));
            if (optMassModifier != null)
                optimalMass = optMassModifier.Value;

            double ratingConstant = GetRatingConstant(fsdKey[1]);
            double exponent = GetClassExponent(fsdKey[0] - '0'); // '6' → 6

            if (ratingConstant == 0 || exponent == 0 || optimalMass == 0 || distance <= 0)
                return 0;

            // Estimate ship mass including cargo (not including fuel for a single-jump estimate)
            double cargoMass = cargo?.Inventory?.Sum(i => i.Count) ?? 0;
            double shipMass = loadout.UnladenMass + cargoMass;

            // Reverse the jump range formula with an empirical scaling factor
            // Original: Range = (100 ^ (1 / exp)) * OptMass / ShipMass * (fuel / rating) ^ (1 / exp)
            double baseConstant = Math.Pow(100, 1 / exponent);
            double massRatio = optimalMass / shipMass;

            // Estimate fuel usage using reverse formula:
            double fuel = ratingConstant * Math.Pow(distance / (baseConstant * massRatio), exponent);

            // Clamp and curve-match
            fuel = Math.Clamp(fuel, 0.1, maxFuelPerJump);

            // Apply efficiency fudge factor (~0.88 gets very close to in-game observed values)
            fuel *= 0.88;

            return Math.Round(fuel, 2);
        }

        private static double GetRatingConstant(char rating)
        {
            return RatingConstants.TryGetValue(rating, out double value) ? value : 0;
        }

        private static double GetClassExponent(int size)
        {
            return ClassConstants.TryGetValue(size, out double value) ? value : 0;
        }

        public static double CalculateCurrentJumpRange(LoadoutModule fsd, LoadoutJson loadout, StatusJson status, CargoJson cargo)
        {
            var (current, _) = CalculateJumpRanges(fsd, loadout, status, cargo);
            return current;
        }

        private static int ExtractSizeFromItem(string item)
        {
            var match = Regex.Match(item.ToLower(), @"size(?<size>\d+)");
            return match.Success ? int.Parse(match.Groups["size"].Value) : 0;
        }

        public static string? GetFsdSpecKeyFromItem(string item)
        {
            if (string.IsNullOrEmpty(item)) return null;

            // Match size and class from FSD module strings
            var match = Regex.Match(item.ToLower(), @"size(?<size>\d+)_class(?<class>\d+)");
            if (match.Success)
            {
                string size = match.Groups["size"].Value;
                string classNum = match.Groups["class"].Value;

                string rating = classNum switch
                {
                    "1" => "E",
                    "2" => "D",
                    "3" => "C",
                    "4" => "B",
                    "5" => "A",
                    _ => null
                };

                if (rating != null)
                    return $"{size}{rating}";
            }

            return null;
        }
    }
}