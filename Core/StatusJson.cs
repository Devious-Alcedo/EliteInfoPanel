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
        Docked = 1 << 0,                    // 1
        Landed = 1 << 1,                    // 2
        LandingGearDown = 1 << 2,           // 4
        ShieldsUp = 1 << 3,                 // 8
        Supercruise = 1 << 4,               // 16
        FlightAssistOff = 1 << 5,           // 32
        HardpointsDeployed = 1 << 6,        // 64
        InWing = 1 << 7,                    // 128
        LightsOn = 1 << 8,                  // 256
        CargoScoopDeployed = 1 << 9,        // 512
        SilentRunning = 1 << 10,            // 1024
        ScoopingFuel = 1 << 11,             // 2048
        SRVHandbrake = 1 << 12,             // 4096
        SRVUsingTurretView = 1 << 13,       // 8192
        SRVTurretRetracted = 1 << 14,       // 16384
        SRVDriveAssist = 1 << 15,           // 32768
        FSDMassLocked = 1 << 16,            // 65536
        FSDCharging = 1 << 17,              // 131072
        FSDCooldown = 1 << 18,              // 262144
        LowFuel = 1 << 19,                  // 524288
        OverHeating = 1 << 20,              // 1048576
        HasLatLong = 1 << 21,               // 2097152
        IsInDanger = 1 << 22,               // 4194304
        BeingInterdicted = 1 << 23,         // 8388608
        InMainShip = 1 << 24,               // 16777216
        InFighter = 1 << 25,                // 33554432
        InSRV = 1 << 26,                    // 67108864
        OnFoot = 1 << 27,                   // 134217728
        HudInAnalysisMode = 1 << 28,        // 268435456
        NightVision = 1 << 29,              // 536870912
        AltitudeFromAverageRadius = 1 << 30,// 1073741824
        FSDJump = 1L << 31,                 // 2147483648
        SRVHighBeam = 1L << 32              // 4294967296
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