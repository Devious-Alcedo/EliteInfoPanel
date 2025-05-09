using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace EliteInfoPanel.Core
{
    [Flags]
    public enum Flag : uint
    {
        None = 0,
        Docked = 1 << 0,
        Landed = 1 << 1,
        LandingGearDown = 1 << 2,
        ShieldsUp = 1 << 3,
        Supercruise = 1 << 4,
        FlightAssistOff = 1 << 5,
        HardpointsDeployed = 1 << 6,
        InWing = 1 << 7,
        LightsOn = 1 << 8,
        CargoScoopDeployed = 1 << 9,
        SilentRunning = 1 << 10,
        ScoopingFuel = 1 << 11,
        SrvHandbrake = 1 << 12,
        SrvUsingTurretView = 1 << 13,
        SrvTurretRetracted = 1 << 14,
        SrvDriveAssist = 1 << 15,
        FsdMassLocked = 1 << 16,
        FsdCharging = 1 << 17,
        FsdCooldown = 1 << 18,
        LowFuel = 1 << 19,
        Overheating = 1 << 20,
        HasLatLong = 1 << 21,
        IsInDanger = 1 << 22,
        BeingInterdicted = 1 << 23,
        InMainShip = 1 << 24,
        InFighter = 1 << 25,
        InSRV = 1 << 26,
        HudInAnalysisMode = 1 << 27,
        NightVision = 1 << 28,
        AltitudeFromAverageRadius = 1 << 29,
        FsdJump = 1 << 30,
        SrvHighBeam = 1u << 31,
        HudInCombatMode = 1u << 31 | 1u << 30, // just a synthetic high value
        Docking = 1u << 13 | 1u << 8 | 1u << 7 | 1u << 6 | 1u << 5 | 1u << 3 | 1u << 1

    }

    [Flags]
    public enum Flags2 : uint
    {
        None = 0,
        OnFoot = 1 << 0,
        InTaxi = 1 << 1,
        InMulticrew = 1 << 2,
        OnFootInStation = 1 << 3,
        OnFootOnPlanet = 1 << 4,
        AimDownSight = 1 << 5,
        LowOxygen = 1 << 6,
        LowHealth = 1 << 7,
        Cold = 1 << 8,
        Hot = 1 << 9,
        VeryCold = 1 << 10,
        VeryHot = 1 << 11,
        GlideMode = 1 << 12,
        OnFootInHangar = 1 << 13,
        OnFootSocialSpace = 1 << 14,
        OnFootExterior = 1 << 15,
        BreathableAtmosphere = 1 << 16,
        TelepresenceMulticrew = 1 << 17,
        PhysicalMulticrew = 1 << 18,
        FsdHyperdriveCharging = 1 << 19
    }
    public static class Flags2VisualHelper
    {
        public static Dictionary<Flags2, (string Icon, string Tooltip, Brush Color)> Flags2Visuals = new()
    {
        { Flags2.OnFoot, ("Walk", "On foot", Brushes.LightBlue) },
        { Flags2.InTaxi, ("Taxi", "In Taxi/Dropship/Shuttle", Brushes.Orange) },
        { Flags2.InMulticrew, ("AccountMultiple", "In Multicrew", Brushes.MediumPurple) },
        { Flags2.OnFootInStation, ("Building", "On foot in station", Brushes.LightSteelBlue) },
        { Flags2.OnFootOnPlanet, ("Earth", "On foot on planet", Brushes.SandyBrown) },
        { Flags2.AimDownSight, ("CrosshairsGps", "Aim down sight", Brushes.Red) },
        { Flags2.LowOxygen, ("GasCylinder", "Low oxygen", Brushes.CornflowerBlue) },
        { Flags2.LowHealth, ("Heart", "Low health", Brushes.Red) },
        { Flags2.Cold, ("Snowflake", "Cold", Brushes.LightBlue) },
        { Flags2.Hot, ("WeatherSunny", "Hot", Brushes.Orange) },
        { Flags2.VeryCold, ("SnowflakeAlert", "Very cold", Brushes.DeepSkyBlue) },
        { Flags2.VeryHot, ("Fire", "Very hot", Brushes.Red) },
        { Flags2.GlideMode, ("AirplaneTakeoff", "Glide mode", Brushes.CornflowerBlue) },
        { Flags2.OnFootInHangar, ("Garage", "On foot in hangar", Brushes.DarkGray) },
        { Flags2.OnFootSocialSpace, ("AccountGroup", "On foot in social space", Brushes.MediumAquamarine) },
        { Flags2.OnFootExterior, ("Earth", "On foot exterior", Brushes.Tan) },
        { Flags2.BreathableAtmosphere, ("WeatherWindy", "Breathable atmosphere", Brushes.LightGreen) },
        { Flags2.TelepresenceMulticrew, ("Remote", "Telepresence multicrew", Brushes.MediumPurple) },
        { Flags2.PhysicalMulticrew, ("Account", "Physical multicrew", Brushes.MediumPurple) },
        { Flags2.FsdHyperdriveCharging, ("RocketLaunch", "FSD hyperdrive charging", Brushes.Violet) }
    };

        public static bool TryGetMetadata(Flags2 flag, out (string Icon, string Tooltip, Brush Color) meta)
        {
            return Flags2Visuals.TryGetValue(flag, out meta);
        }
    }
    public class SRVStatus
    {
        public double Fuel { get; set; }
        public double Temp { get; set; }
    }


    public class DestinationInfo
    {
        public long System { get; set; }
        public int Body { get; set; }
        public string Name { get; set; }
        
    }

    public class StatusJson
    {
        public long Balance { get; set; }
        public DestinationInfo Destination { get; set; }
        [JsonPropertyName("Flags")]
        public Flag Flags { get; set; }
        public int Flags2 { get; set; }
        public FuelInfo Fuel { get; set; }
        public bool OnFoot => (Flags2 & 1) != 0;
        [JsonPropertyName("Heat")]
        public float Heat { get; set; }
        public SRVStatus SRV { get; set; }
        public string ShipType { get; set; }

        public class FuelInfo
        {
            #region Public Properties

            public float FuelMain { get; set; }
            public float FuelReservoir { get; set; }
            [JsonPropertyName("FuelCapacity")]
            public double FuelCapacity { get; set; } // ← Add this if not present
            #endregion Public Properties
        }
    }
}