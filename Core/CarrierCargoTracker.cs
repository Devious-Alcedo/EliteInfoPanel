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

                    // Process the transfer based on direction
                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // When adding to carrier, just use the name as provided by the game
                        // This ensures we're tracking items with the same keys the game uses
                        _cargo[name] = _cargo.TryGetValue(name, out int current) ? current + count : count;
                        Log.Debug("Added to carrier: {Item} now at {Count}", name, _cargo[name]);
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // When removing from carrier, find the exact key as used in our dictionary
                        if (_cargo.TryGetValue(name, out int current))
                        {
                            int newAmount = Math.Max(0, current - count);
                            if (newAmount > 0)
                            {
                                _cargo[name] = newAmount;
                                Log.Debug("Removed from carrier: {Item} now at {Count}", name, newAmount);
                            }
                            else
                            {
                                // CRITICAL: When quantity reaches zero, remove the item completely
                                _cargo.Remove(name);
                                Log.Debug("Removed completely: {Item} (quantity would be 0)", name);
                            }
                        }
                    }
                }
            }
        }
    }
}
