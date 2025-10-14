using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Serilog;
using EliteInfoPanel.Core.Models;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        public void PruneCompletedRouteSystems()
        {
            if (CurrentRoute?.Route == null || string.IsNullOrWhiteSpace(CurrentSystem))
                return;

            int index = CurrentRoute.Route.FindIndex(j =>
                string.Equals(j.StarSystem, CurrentSystem, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                Log.Information("?? Pruning route - current system is {0}, removing {1} previous entries",
                    CurrentSystem, index);

                var updatedRoute = new NavRouteJson
                {
                    Route = CurrentRoute.Route.Skip(index).ToList()
                };

                CurrentRoute = updatedRoute;
                OnPropertyChanged(nameof(TotalRemainingJumps));
            }
        }

        private void SaveRouteProgress()
        {
            try
            {
                _routeProgressService.Save(_routeProgress);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save RouteProgress.json");
            }
        }

        private void LoadRouteProgress()
        {
            try
            {
                _routeProgress = _routeProgressService.Load();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load RouteProgress.json");
            }
        }
    }
}
