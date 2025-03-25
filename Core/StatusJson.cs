using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
namespace EliteInfoPanel.Core
{
    [Flags]
    public enum Flag
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
        OverHeating = 1 << 20,
        // Add more flags if needed
    }
    public class StatusJson
    {
        [JsonPropertyName("Heat")]
        public float Heat { get; set; }
        [JsonPropertyName("Flags")]
        public Flag Flags { get; set; }


        public FuelInfo Fuel { get; set; }
         public long Balance { get; set; }
        public string ShipType { get; set; }
        
        public class FuelInfo
        {
            public float FuelMain { get; set; }
            public float FuelReservoir { get; set; }
        }
    }


}
