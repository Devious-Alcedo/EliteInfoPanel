using System;
using System.IO;
using System.Text.Json;
using EliteInfoPanel.Core.Models;
using Serilog;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class RouteProgressService : IRouteProgressService
    {
        private readonly string _filePath;
        public event EventHandler<RouteUpdatedEventArgs>? RouteUpdated;
        public RouteProgressService(string filePath)
        {
            _filePath = filePath;
        }

        public RouteProgressState Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var state = JsonSerializer.Deserialize<RouteProgressState>(json) ?? new RouteProgressState();
                    RouteUpdated?.Invoke(this, new RouteUpdatedEventArgs(state));
                    return state;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load RouteProgress.json");
            }
            return new RouteProgressState();
        }

        public void Save(RouteProgressState state)
        {
            try
            {
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
                RouteUpdated?.Invoke(this, new RouteUpdatedEventArgs(state));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save RouteProgress.json");
            }
        }
    }
}
