using System;
using System.Collections.Generic;
using System.IO;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Core.Services;
using Xunit;

namespace EliteInfoPanel.Tests
{
    public class ColonizationServiceTests
    {
        [Fact]
        public void LoadActive_filters_and_raises_events()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var depots = new Dictionary<long, ColonizationData>
                {
                    [1] = new ColonizationData { MarketID = 1, ConstructionComplete = false, ConstructionFailed = false, LastUpdated = DateTime.UtcNow },
                    [2] = new ColonizationData { MarketID = 2, ConstructionComplete = true, LastUpdated = DateTime.UtcNow },
                };
                File.WriteAllText(tmp, System.Text.Json.JsonSerializer.Serialize(depots));

                var svc = new ColonizationService();
                var raised = new List<long>();
                svc.ColonizationUpdated += (_, e) => raised.Add(e.MarketId);

                var active = svc.LoadActive(tmp);
                Assert.Single(active);
                Assert.Contains(1, active.Keys);
                Assert.Contains(1, raised);
                Assert.DoesNotContain(2, raised);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        [Fact]
        public void SaveSingle_raises_event()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var svc = new ColonizationService();
                long marketId = 42;
                ColonizationData data = new ColonizationData { MarketID = marketId, LastUpdated = DateTime.UtcNow };
                long seen = 0;
                svc.ColonizationUpdated += (_, e) => seen = e.MarketId;
                svc.SaveSingle(tmp, data);
                Assert.Equal(marketId, seen);
                Assert.True(new FileInfo(tmp).Length > 0);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
