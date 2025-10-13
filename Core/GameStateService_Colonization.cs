using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EliteInfoPanel.Core.Models;
using Serilog;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        public List<ColonizationData> GetActiveColonizationDepots()
        {
            return _colonizationDepots.Values
                .Where(d => !d.ConstructionComplete && !d.ConstructionFailed)
                .OrderBy(d => d.MarketID)
                .ToList();
        }

        public void RemoveColonizationDepot(long marketId)
        {
            try
            {
                Log.Information("Removing colonization depot {MarketID}", marketId);

                if (!_colonizationDepots.ContainsKey(marketId))
                {
                    Log.Warning("Depot {MarketID} not found in colonization depots", marketId);
                    return;
                }

                _colonizationDepots.Remove(marketId);

                if (_selectedDepotMarketId == marketId)
                {
                    _selectedDepotMarketId = _colonizationDepots.Keys.FirstOrDefault();
                    OnPropertyChanged(nameof(SelectedColonizationDepot));
                }

                OnPropertyChanged(nameof(ColonizationDepots));
                OnPropertyChanged(nameof(CurrentColonization));

                SaveAllColonizationData();

                Log.Information("Colonization depot {MarketID} removed successfully", marketId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error removing colonization depot {MarketID}", marketId);
            }
        }

        public void UpdateColonizationDepot(long marketId, ColonizationData data)
        {
            _colonizationDepots[marketId] = data;

            if (!_selectedDepotMarketId.HasValue || _selectedDepotMarketId.Value == marketId)
            {
                _selectedDepotMarketId = marketId;
            }

            OnPropertyChanged(nameof(ColonizationDepots));
            OnPropertyChanged(nameof(CurrentColonization));
            SaveAllColonizationData();
        }

        private void LoadPersistedColonizationData()
        {
            try
            {
                if (!File.Exists(ColonizationDataFile))
                {
                    Log.Debug("No colonization data file found at {File}", ColonizationDataFile);
                    return;
                }

                var json = File.ReadAllText(ColonizationDataFile);
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
                        return;
                    }
                }

                if (loadedDepots != null)
                {
                    _colonizationDepots.Clear();

                    int skippedCount = 0;
                    foreach (var depot in loadedDepots.Values)
                    {
                        if (!depot.ConstructionComplete && !depot.ConstructionFailed)
                        {
                            _colonizationDepots[depot.MarketID] = depot;
                        }
                        else
                        {
                            skippedCount++;
                            Log.Information("Skipping completed/failed depot {MarketID} during load (Complete: {Complete}, Failed: {Failed})",
                                depot.MarketID, depot.ConstructionComplete, depot.ConstructionFailed);
                        }
                    }

                    Log.Information("Loaded {Count} active colonization depots (skipped {Skipped} completed/failed)",
                        _colonizationDepots.Count, skippedCount);

                    if (_colonizationDepots.Any())
                    {
                        if (!_selectedDepotMarketId.HasValue || !_colonizationDepots.ContainsKey(_selectedDepotMarketId.Value))
                        {
                            _selectedDepotMarketId = _colonizationDepots.Keys.First();
                        }
                    }
                    else
                    {
                        _selectedDepotMarketId = null;
                    }

                    OnPropertyChanged(nameof(ColonizationDepots));
                    OnPropertyChanged(nameof(CurrentColonization));
                    OnPropertyChanged(nameof(SelectedColonizationDepot));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading persisted colonization data");
            }
        }

        private void SaveAllColonizationData()
        {
            try
            {
                var activeDepots = GetActiveColonizationDepots();
                if (!activeDepots.Any())
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(ColonizationDataFile));

                string json = JsonSerializer.Serialize(activeDepots, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ColonizationDataFile, json);

                Log.Information("Saved {Count} colonization depots to file", activeDepots.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data");
            }
        }

        private void SaveColonizationData()
        {
            try
            {
                if (CurrentColonization == null)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(ColonizationDataFile));

                string json = JsonSerializer.Serialize(CurrentColonization, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ColonizationDataFile, json);

                Log.Information("Saved colonization data to file: Progress={Progress:P2}, Resources={Count}",
                    CurrentColonization.ConstructionProgress,
                    CurrentColonization.ResourcesRequired?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data");
            }
        }
    }
}
