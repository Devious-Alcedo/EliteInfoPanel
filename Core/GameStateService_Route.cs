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
        private System.Timers.Timer _routeSaveTimer;

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

                // Capture systems being pruned for progress
                var completed = CurrentRoute.Route.Take(index)
                    .Select(s => s.StarSystem)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                var updatedRoute = new NavRouteJson
                {
                    Route = CurrentRoute.Route.Skip(index).ToList()
                };

                CurrentRoute = updatedRoute;
                OnPropertyChanged(nameof(TotalRemainingJumps));

                // Update route progress state and persist
                if (completed.Count > 0)
                {
                    foreach (var sys in completed)
                    {
                        if (!_routeProgress.CompletedSystems.Contains(sys, StringComparer.OrdinalIgnoreCase))
                        {
                            _routeProgress.CompletedSystems.Add(sys);
                        }
                    }
                }
                _routeProgress.LastKnownSystem = CurrentSystem;
                SaveRouteProgressDebounced();
            }
        }

        private void SaveRouteProgressDebounced()
        {
            try
            {
                if (_routeSaveTimer == null)
                {
                    _routeSaveTimer = new System.Timers.Timer(1000) { AutoReset = false };
                    _routeSaveTimer.Elapsed += (s, e) =>
                    {
                        try { _routeProgressService.Save(_routeProgress); }
                        catch (Exception ex) { Log.Warning(ex, "Failed to save RouteProgress.json (debounced)"); }
                    };
                }
                _routeSaveTimer.Stop();
                _routeSaveTimer.Start();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to schedule debounced RouteProgress save");
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
                // Normalize any duplicates to avoid case variations over time
                if (_routeProgress?.CompletedSystems != null)
                {
                    _routeProgress.CompletedSystems = _routeProgress.CompletedSystems
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load RouteProgress.json");
            }
        }

        // Exposed for app shutdown to guarantee persistence
        public void FlushRouteProgressSave()
        {
            try
            {
                if (_routeSaveTimer != null)
                {
                    _routeSaveTimer.Stop();
                    _routeSaveTimer.Dispose();
                    _routeSaveTimer = null;
                }
                SaveRouteProgress();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to flush RouteProgress save");
            }
        }
    }
}
