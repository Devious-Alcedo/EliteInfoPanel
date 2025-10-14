using System;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class CarrierJumpScheduledEventArgs : EventArgs
    {
        public DateTime DepartureTimeUtc { get; }
        public string SystemName { get; }
        public string BodyName { get; }

        public CarrierJumpScheduledEventArgs(DateTime departureTimeUtc, string systemName, string bodyName)
        {
            DepartureTimeUtc = departureTimeUtc;
            SystemName = systemName;
            BodyName = bodyName;
        }
    }

    internal sealed class CargoUpdatedEventArgs : EventArgs
    {
        public System.Collections.Generic.Dictionary<string, int> Cargo { get; }

        public CargoUpdatedEventArgs(System.Collections.Generic.Dictionary<string, int> cargo)
        {
            Cargo = cargo;
        }
    }

    internal sealed class ColonizationUpdatedEventArgs : EventArgs
    {
        public long MarketId { get; }
        public global::EliteInfoPanel.Core.Models.ColonizationData Data { get; }
        public ColonizationUpdatedEventArgs(long marketId, global::EliteInfoPanel.Core.Models.ColonizationData data)
        {
            MarketId = marketId;
            Data = data;
        }
    }

    internal sealed class RouteUpdatedEventArgs : EventArgs
    {
        public global::EliteInfoPanel.Core.Models.RouteProgressState State { get; }
        public RouteUpdatedEventArgs(global::EliteInfoPanel.Core.Models.RouteProgressState state)
        {
            State = state;
        }
    }
}
