using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EliteInfoPanel.Core.Services;
using Xunit;

namespace EliteInfoPanel.Tests
{
    public class JournalReaderTests
    {
        [Fact]
        public async Task ReadEventsAsync_skips_heavy_historic_and_raises_push()
        {
            var temp = Path.GetTempFileName();
            try
            {
                var tsOld = DateTime.UtcNow.AddDays(-1).ToString("o");
                var tsNow = DateTime.UtcNow.ToString("o");
                var lines = new[]
                {
                    $"{{\"timestamp\":\"{tsOld}\",\"event\":\"MarketBuy\"}}",
                    $"{{\"timestamp\":\"{tsNow}\",\"event\":\"Commander\",\"Name\":\"CMDR Test\"}}"
                };
                await File.WriteAllLinesAsync(temp, lines);

                var reader = new JournalReader();
                int pushCount = 0;
                reader.JournalEventReceived += (evt, root) =>
                {
                    if (evt == "Commander" && root.TryGetProperty("Name", out var n) && n.GetString() == "CMDR Test")
                        Interlocked.Increment(ref pushCount);
                };

                await reader.StartAsync(temp, 0, DateTime.UtcNow, isInitialScan: false, CancellationToken.None);
                await Task.Delay(200);
                reader.Stop();

                // Pull path should yield one event
                var list = await reader.ReadEventsAsync(temp, 0, DateTime.UtcNow, false).ToListAsync();
                Assert.Single(list);
                Assert.Equal("Commander", list[0].Root.GetProperty("event").GetString());
                Assert.True(pushCount >= 1);
            }
            finally
            {
                try { File.Delete(temp); } catch { }
            }
        }
    }
}
