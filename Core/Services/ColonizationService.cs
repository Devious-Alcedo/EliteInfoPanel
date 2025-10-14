using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EliteInfoPanel.Core.Models;
using Serilog;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class ColonizationService : IColonizationService
    {
        public event EventHandler<ColonizationUpdatedEventArgs>? ColonizationUpdated;
        public Dictionary<long, ColonizationData> LoadActive(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Log.Debug("No colonization data file found at {File}", filePath);
                    return new Dictionary<long, ColonizationData>();
                }

                var json = File.ReadAllText(filePath);
                Dictionary<long, ColonizationData> loadedDepots = null;

                try
                {
                    loadedDepots = JsonSerializer.Deserialize<Dictionary<long, ColonizationData>>(json);
                    Log.Information("Loaded colonization data in Dictionary format");
                }
                catch (JsonException)
                {
                    try
                    {
                        var legacyList = JsonSerializer.Deserialize<List<ColonizationData>>(json);
                        if (legacyList != null)
                        {
                            loadedDepots = legacyList
                                .Where(d => d != null && d.MarketID != 0)
                                .ToDictionary(d => d.MarketID, d => d);
                            Log.Information("Loaded colonization data from legacy List format and converted to Dictionary");
                        }
                    }
                    catch (JsonException legacyEx)
                    {
                        Log.Error(legacyEx, "Failed to load colonization data in both Dictionary and List formats");
                        return new Dictionary<long, ColonizationData>();
                    }
                }

                if (loadedDepots == null)
                    return new Dictionary<long, ColonizationData>();

                var active = new Dictionary<long, ColonizationData>();
                foreach (var depot in loadedDepots.Values)
                {
                    if (!depot.ConstructionComplete && !depot.ConstructionFailed)
                    {
                        active[depot.MarketID] = depot;
                        ColonizationUpdated?.Invoke(this, new ColonizationUpdatedEventArgs(depot.MarketID, depot));
                    }
                }
                return active;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading persisted colonization data from {File}", filePath);
                return new Dictionary<long, ColonizationData>();
            }
        }

        public void SaveAllActive(string filePath, IEnumerable<ColonizationData> activeDepots)
        {
            try
            {
                var list = activeDepots?.Where(d => d != null && !d.ConstructionComplete && !d.ConstructionFailed).ToList() ?? new();
                if (list.Count == 0) return;

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Log.Information("Saved {Count} colonization depots to file", list.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data to {File}", filePath);
            }
        }

        public void SaveSingle(string filePath, ColonizationData data)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                Log.Information("Saved colonization data to file: Progress={Progress:P2}, Resources={Count}",
                    data?.ConstructionProgress ?? 0,
                    data?.ResourcesRequired?.Count ?? 0);

                if (data != null)
                {
                    ColonizationUpdated?.Invoke(this, new ColonizationUpdatedEventArgs(data.MarketID, data));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data to {File}", filePath);
            }
        }
    }
}
