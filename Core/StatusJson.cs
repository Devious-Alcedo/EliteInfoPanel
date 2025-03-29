using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace EliteInfoPanel.Core
{
    [Flags]
    public enum Flag : long
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
        SRVHandbrake = 1 << 12,
        SRVTurret = 1 << 13,
        SRVTurretRetracted = 1 << 14,
        LandingGearDeployed = 1 << 2,
        SRVDriveAssist = 1 << 15,
        FSDMassLocked = 1 << 16,
        MassLocked = 1 << 12,
        FSDCharging = 1 << 17,
        FSDCooldown = 1 << 18,
        LowFuel = 1 << 19,
        OverHeating = 1 << 20,
        OnFoot = 1 << 27,
        HasLatLong = 1 << 21,
        IsInDanger = 1 << 22,
        BeingInterdicted = 1 << 23,
        InMainShip = 1 << 24,
        InFighter = 1 << 25,
        InSRV = 1 << 26,
        HudInAnalysisMode = 1 << 27,
        NightVision = 1 << 28,
        AltFromAvgRad = 1 << 29,
        FSDJump = 1 << 30,
        SRVHighBeam = 1 << 31
    }

    public class StatusJson
    {
        public long Balance { get; set; }

        [JsonPropertyName("Flags")]
        public Flag Flags { get; set; }

        public FuelInfo Fuel { get; set; }

        [JsonPropertyName("Heat")]
        public float Heat { get; set; }

        public string ShipType { get; set; }

        public class FuelInfo
        {
            #region Public Properties

            public float FuelMain { get; set; }
            public float FuelReservoir { get; set; }

            #endregion Public Properties
        }
    }
}