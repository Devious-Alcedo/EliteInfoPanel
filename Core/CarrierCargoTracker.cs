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
                string internalName = commodityProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(internalName))
                {
                    // Convert to display name for consistency
                    string displayName = CommodityMapper.GetDisplayName(internalName);
                    _cargo[displayName] = count;
                    Log.Debug("Set commodity count: {DisplayName} = {Count}", displayName, count);
                }
            }
        }
        private void ProcessAddToCarrier(JsonElement root)
        {
            if (root.TryGetProperty("Type", out var typeProp) &&
                root.TryGetProperty("Count", out var countProp))
            {
                string internalName = typeProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(internalName))
                {
                    // Convert to display name
                    string displayName = CommodityMapper.GetDisplayName(internalName);

                    if (_cargo.TryGetValue(displayName, out var existing))
                        _cargo[displayName] = existing + count;
                    else
                        _cargo[displayName] = count;

                    Log.Debug("Added to carrier via market: {DisplayName} + {Count}", displayName, count);
                }
            }
        }

        private void ProcessRemoveFromCarrier(JsonElement root)
        {
            if (root.TryGetProperty("Type", out var typeProp) &&
                root.TryGetProperty("Count", out var countProp))
            {
                string internalName = typeProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(internalName))
                {
                    // Convert to display name
                    string displayName = CommodityMapper.GetDisplayName(internalName);

                    if (_cargo.TryGetValue(displayName, out var existing))
                    {
                        _cargo[displayName] = System.Math.Max(0, existing - count);
                        if (_cargo[displayName] == 0)
                            _cargo.Remove(displayName);

                        Log.Debug("Removed from carrier via market: {DisplayName} - {Count}", displayName, count);
                    }
                }
            }
        }


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
                    string internalName = typeProp.GetString();
                    int count = countProp.GetInt32();
                    string direction = directionProp.GetString();

                    if (string.IsNullOrWhiteSpace(internalName)) continue;

                    string displayName = CommodityMapper.GetDisplayName(internalName);

                    Log.Information("Processing transfer: {InternalName} ({DisplayName}) - {Direction} {Count}",
                        internalName, displayName, direction, count);

                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // When adding to carrier, use display name for consistency with UI
                        _cargo[displayName] = _cargo.TryGetValue(displayName, out int current) ? current + count : count;

                        Log.Information("Added to carrier: {Item} now at {NewQty} (was {OldQty})",
                            displayName, _cargo[displayName], current);
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove from carrier - look for the item by display name
                        if (_cargo.TryGetValue(displayName, out int currentQuantity))
                        {
                            int newAmount = Math.Max(0, currentQuantity - count);
                            if (newAmount > 0)
                            {
                                _cargo[displayName] = newAmount;
                                Log.Information("Removed from carrier: {Item} now at {NewQty} (was {OldQty})",
                                    displayName, newAmount, currentQuantity);
                            }
                            else
                            {
                                _cargo.Remove(displayName);
                                Log.Information("Removed completely: {Item} (quantity would be 0)", displayName);
                            }
                        }
                        else
                        {
                            Log.Warning("Could not find '{DisplayName}' in carrier cargo to remove", displayName);

                            // Log current cargo for debugging
                            Log.Debug("Current carrier cargo contains: {Items}",
                                string.Join(", ", _cargo.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                        }
                    }
                }
            }
        }
    }
}
