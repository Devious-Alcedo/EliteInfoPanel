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
                    using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length == 0) return new RouteProgressState();
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json)) return new RouteProgressState();
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
                // Ensure directory exists for the target file path
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

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
