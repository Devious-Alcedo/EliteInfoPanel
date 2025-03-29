using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace EliteInfoPanel.Core
{
    [Flags]
    public enum Flag : uint  // <-- Change to uint
    {
        None = 0,
        Docked = 1,
        Landed = 2,
        LandingGearDown = 4,
        ShieldsUp = 8,
        Supercruise = 16,
        FlightAssistOff = 32,
        HardpointsDeployed = 64,
        InWing = 128,
        LightsOn = 256,
        CargoScoopDeployed = 512,
        SilentRunning = 1024,
        ScoopingFuel = 2048,
        SrvHandbrake = 4096,
        SrvTurret = 8192,
        SrvUnderShip = 16384,
        SrvDriveAssist = 32768,
        FSDMassLocked = 65536,
        FSDCharging = 131072,
        FSDCooldown = 262144,
        LowFuel = 524288,
        OverHeating = 1048576,
        HasLatLong = 2097152,
        IsInDanger = 4194304,
        BeingInterdicted = 8388608,
        InMainShip = 16777216,
        InFighter = 33554432,
        InSRV = 67108864,
        HudInAnalysisMode = 134217728,
        NightVision = 268435456,
        AltitudeFromAverageRadius = 536870912,
        FSDSupercharging = 1073741824,
        FSDJump = 2147483648,
    }





    public class StatusJson
    {
        public long Balance { get; set; }

        [JsonPropertyName("Flags")]
        public Flag Flags { get; set; }
        public int Flags2 { get; set; }
        public FuelInfo Fuel { get; set; }
        public bool OnFoot => (Flags2 & 1) != 0;
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