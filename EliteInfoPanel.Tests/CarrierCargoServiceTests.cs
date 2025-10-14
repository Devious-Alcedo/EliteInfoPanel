using System;
using System.Collections.Generic;
using System.Text.Json;
using EliteInfoPanel.Core.Services;
using Xunit;

namespace EliteInfoPanel.Tests
{
    public class CarrierCargoServiceTests
    {
        [Fact]
        public void ApplyEvent_raises_CargoUpdated()
        {
            var tracker = new Core.CarrierCargoTracker();
            var service = new CarrierCargoService(tracker, System.IO.Path.GetTempFileName());

            Dictionary<string, int>? last = null;
            service.CargoUpdated += (_, args) => last = args.Cargo;

            var json = JsonDocument.Parse("{\"event\":\"CargoTransfer\",\"Commodity\":\"gold\",\"Count\":10}");
            service.Initialize(new Dictionary<string, int>());
            service.ApplyEvent(json.RootElement);

            Assert.NotNull(last);
            Assert.True(last!.ContainsKey("gold"));
            Assert.Equal(10, last["gold"]);
        }
    }
}
