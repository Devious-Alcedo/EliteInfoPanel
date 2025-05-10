using EliteInfoPanel.Util;
using Serilog;
using System.Collections.Generic;
using System.Text.Json;

namespace EliteInfoPanel.Core
{
    public class CarrierCargoTracker
    {
        private readonly Dictionary<string, int> _cargo = new();

        public IReadOnlyDictionary<string, int> Cargo => _cargo;

        public void Process(JsonElement root)
        {
           
            if (!root.TryGetProperty("event", out var eventTypeProp)) return;
            string eventType = eventTypeProp.GetString();

            switch (eventType)
            {
                case "CarrierTradeOrder":
                case "CargoDepot":
                    ProcessCommodityCount(root);
                    break;

                case "MarketSell": // Player sold TO carrier
                    if (root.TryGetProperty("SellToFleetCarrier", out var soldToCarrierProp) && soldToCarrierProp.GetBoolean())
                    {
                        ProcessAddToCarrier(root);
                    }
                    break;

                case "MarketBuy": // Player bought FROM carrier
                    if (root.TryGetProperty("BuyFromFleetCarrier", out var boughtFromCarrierProp) && boughtFromCarrierProp.GetBoolean())
                    {
                        ProcessRemoveFromCarrier(root);
                    }
                    break;

                case "CargoTransfer": // More complex transfers
                    ProcessTransfer(root);
                    break;
            }
        }
        public void Reset()
        {
            _cargo.Clear();
            Log.Debug("CarrierCargoTracker state reset");
        }
        public void Initialize(Dictionary<string, int> savedCargo)
        {
            // Clear existing data
            _cargo.Clear();

            // Copy the saved cargo data
            foreach (var item in savedCargo)
            {
                _cargo[item.Key] = item.Value;
            }

            Log.Information("CarrierCargoTracker initialized with {Count} saved cargo items", _cargo.Count);
        }
        private void ProcessCommodityCount(JsonElement root)
        {
            if (root.TryGetProperty("Commodity", out var commodityProp) &&
                root.TryGetProperty("Count", out var countProp))
            {
                string name = commodityProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    _cargo[name] = count;
                }
            }
        }

        private void ProcessAddToCarrier(JsonElement root)
        {
            if (root.TryGetProperty("Type", out var typeProp) &&
                root.TryGetProperty("Count", out var countProp))
            {
                string name = typeProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (_cargo.TryGetValue(name, out var existing))
                        _cargo[name] = existing + count;
                    else
                        _cargo[name] = count;
                }
            }
        }

        private void ProcessRemoveFromCarrier(JsonElement root)
        {
            if (root.TryGetProperty("Type", out var typeProp) &&
                root.TryGetProperty("Count", out var countProp))
            {
                string name = typeProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (_cargo.TryGetValue(name, out var existing))
                    {
                        _cargo[name] = System.Math.Max(0, existing - count);
                        if (_cargo[name] == 0)
                            _cargo.Remove(name);
                    }
                }
            }
        }

        // In CarrierCargoTracker.cs
        // In CarrierCargoTracker.cs - Fix the ProcessTransfer method

        // In CarrierCargoTracker.cs - Fix the ProcessTransfer method
        // 1. Fix in CarrierCargoTracker.ProcessTransfer
        // - Preserve spaces in names
        // - Use case-insensitive comparison consistent with the UI
        // In CarrierCargoTracker.cs - Update ProcessTransfer to handle name mapping
        private void ProcessTransfer(JsonElement root)
        {
            if (!root.TryGetProperty("Transfers", out var transfersProp) || transfersProp.ValueKind != JsonValueKind.Array)
                return;

            foreach (var transfer in transfersProp.EnumerateArray())
            {
                if (transfer.TryGetProperty("Type", out var typeProp) &&
                    transfer.TryGetProperty("Count", out var countProp) &&
                    transfer.TryGetProperty("Direction", out var directionProp))
                {
                    string name = typeProp.GetString();
                    int count = countProp.GetInt32();
                    string direction = directionProp.GetString();

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    // IMPORTANT: CargoTransfer uses internal names, but we need to check if there's 
                    // an existing entry using the display name key (from manual additions)
                    string keyToUse = name;
                    string displayName = CommodityMapper.GetDisplayName(name);

                    // Check if we have this commodity stored under its display name
                    if (_cargo.ContainsKey(displayName) && !_cargo.ContainsKey(name))
                    {
                        // We have it stored as display name, use that key
                        keyToUse = displayName;
                        Log.Information("Found existing item stored as display name: '{DisplayName}' for internal '{Internal}'",
                            displayName, name);
                    }
                    else if (_cargo.ContainsKey(name))
                    {
                        // We have it stored as internal name, use that
                        keyToUse = name;
                    }
                    else
                    {
                        // New item, use internal name (the standard)
                        keyToUse = name;
                    }

                    Log.Information("Processing transfer: {Item} ({Key}) - {Direction} {Count}",
                        displayName, keyToUse, direction, count);

                    // Process the transfer
                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add to existing quantity
                        _cargo[keyToUse] = _cargo.TryGetValue(keyToUse, out int current) ? current + count : count;
                        Log.Information("Added to carrier: {Item} now at {NewQty} (was {OldQty})",
                            displayName, _cargo[keyToUse], current);
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove from carrier
                        if (_cargo.TryGetValue(keyToUse, out int current))
                        {
                            int newAmount = Math.Max(0, current - count);
                            if (newAmount > 0)
                            {
                                _cargo[keyToUse] = newAmount;
                                Log.Information("Removed from carrier: {Item} now at {NewQty} (was {OldQty})",
                                    displayName, newAmount, current);
                            }
                            else
                            {
                                _cargo.Remove(keyToUse);
                                Log.Information("Removed completely: {Item} (quantity would be 0)", displayName);
                            }
                        }
                    }
                }
            }
        }
    }
}
