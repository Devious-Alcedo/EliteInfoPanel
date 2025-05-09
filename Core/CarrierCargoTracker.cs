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

                    // IMPORTANT: Only use case-insensitive comparison but PRESERVE SPACES
                    // This matches how items are looked up in the UI
                    string existingKey = null;
                    foreach (var key in _cargo.Keys)
                    {
                        if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                        {
                            existingKey = key;
                            break;
                        }
                    }

                    if (direction == "tocarrier")
                    {
                        if (existingKey != null)
                        {
                            // Use the existing key to maintain consistency
                            int currentAmount = _cargo[existingKey];
                            _cargo[existingKey] = currentAmount + count;
                            Log.Information("  -> Added to carrier: \"{ExistingName}\" now at {Count} (was {Previous})",
                                existingKey, _cargo[existingKey], currentAmount);
                        }
                        else
                        {
                            // This is a new item - use the EXACT name from the game
                            _cargo[name] = count;
                            Log.Information("  -> Added new item to carrier: \"{Name}\" = {Count}", name, count);
                        }
                    }
                    else if (direction == "toship")
                    {
                        if (existingKey != null)
                        {
                            int currentAmount = _cargo[existingKey];
                            // Subtract from current amount
                            int newAmount = Math.Max(0, currentAmount - count);

                            if (newAmount > 0)
                            {
                                _cargo[existingKey] = newAmount;
                                Log.Information("  -> Removed from carrier: \"{ExistingName}\" now at {Count} (was {Previous})",
                                    existingKey, newAmount, currentAmount);
                            }
                            else
                            {
                                _cargo.Remove(existingKey);
                                Log.Information("  -> Removed \"{ExistingName}\" completely from carrier (was {Previous})",
                                    existingKey, currentAmount);
                            }
                        }
                        else
                        {
                            Log.Warning("  -> Attempted to remove {Count} of \"{Name}\" from carrier, but none in inventory",
                                count, name);
                        }
                    }
                }
            }
        }
    }
}
