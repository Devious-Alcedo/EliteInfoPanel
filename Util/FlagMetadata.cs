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
    // Primary Flags
    { Flag.Docked, ("Anchor", "Docked at a station", Brushes.LightGreen) },
    { Flag.Landed, ("AirplaneLanding", "Landed on planet surface", Brushes.SandyBrown) },
    { Flag.LandingGearDown, ("ArrowDownBox", "Landing gear deployed", Brushes.Orange) },
    { Flag.ShieldsUp, ("Shield", "Shields active", Brushes.CadetBlue) },
    { Flag.Supercruise, ("Rocket", "In Supercruise", Brushes.Gold) },
    { Flag.FlightAssistOff, ("AirplaneOff", "Flight assist off", Brushes.OrangeRed) },
    { Flag.HardpointsDeployed, ("SwordCross", "Hardpoints deployed", Brushes.Crimson) },
    { Flag.InWing, ("AccountGroup", "In a wing", Brushes.MediumPurple) },
    { Flag.LightsOn, ("Lightbulb", "Ship lights on", Brushes.Yellow) },
    { Flag.CargoScoopDeployed, ("InboxArrowDown", "Cargo scoop deployed", Brushes.Peru) },
    { Flag.SilentRunning, ("VolumeOff", "Silent running", Brushes.LightGray) },
    { Flag.ScoopingFuel, ("Fuel", "Scooping fuel", Brushes.Goldenrod) },
    { Flag.SrvHandbrake, ("CarBrakeHold", "SRV handbrake", Brushes.Tomato) },
    { Flag.SrvUsingTurretView, ("Target", "SRV turret active", Brushes.LightSlateGray) },
    { Flag.SrvTurretRetracted, ("EyeOff", "SRV turret retracted", Brushes.DarkSlateGray) },
    { Flag.SrvDriveAssist, ("CarCruiseControl", "SRV drive assist on", Brushes.LightGreen) },
    { Flag.FsdMassLocked, ("Lock", "Mass locked", Brushes.Red) },
    { Flag.FsdCharging, ("BatteryCharging", "FSD charging", Brushes.LightSkyBlue) },
    { Flag.FsdCooldown, ("TimerSand", "FSD cooldown", Brushes.Gray) },
    { Flag.LowFuel, ("GasStationOff", "Low fuel (< 25%)", Brushes.OrangeRed) },
    { Flag.Overheating, ("Fire", "Overheating (> 100%)", Brushes.Red) },
    { Flag.HasLatLong, ("MapMarker", "Latitude/Longitude available", Brushes.LightSeaGreen) },
    { Flag.IsInDanger, ("Alert", "In danger", Brushes.OrangeRed) },
    { Flag.BeingInterdicted, ("AccountAlert", "Being interdicted", Brushes.OrangeRed) },
    { Flag.InMainShip, ("ShipWheel", "In main ship", Brushes.White) },
    { Flag.InFighter, ("Aircraft", "In a fighter", Brushes.LightBlue) },
    { Flag.InSRV, ("Car", "In an SRV", Brushes.Gray) },
    { Flag.HudInAnalysisMode, ("Microscope", "Analysis Mode", Brushes.Aquamarine) },
    { Flag.NightVision, ("NightVision", "Night vision enabled", Brushes.DeepSkyBlue) },
    { Flag.AltitudeFromAverageRadius, ("ElevationRise", "Altitude (avg radius)", Brushes.MediumSlateBlue) },
    { Flag.FsdJump, ("FastForward", "FSD Jump in progress", Brushes.Violet) },
    { Flag.SrvHighBeam, ("CarLightHigh", "SRV high beams on", Brushes.LemonChiffon) },

    // Synthetic/special flags
    { SyntheticFlags.HudInCombatMode, ("Crosshairs", "Combat Mode", Brushes.Red) },
    { SyntheticFlags.Docking, ("Ferry", "Docking in progress", Brushes.SkyBlue) },
};

        public static bool TryGetMetadata(Flag flag, out (string Icon, string Tooltip, Brush Color) meta)
        {
            return FlagVisuals.TryGetValue(flag, out meta);
        }
        // Add this debugging method to FlagVisualHelper
        public static void LogAllFlags(StatusJson status)
        {
            if (status == null) return;

            uint rawFlags = (uint)status.Flags;
            Log.Information("🚩 Raw flag status: 0x{RawFlags:X8}", rawFlags);

            var activeFlags = new List<string>();

            foreach (Flag flag in Enum.GetValues(typeof(Flag)))
            {
                if (flag != Flag.None && status.Flags.HasFlag(flag))
                {
                    activeFlags.Add(flag.ToString());

                    // Check if this flag has metadata
                    bool hasMetadata = FlagVisuals.ContainsKey(flag);
                    Log.Debug("🚩 Active flag: {Flag}, Has Metadata: {HasMetadata}",
                        flag, hasMetadata);
                }
            }

            Log.Information("🚩 Active flags ({Count}): {Flags}",
                activeFlags.Count, string.Join(", ", activeFlags));
        }
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
