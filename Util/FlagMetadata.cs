using EliteInfoPanel.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EliteInfoPanel.Util
{
    public static class FlagVisualHelper
    {
        public static Dictionary<Flag, (string Icon, string Tooltip, Brush Color)> FlagVisuals = new()
            {
                { Flag.Docked, ("Anchor", "Docked at a station", Brushes.LightGreen) },

                { Flag.Landed, ("AirplaneLanding", "Landed on surface", Brushes.SandyBrown) },
                { Flag.LandingGearDown, ("ArrowDownBox", "Landing gear deployed", Brushes.Orange) },
                { Flag.ShieldsUp, ("Shield", "Shields active", Brushes.CadetBlue) },
                { Flag.FlightAssistOff, ("AirplaneOff", "Flight assist off", Brushes.OrangeRed) },
                { Flag.HardpointsDeployed, ("SwordCross", "Hardpoints deployed", Brushes.Crimson) },
                { Flag.InWing, ("AccountGroup", "In a wing", Brushes.MediumPurple) },
                { Flag.LightsOn, ("LightbulbOn", "Ship lights on", Brushes.Goldenrod) },
                { Flag.CargoScoopDeployed, ("InboxArrowDown", "Cargo scoop deployed", Brushes.Peru) },
                { Flag.SilentRunning, ("VolumeOff", "Silent running", Brushes.LightGray) },
                { Flag.ScoopingFuel, ("Fuel", "Scooping fuel", Brushes.Goldenrod) },
                { Flag.SrvHandbrake, ("PauseCircle", "SRV handbrake", Brushes.Tomato) },
                { Flag.SrvUsingTurretView, ("Target", "SRV turret active", Brushes.LightSlateGray) },
                { Flag.SrvTurretRetracted, ("EyeOff", "SRV turret retracted", Brushes.DarkSlateGray) },
                { Flag.SrvDriveAssist, ("CarCruiseControl", "SRV drive assist on", Brushes.LightGreen) },
                { Flag.FsdMassLocked, ("Lock", "Mass locked", Brushes.Red) },
                { Flag.FsdCharging, ("BatteryCharging", "FSD charging", Brushes.LightSkyBlue) },
                { Flag.FsdCooldown, ("TimerSand", "FSD cooldown", Brushes.Gray) },
                { Flag.FsdJump, ("FastForward", "Jumping", Brushes.Violet) },
                { Flag.Supercruise, ("Rocket", "In Supercruise", Brushes.Gold) },
                { Flag.LowFuel, ("GasStationOff", "Low fuel", Brushes.OrangeRed) },
                { Flag.Overheating, ("Fire", "Overheating!", Brushes.Red) },
                { Flag.HasLatLong, ("MapMarker", "Latitude/Longitude available", Brushes.LightSeaGreen) },
                { Flag.IsInDanger, ("Alert", "In danger", Brushes.OrangeRed) },
                { Flag.BeingInterdicted, ("AccountAlert", "Being interdicted", Brushes.OrangeRed) },
                { Flag.InMainShip, ("ShipWheel", "In main ship", Brushes.White) },
                { Flag.InFighter, ("Aircraft", "In a fighter", Brushes.LightBlue) },
                { Flag.InSRV, ("Car", "In an SRV", Brushes.Gray) },
                { Flag.HudInAnalysisMode, ("Eyedropper", "Analysis Mode", Brushes.Aquamarine) },
                { Flag.NightVision, ("Eye", "Night vision enabled", Brushes.DeepSkyBlue) },
                { Flag.AltitudeFromAverageRadius, ("ElevationRise", "Altitude (avg radius)", Brushes.MediumSlateBlue) },
                { Flag.SrvHighBeam, ("HighDefinition", "SRV high beams on", Brushes.LemonChiffon) },

                // Synthetic/special flags
                { Flag.HudInCombatMode, ("Crosshairs", "Combat Mode", Brushes.Red) },
                { Flag.Docking, ("Ferry", "Docking in progress", Brushes.SkyBlue) },
            };
        public static bool TryGetMetadata(Flag flag, out (string Icon, string Tooltip, Brush Color) meta)
        {
            return FlagVisuals.TryGetValue(flag, out meta);
        }
        // Add this debugging method to FlagVisualHelper
        public static void LogAllMetadata()
        {
            Log.Information("🚩 Flag metadata check:");

            foreach (var flag in Enum.GetValues(typeof(Flag)).Cast<Flag>().Where(f => f != Flag.None))
            {
                bool hasMetadata = TryGetMetadata(flag, out var meta);
                Log.Information("🚩 Flag: {Flag}, Has Metadata: {HasMetadata}, Icon: {Icon}, Color: {Color}",
                    flag, hasMetadata, hasMetadata ? meta.Icon : "None", hasMetadata ? meta.Color : "None");
            }

            // Also check synthetic flags
            foreach (var flag in SyntheticFlags.All)
            {
                bool hasMetadata = TryGetMetadata(flag, out var meta);
                Log.Information("🚩 Synthetic Flag: {Flag}, Has Metadata: {HasMetadata}, Icon: {Icon}, Color: {Color}",
                    flag, hasMetadata, hasMetadata ? meta.Icon : "None", hasMetadata ? meta.Color : "None");
            }
        }

    }



}
