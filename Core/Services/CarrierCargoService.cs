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
                if (!File.Exists(_savePath))
                {
                    // Back-compat: migrate legacy carrier_cargo_state.json if present
                    var legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "EliteInfoPanel", "carrier_cargo_state.json");
                    if (File.Exists(legacyPath))
                    {
                        try
                        {
                            using var legacyStream = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            if (legacyStream.Length > 0)
                            {
                                using var legacyReader = new StreamReader(legacyStream);
                                var legacyJson = legacyReader.ReadToEnd();
                                if (!string.IsNullOrWhiteSpace(legacyJson))
                                {
                                    var legacyData = JsonSerializer.Deserialize<Dictionary<string, int>>(legacyJson,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                                        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                                    var migrated = new Dictionary<string, int>(legacyData, StringComparer.OrdinalIgnoreCase);

                                    // Persist immediately to new path
                                    Save(migrated);
                                    Log.Information("Migrated carrier cargo state from legacy file to {NewPath}", _savePath);
                                    return migrated;
                                }
                            }
                        }
                        catch (Exception mex)
                        {
                            Log.Warning(mex, "Failed reading legacy carrier cargo state from {Legacy}", legacyPath);
                        }
                    }

                    return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }

                using var stream = new FileStream(_savePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length == 0)
                    return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return loaded != null
                    ? new Dictionary<string, int>(loaded, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
                // Clean up legacy file if present
                var legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EliteInfoPanel", "carrier_cargo_state.json");
                if (!string.Equals(legacyPath, _savePath, StringComparison.OrdinalIgnoreCase) && File.Exists(legacyPath))
                {
                    try { File.Delete(legacyPath); Log.Information("Deleted legacy carrier cargo file at {Path}", legacyPath); }
                    catch (Exception dex) { Log.Warning(dex, "Failed to delete legacy carrier cargo file at {Path}", legacyPath); }
                }
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
