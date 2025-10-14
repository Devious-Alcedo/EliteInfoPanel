using System;
using System.IO;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Core.Services;
using Xunit;

namespace EliteInfoPanel.Tests
{
    public class RouteProgressServiceTests
    {
        [Fact]
        public void Load_and_Save_raise_events()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var svc = new RouteProgressService(tmp);
                RouteProgressState seenOnLoad = null;
                RouteProgressState seenOnSave = null;
                svc.RouteUpdated += (_, e) => { if (seenOnLoad == null) seenOnLoad = e.State; else seenOnSave = e.State; };

                // Initial save should raise RouteUpdated
                var state = new RouteProgressState();
                svc.Save(state);
                Assert.NotNull(seenOnSave);

                // Write a file to be loaded
                File.WriteAllText(tmp, System.Text.Json.JsonSerializer.Serialize(state));
                var loaded = svc.Load();
                Assert.NotNull(seenOnLoad);
                Assert.NotNull(loaded);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
