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
                var loadedDepots = _colonizationService.LoadActive(ColonizationDataFile);
                if (loadedDepots != null)
                {
                    _colonizationDepots.Clear();

                    int skippedCount = 0;
                    foreach (var depot in loadedDepots.Values)
                    {
                        _colonizationDepots[depot.MarketID] = depot;
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
                _colonizationService.SaveAllActive(ColonizationDataFile, activeDepots);
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
                if (CurrentColonization == null) return;
                _colonizationService.SaveSingle(ColonizationDataFile, CurrentColonization);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data");
            }
        }
    }
}
