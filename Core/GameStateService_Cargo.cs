using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        private void UpdateShipCargoFromTransfers(JsonElement root)
        {
            if (!_cargoTrackingInitialized || CurrentCargo?.Inventory == null) return;
            if (root.TryGetProperty("Transfers", out var transfers)) return;

            var updatedInventory = new List<CargoJson.CargoItem>(CurrentCargo.Inventory);
            bool cargoChanged = false;

            foreach (var transfer in transfers.EnumerateArray())
            {
                if (transfer.TryGetProperty("Type", out var typeProp) &&
                    transfer.TryGetProperty("Count", out var countProp) &&
                    transfer.TryGetProperty("Direction", out var directionProp))
                {
                    string internalName = typeProp.GetString();
                    int count = countProp.GetInt32();
                    string direction = directionProp.GetString();

                    if (string.IsNullOrWhiteSpace(internalName)) continue;

                    var existingItem = updatedInventory.FirstOrDefault(i =>
                        string.Equals(i.Name, internalName, StringComparison.OrdinalIgnoreCase));

                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        if (existingItem != null)
                        {
                            int newCount = Math.Max(0, existingItem.Count - count);
                            if (newCount > 0)
                            {
                                existingItem.Count = newCount;
                            }
                            else
                            {
                                updatedInventory.Remove(existingItem);
                            }
                            cargoChanged = true;
                        }
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        if (existingItem != null)
                        {
                            existingItem.Count += count;
                        }
                        else
                        {
                            updatedInventory.Add(new CargoJson.CargoItem
                            {
                                Name = internalName,
                                Count = count,
                                Value = 0
                            });
                        }
                        cargoChanged = true;
                    }
                }
            }

            if (cargoChanged)
            {
                var updatedCargo = new CargoJson
                {
                    Inventory = updatedInventory
                };

                CurrentCargo = updatedCargo;
            }
        }
        private void EnsureCarrierCargoTrackingInitialized(string context = "unknown")
        {
            if (_cargoTrackingInitialized)
            {
                return; // Already initialized
            }

            Log.Information("?? Initializing carrier cargo tracking from {Context}", context);

            try
            {
                if (_carrierCargo.Count == 0)
                {
                    LoadCarrierCargoFromDisk();
                }

                _carrierCargoTracker.Initialize(_carrierCargo);
                _cargoTrackingInitialized = true;

                Log.Information("? Carrier cargo tracking initialized with {Count} items from {Context}",
                    _carrierCargo.Count, context);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? Failed to initialize carrier cargo tracking from {Context}", context);

                _carrierCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _carrierCargoTracker.Initialize(_carrierCargo);
                _cargoTrackingInitialized = true;

                Log.Warning("? Initialized with empty carrier cargo state after failure");
            }
        }

        public void InitializeCargoFromSavedData(Dictionary<string, int> savedCargo)
        {
            using (BeginUpdate())
            {
                _carrierCargo.Clear();

                foreach (var item in savedCargo)
                {
                    _carrierCargo[item.Key] = item.Value;
                }

                _carrierCargoTracker.Initialize(savedCargo);
                _carrierCargoTracker.NormalizeCargoKeys();
                _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                UpdateCurrentCarrierCargoFromDictionary();

                _cargoTrackingInitialized = true;

                OnPropertyChanged(nameof(CarrierCargo));
                OnPropertyChanged(nameof(CurrentCarrierCargo));

                Log.Information("Carrier cargo initialized from saved data with {Count} items", _carrierCargo.Count);
            }
        }

        public void SyncCarrierCargoState()
        {
            try
            {
                Log.Information("?? Synchronizing carrier cargo state between GameState and CarrierCargoTracker");
                using (BeginUpdate())
                {
                    _carrierCargoTracker.NormalizeCargoKeys();
                    var merged = new Dictionary<string, int>(_carrierCargoTracker.Cargo, StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in _manualCarrierCargoChanges)
                    {
                        if (kvp.Value.IsActive && DateTime.UtcNow - kvp.Value.LastModified < TimeSpan.FromMinutes(30))
                        {
                            merged[kvp.Key] = kvp.Value.ManualQuantity;
                        }
                    }
                    _carrierCargo = merged;
                    _carrierCargoTracker.Initialize(_carrierCargo);
                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }
                Log.Information("? Carrier cargo state synchronized: {Count} items", _carrierCargo.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error synchronizing carrier cargo state");
            }
        }

        public void UpdateCarrierCargoItem(string itemName, int quantity, bool isManualChange = true)
        {
            Log.Debug("UpdateCarrierCargoItem: {Item} = {Quantity} (Manual: {Manual})",
                itemName, quantity, isManualChange);

            lock (_cargoLock)
            {
                int oldValue = _carrierCargo.TryGetValue(itemName, out int existing) ? existing : 0;

                using (BeginUpdate())
                {
                    if (quantity > 0)
                    {
                        _carrierCargo[itemName] = quantity;
                    }
                    else
                    {
                        if (_carrierCargo.ContainsKey(itemName))
                        {
                            _carrierCargo.Remove(itemName);
                            Log.Debug("Removed {Item} from carrier cargo tracking dictionary", itemName);
                        }
                        var itemToRemove = _currentCarrierCargo.FirstOrDefault(i =>
                            string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));
                        if (itemToRemove != null)
                        {
                            _currentCarrierCargo.Remove(itemToRemove);
                            Log.Debug("Removed {Item} from CurrentCarrierCargo UI list", itemName);
                        }
                    }

                    if (isManualChange)
                    {
                        var originalGameValue = GetOriginalGameValueForItem(itemName);
                        _manualCarrierCargoChanges[itemName] = new ManualCargoChange
                        {
                            ItemName = itemName,
                            ManualQuantity = quantity,
                            OriginalGameQuantity = originalGameValue,
                            LastModified = DateTime.UtcNow,
                            IsActive = true
                        };
                        SaveManualCarrierCargoChanges();
                        Log.Information("Saved manual change: {Item} {Original} -> {Manual}",
                            itemName, originalGameValue, quantity);
                    }
                    else
                    {
                        if (_manualCarrierCargoChanges.ContainsKey(itemName))
                        {
                            var manualChange = _manualCarrierCargoChanges[itemName];
                            if (DateTime.UtcNow - manualChange.LastModified < TimeSpan.FromMinutes(30))
                            {
                                Log.Information("Preserving manual change for {Item}: keeping {Manual} instead of game value {Game}",
                                    itemName, manualChange.ManualQuantity, quantity);
                                manualChange.OriginalGameQuantity = quantity;
                                _carrierCargo[itemName] = manualChange.ManualQuantity;
                                SaveManualCarrierCargoChanges();
                            }
                            else
                            {
                                _manualCarrierCargoChanges.Remove(itemName);
                                SaveManualCarrierCargoChanges();
                                Log.Information("Manual change for {Item} has expired, accepting game value {Quantity}",
                                    itemName, quantity);
                            }
                        }
                    }

                    _carrierCargoTracker.Initialize(_carrierCargo);

                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }

                Log.Information("Carrier cargo updated: {Item} {OldValue} -> {NewValue} (Manual: {Manual})",
                    itemName, oldValue, quantity, isManualChange);
            }
        }

        public void SynchronizeCarrierCargoState()
        {
            try
            {
                Log.Information("?? Synchronizing carrier cargo state between GameState and CarrierCargoTracker");
                using (BeginUpdate())
                {
                    var merged = new Dictionary<string, int>(_carrierCargoTracker.Cargo, StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in _manualCarrierCargoChanges)
                    {
                        if (kvp.Value.IsActive && DateTime.UtcNow - kvp.Value.LastModified < TimeSpan.FromMinutes(30))
                        {
                            merged[kvp.Key] = kvp.Value.ManualQuantity;
                        }
                    }
                    _carrierCargo = merged;
                    _carrierCargoTracker.Initialize(_carrierCargo);
                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }
                Log.Information("? Carrier cargo state synchronized: {Count} items", _carrierCargo.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error synchronizing carrier cargo state");
            }
        }

        private int GetOriginalGameValueForItem(string itemName)
        {
            if (_manualCarrierCargoChanges.TryGetValue(itemName, out var existingChange))
            {
                return existingChange.OriginalGameQuantity;
            }

            return _carrierCargo.TryGetValue(itemName, out int currentValue) ? currentValue : 0;
        }

        public void CleanupDuplicateCargoEntries()
        {
            try
            {
                Log.Information("?? Cleaning up duplicate carrier cargo entries");

                var duplicateGroups = _carrierCargo.Keys
                    .GroupBy(k => k.ToLowerInvariant())
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (!duplicateGroups.Any())
                {
                    Log.Information("? No duplicate entries found");
                    return;
                }

                using (BeginUpdate())
                {
                    foreach (var group in duplicateGroups)
                    {
                        var items = group.ToList();
                        Log.Information("Found duplicates: {Items}", string.Join(", ", items));

                        string bestKey = items.FirstOrDefault(k => char.IsUpper(k[0])) ?? items.First();
                        int totalQuantity = 0;

                        foreach (var key in items)
                        {
                            totalQuantity += _carrierCargo[key];
                            if (key != bestKey)
                            {
                                _carrierCargo.Remove(key);
                                Log.Information("Removed duplicate: {Key}", key);
                            }
                        }

                        _carrierCargo[bestKey] = totalQuantity;
                        Log.Information("Consolidated to: {Key} = {Quantity}", bestKey, totalQuantity);
                    }

                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }

                Log.Information("? Duplicate cleanup completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up duplicate cargo entries");
            }
        }

        public void FixCarrierCargoItem(string itemName, int correctQuantity)
        {
            try
            {
                Log.Information("?? Manual fix for carrier cargo item: {Item}", itemName);

                var actualKey = _carrierCargo.Keys.FirstOrDefault(k =>
                    string.Equals(k, itemName, StringComparison.OrdinalIgnoreCase));

                if (actualKey != null)
                {
                    int oldQty = _carrierCargo[actualKey];

                    using (BeginUpdate())
                    {
                        if (correctQuantity > 0)
                        {
                            _carrierCargo[actualKey] = correctQuantity;
                        }
                        else
                        {
                            _carrierCargo.Remove(actualKey);
                        }

                        UpdateCurrentCarrierCargoFromDictionary();
                        SaveCarrierCargoToDisk();
                    }

                    Log.Information("? Fixed {Item}: {OldQty} -> {NewQty}", actualKey, oldQty, correctQuantity);
                }
                else
                {
                    Log.Warning("Could not find item '{Item}' in carrier cargo", itemName);
                    Log.Information("Available items: {Items}", string.Join(", ", _carrierCargo.Keys));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fixing carrier cargo item {Item}", itemName);
            }
        }

        public void RefreshCargoStates()
        {
            try
            {
                Log.Information("?? Force refreshing cargo states");

                LoadCargoData();
                ForceProcessRecentCargoEvents();

                _carrierCargoTracker.NormalizeCargoKeys();
                SynchronizeCarrierCargoState();

                Log.Information("? Cargo states refreshed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing cargo states");
            }
        }

        public void ForceProcessRecentCargoEvents()
        {
            try
            {
                Log.Information("?? Force processing recent cargo events");

                if (string.IsNullOrEmpty(latestJournalPath) || !File.Exists(latestJournalPath))
                {
                    Log.Warning("No journal file available for processing");
                    return;
                }

                if (!_cargoTrackingInitialized)
                {
                    Log.Information("Initializing cargo tracking before processing events");
                    _cargoTrackingInitialized = true;
                }

                var fileInfo = new FileInfo(latestJournalPath);
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                long startPos = Math.Max(0, fileInfo.Length - 10240);
                fs.Seek(startPos, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                var cargoEvents = new List<string>();

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line) &&
                        (line.Contains("CargoTransfer") || line.Contains("CargoDepot") ||
                         line.Contains("MarketBuy") || line.Contains("MarketSell") ||
                         line.Contains("CarrierTradeOrder")))
                    {
                        cargoEvents.Add(line);
                    }
                }

                Log.Information("Found {Count} recent cargo events to process", cargoEvents.Count);

                int processedCount = 0;
                foreach (var eventLine in cargoEvents)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(eventLine);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("event", out var eventProp))
                        {
                            string eventType = eventProp.GetString();
                            bool shouldProcess = false;

                            switch (eventType)
                            {
                                case "CargoTransfer":
                                case "CargoDepot":
                                case "CarrierTradeOrder":
                                    shouldProcess = true;
                                    break;

                                case "MarketBuy":
                                    shouldProcess = root.TryGetProperty("BuyFromFleetCarrier", out var boughtFromCarrierProp) &&
                                                  boughtFromCarrierProp.GetBoolean();
                                    break;

                                case "MarketSell":
                                    shouldProcess = root.TryGetProperty("SellToFleetCarrier", out var soldToCarrierProp) &&
                                                  soldToCarrierProp.GetBoolean();
                                    break;
                            }

                            if (shouldProcess)
                            {
                                _carrierCargoTracker.Process(root);
                                processedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error processing cargo event: {Event}", eventLine);
                    }
                }

                if (processedCount > 0)
                {
                    using (BeginUpdate())
                    {
                        _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo, StringComparer.OrdinalIgnoreCase);
                        UpdateCurrentCarrierCargoFromDictionary();
                        SaveCarrierCargoToDisk();
                    }

                    Log.Information("Force processed {Count} cargo events, carrier now has {Items} items",
                        processedCount, _carrierCargo.Count);
                }
                else
                {
                    Log.Information("No cargo events needed processing");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error force processing recent cargo events");
            }
        }

        public void AnalyzeRecentCargoTransfers(string commodityName = null)
        {
            try
            {
                Log.Information("?? Analyzing recent cargo transfers for: {Commodity}", commodityName ?? "ALL");

                if (string.IsNullOrEmpty(latestJournalPath) || !File.Exists(latestJournalPath))
                {
                    Log.Warning("No journal file available for analysis");
                    return;
                }

                var fileInfo = new FileInfo(latestJournalPath);
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                long startPos = Math.Max(0, fileInfo.Length - 51200);
                fs.Seek(startPos, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                var transferEvents = new List<(DateTime timestamp, string eventLine)>();

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line) && line.Contains("CargoTransfer"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("timestamp", out var timestampProp) &&
                                DateTime.TryParse(timestampProp.GetString(), out var timestamp))
                            {
                                transferEvents.Add((timestamp, line));
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                Log.Information("Found {Count} CargoTransfer events", transferEvents.Count);

                int totalAdded = 0, totalRemoved = 0;
                foreach (var (timestamp, eventLine) in transferEvents.OrderBy(e => e.timestamp).TakeLast(10))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(eventLine);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("Transfers", out var transfersProp) &&
                            transfersProp.ValueKind == JsonValueKind.Array)
                        {
                            Log.Information("Transfer at {Timestamp}:", timestamp.ToString("HH:mm:ss"));

                            foreach (var transfer in transfersProp.EnumerateArray())
                            {
                                if (transfer.TryGetProperty("Type", out var typeProp) &&
                                    transfer.TryGetProperty("Count", out var countProp) &&
                                    transfer.TryGetProperty("Direction", out var directionProp))
                                {
                                    string internalName = typeProp.GetString();
                                    int count = countProp.GetInt32();
                                    string direction = directionProp.GetString();
                                    string displayName = CommodityMapper.GetDisplayName(internalName);

                                    if (!string.IsNullOrEmpty(commodityName) &&
                                        !displayName.Contains(commodityName, StringComparison.OrdinalIgnoreCase) &&
                                        !internalName.Contains(commodityName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }

                                    Log.Information("  • {InternalName} ? {DisplayName}: {Direction} {Count}",
                                        internalName, displayName, direction, count);

                                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                                    {
                                        totalAdded += count;
                                    }
                                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                                    {
                                        totalRemoved += count;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error analyzing transfer event");
                    }
                }

                Log.Information("Transfer summary: +{Added} to carrier, -{Removed} from carrier", totalAdded, totalRemoved);

                if (!string.IsNullOrEmpty(commodityName))
                {
                    var currentQty = _carrierCargo.TryGetValue(commodityName, out int qty) ? qty : 0;
                    Log.Information("Current {Commodity} quantity: {Quantity}", commodityName, currentQty);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error analyzing recent cargo transfers");
            }
        }

        private void ProcessCarrierCargoGameUpdate(Dictionary<string, int> newGameData)
        {
            if (newGameData == null) return;

            lock (_cargoLock)
            {
                Log.Information("Processing carrier cargo game update with manual change preservation");

                LoadManualCarrierCargoChanges();

                using (BeginUpdate())
                {
                    foreach (var gameItem in newGameData)
                    {
                        UpdateCarrierCargoItem(gameItem.Key, gameItem.Value, isManualChange: false);
                    }

                    var itemsToCheck = _carrierCargo.Keys.ToList();
                    foreach (var existingItem in itemsToCheck)
                    {
                        if (!newGameData.ContainsKey(existingItem))
                        {
                            if (!_manualCarrierCargoChanges.ContainsKey(existingItem))
                            {
                                UpdateCarrierCargoItem(existingItem, 0, isManualChange: false);
                            }
                            else
                            {
                                var manualChange = _manualCarrierCargoChanges[existingItem];
                                if (DateTime.UtcNow - manualChange.LastModified > TimeSpan.FromMinutes(30))
                                {
                                    _manualCarrierCargoChanges.Remove(existingItem);
                                    SaveManualCarrierCargoChanges();
                                    Log.Information("Expired manual change removed for non-existent item: {Item}", existingItem);
                                }
                            }
                        }
                    }
                }

                Log.Information("Game cargo update processing complete: {GameItems} game items processed",
                    newGameData.Count);
            }
        }

        private void SaveManualCarrierCargoChanges()
        {
            try
            {
                string directory = Path.GetDirectoryName(ManualCarrierCargoFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var activeChanges = _manualCarrierCargoChanges
                    .Where(kvp => kvp.Value.IsActive)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var json = JsonSerializer.Serialize(activeChanges, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ManualCarrierCargoFilePath, json);
                Log.Debug("Saved {Count} manual carrier cargo changes to disk", activeChanges.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save manual carrier cargo changes to disk");
            }
        }

        private void LoadManualCarrierCargoChanges()
        {
            try
            {
                if (!File.Exists(ManualCarrierCargoFilePath)) return;

                var json = File.ReadAllText(ManualCarrierCargoFilePath);
                var loadedChanges = JsonSerializer.Deserialize<Dictionary<string, ManualCargoChange>>(json);

                if (loadedChanges != null)
                {
                    var validChanges = loadedChanges.Where(kvp =>
                        DateTime.UtcNow - kvp.Value.LastModified < TimeSpan.FromMinutes(30))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    _manualCarrierCargoChanges.Clear();
                    foreach (var change in validChanges)
                    {
                        _manualCarrierCargoChanges[change.Key] = change.Value;
                    }

                    Log.Information("Loaded {Count} valid manual carrier cargo changes from disk",
                        _manualCarrierCargoChanges.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load manual carrier cargo changes from disk");
                _manualCarrierCargoChanges.Clear();
            }
        }

        public void ClearExpiredManualCargoChanges()
        {
            lock (_cargoLock)
            {
                var expiredKeys = _manualCarrierCargoChanges
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastModified > TimeSpan.FromMinutes(30))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _manualCarrierCargoChanges.Remove(key);
                }

                if (expiredKeys.Any())
                {
                    SaveManualCarrierCargoChanges();
                    Log.Information("Cleared {Count} expired manual cargo changes", expiredKeys.Count);
                }
            }
        }
    }
}
