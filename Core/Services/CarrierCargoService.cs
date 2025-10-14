using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EliteInfoPanel.Core.Models;
using Serilog;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class CarrierCargoService : ICarrierCargoService
    {
        private readonly CarrierCargoTracker _tracker;
        private readonly object _lock = new();
        private readonly string _savePath;

        public event EventHandler<CargoUpdatedEventArgs>? CargoUpdated;

        public CarrierCargoService(CarrierCargoTracker tracker, string savePath)
        {
            _tracker = tracker;
            _savePath = savePath;
        }

        public Dictionary<string, int> Load()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    var json = File.ReadAllText(_savePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    return loaded ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load carrier cargo from {Path}", _savePath);
            }
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public void Save(Dictionary<string, int> cargo)
        {
            try
            {
                var dir = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(cargo, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save carrier cargo to {Path}", _savePath);
            }
        }

        public void Initialize(Dictionary<string, int> cargo)
        {
            lock (_lock)
            {
                _tracker.Initialize(cargo);
                CargoUpdated?.Invoke(this, new CargoUpdatedEventArgs(new Dictionary<string, int>(_tracker.Cargo)));
            }
        }

        public void Normalize()
        {
            lock (_lock)
            {
                _tracker.NormalizeCargoKeys();
                CargoUpdated?.Invoke(this, new CargoUpdatedEventArgs(new Dictionary<string, int>(_tracker.Cargo)));
            }
        }

        public Dictionary<string, int> GetState()
        {
            lock (_lock)
            {
                return new Dictionary<string, int>(_tracker.Cargo, StringComparer.OrdinalIgnoreCase);
            }
        }

        public void ApplyEvent(JsonElement root)
        {
            lock (_lock)
            {
                _tracker.Process(root);
                CargoUpdated?.Invoke(this, new CargoUpdatedEventArgs(new Dictionary<string, int>(_tracker.Cargo)));
            }
        }
    }
}
