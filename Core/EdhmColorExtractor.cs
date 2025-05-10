using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace EliteInfoPanel.Core
{
    public class EdhmColorExtractor
    {
        public readonly string AdvancedIniPath;
        private readonly Dictionary<string, float> _settings = new();

        public EdhmColorExtractor()
        {
            AdvancedIniPath = FindEdhmAdvancedIni();
        }

        public EliteHudColors ExtractColors()
        {
            try
            {
                if (!File.Exists(AdvancedIniPath))
                {
                    Log.Warning("EDHM Advanced.ini not found at {Path}", AdvancedIniPath);
                    return GetDefaultColors();
                }

                // Parse the INI file
                ParseAdvancedIni();

                // Extract the HUD colors based on EDHM settings
                return CalculateHudColors();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract HUD colors from EDHM Advanced.ini");
                return GetDefaultColors();
            }
        }

        private void ParseAdvancedIni()
        {
            var lines = File.ReadAllLines(AdvancedIniPath);

            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";"))
                    continue;

                // Parse key-value pairs (e.g., "x77 =0.4793")
                var match = Regex.Match(line, @"^([xyz]\d+|w\d+)\s*=\s*([0-9\.-]+)");
                if (match.Success)
                {
                    string key = match.Groups[1].Value;
                    if (float.TryParse(match.Groups[2].Value, out float value))
                    {
                        _settings[key] = value;
                    }
                }
            }

            Log.Information("Parsed {Count} EDHM settings from Advanced.ini", _settings.Count);
        }

        private EliteHudColors CalculateHudColors()
        {
            var colors = new EliteHudColors();

            // Orange Text Color Swap (main HUD text)
            if (TryGetRgb("x77", "y77", "z77", out Color mainText))
            {
                colors.HudText = mainText;
                colors.HudMain = mainText;
            }

            // Blue Text Color Swap (secondary text)
            if (TryGetRgb("x82", "y82", "z82", out Color blueText))
            {
                colors.HudSecondary = blueText;
            }

            // Shield Up Colour
            if (TryGetRgb("x228", "y228", "z228", out Color shieldUp))
            {
                // You can use this for other UI elements
            }

            // Warning colors (Speeding Warning)
            if (TryGetRgb("x172", "y172", "z172", out Color warning))
            {
                colors.HudWarning = warning;
            }

            // Success/Fuel Scoop color
            if (TryGetRgb("x145", "y145", "z145", out Color success))
            {
                colors.HudSuccess = success;
            }

            // Background color (darker radar color)
            if (TryGetRgb("x237", "y237", "z237", out Color radar))
            {
                colors.HudBackground = Color.FromRgb(
                    (byte)(radar.R * 0.3),
                    (byte)(radar.G * 0.3),
                    (byte)(radar.B * 0.3));
            }

            return colors;
        }

        private bool TryGetRgb(string redKey, string greenKey, string blueKey, out Color color)
        {
            if (_settings.TryGetValue(redKey, out float r) &&
                _settings.TryGetValue(greenKey, out float g) &&
                _settings.TryGetValue(blueKey, out float b))
            {
                // Convert from float (0-1) to byte (0-255)
                color = Color.FromRgb(
                    (byte)(Math.Clamp(r, 0, 1) * 255),
                    (byte)(Math.Clamp(g, 0, 1) * 255),
                    (byte)(Math.Clamp(b, 0, 1) * 255));
                return true;
            }

            color = Colors.Black;
            return false;
        }

        private string FindEdhmAdvancedIni()
        {
            // Try common installation paths
            var possiblePaths = new[]
            {
                // Steam location
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\EDHM-ini\Advanced.ini"),
                
                // Direct install location
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Elite Dangerous\Products\elite-dangerous-odyssey-64\EDHM-ini\Advanced.ini"),
                
                // Epic Games location
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Epic Games\EliteDangerous\Products\elite-dangerous-odyssey-64\EDHM-ini\Advanced.ini"),
                
                // Frontier Store location
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Frontier\EDLaunch\Products\elite-dangerous-odyssey-64\EDHM-ini\Advanced.ini"),
                
                // Custom location
                Path.Combine(@"C:\Program Files (x86)\Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\EDHM-ini\Advanced.ini")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Log.Information("Found EDHM Advanced.ini at: {Path}", path);
                    return path;
                }
            }

            Log.Information("Could not find EDHM Advanced.ini, will use default colors");
            return string.Empty;
        }

        private EliteHudColors GetDefaultColors()
        {
            // Return default Elite orange theme
            return new EliteHudColors
            {
                HudMain = Colors.Orange,
                HudSecondary = Color.FromRgb(204, 95, 0),
                HudText = Color.FromRgb(255, 140, 0),
                HudBackground = Colors.Black,
                HudWarning = Colors.Red,
                HudSuccess = Colors.LimeGreen
            };
        }
    }
}