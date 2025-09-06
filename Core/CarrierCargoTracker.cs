using EliteInfoPanel.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace EliteInfoPanel.Core
{
    public class CarrierCargoTracker
    {
        private readonly Dictionary<string, int> _cargo = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, int> Cargo => _cargo;

        public void Process(JsonElement root)
        {
            if (!root.TryGetProperty("event", out var eventTypeProp)) return;
            string eventType = eventTypeProp.GetString();
            Log.Information("🎯 CarrierCargoTracker.Process: {EventType}", eventType);
            switch (eventType)
            {
                case "CarrierTradeOrder":
                case "CargoDepot":
                    ProcessCommodityCount(root);
                    break;
                case "MarketSell":
                    if (root.TryGetProperty("SellToFleetCarrier", out var soldToCarrierProp) && soldToCarrierProp.GetBoolean())
                    {
                        ProcessAddToCarrier(root);
                    }
                    break;
                case "MarketBuy":
                    if (root.TryGetProperty("BuyFromFleetCarrier", out var boughtFromCarrierProp) && boughtFromCarrierProp.GetBoolean())
                    {
                        ProcessRemoveFromCarrier(root);
                    }
                    break;
                case "CargoTransfer":
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
            _cargo.Clear();
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
                    string displayName = CommodityMapper.GetDisplayName(internalName);
                    var existingKey = FindExistingCargoKey(displayName);
                    if (existingKey != null && existingKey != displayName)
                    {
                        Log.Information("🔄 Normalizing cargo key: {OldKey} → {NewKey}", existingKey, displayName);
                        _cargo.Remove(existingKey);
                    }
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
                    string displayName = CommodityMapper.GetDisplayName(internalName);
                    var existingKey = FindExistingCargoKey(displayName);
                    int currentQty = 0;
                    if (existingKey != null)
                    {
                        currentQty = _cargo[existingKey];
                        if (existingKey != displayName)
                        {
                            Log.Information("🔄 Normalizing cargo key during add: {OldKey} → {NewKey}", existingKey, displayName);
                            _cargo.Remove(existingKey);
                        }
                    }
                    _cargo[displayName] = currentQty + count;
                    Log.Debug("Added to carrier via market: {DisplayName} + {Count} = {Total}", displayName, count, _cargo[displayName]);
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
                    string displayName = CommodityMapper.GetDisplayName(internalName);
                    var existingKey = FindExistingCargoKey(displayName);
                    if (existingKey != null)
                    {
                        int currentQty = _cargo[existingKey];
                        int newAmount = Math.Max(0, currentQty - count);
                        _cargo.Remove(existingKey);
                        if (newAmount > 0)
                        {
                            _cargo[displayName] = newAmount;
                        }
                        Log.Debug("Removed from carrier via market: {DisplayName} - {Count} = {Remaining} (key: {OldKey} → {NewKey})", displayName, count, newAmount, existingKey, displayName);
                    }
                    else
                    {
                        Log.Warning("Could not find '{DisplayName}' in carrier cargo to remove via market", displayName);
                    }
                }
            }
        }
        private void ProcessTransfer(JsonElement root)
        {
            if (!root.TryGetProperty("Transfers", out var transfersProp) || transfersProp.ValueKind != JsonValueKind.Array)
                return;
            Log.Information("🔄 ProcessTransfer: Processing {Count} transfers", transfersProp.GetArrayLength());
            Log.Information("📦 BEFORE TRANSFER - Carrier cargo state:");
            foreach (var item in _cargo)
            {
                Log.Information("  📦 {Name}: {Quantity}", item.Key, item.Value);
            }
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
                    Log.Information("🔄 Processing transfer: {InternalName} → {DisplayName} | {Direction} {Count}", internalName, displayName, direction, count);
                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        var existingKey = FindExistingCargoKey(displayName);
                        int previousQty = 0;
                        if (existingKey != null)
                        {
                            previousQty = _cargo[existingKey];
                            _cargo.Remove(existingKey);
                        }
                        _cargo[displayName] = previousQty + count;
                        Log.Information("➕ Added to carrier: {Item} | {PrevQty} + {Count} = {NewQty} (key: {ExistingKey} → {NewKey})", displayName, previousQty, count, _cargo[displayName], existingKey ?? "none", displayName);
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        var existingKey = FindExistingCargoKey(displayName);
                        if (existingKey != null)
                        {
                            int currentQuantity = _cargo[existingKey];
                            int newAmount = Math.Max(0, currentQuantity - count);
                            Log.Information("➖ Removing from carrier: {Item} (key: {ExistingKey}) | {CurrentQty} - {Count} = {NewQty}", displayName, existingKey, currentQuantity, count, newAmount);
                            _cargo.Remove(existingKey);
                            if (newAmount > 0)
                            {
                                _cargo[displayName] = newAmount;
                                Log.Information("✅ Updated carrier: {Item} now at {NewQty}", displayName, newAmount);
                            }
                            else
                            {
                                Log.Information("🗑️ Removed completely: {Item} (quantity would be 0)", displayName);
                            }
                        }
                        else
                        {
                            Log.Warning("⚠️ Could not find '{DisplayName}' in carrier cargo to remove", displayName);
                            var similarNames = _cargo.Keys.Where(k => 
                                k.Contains(displayName, StringComparison.OrdinalIgnoreCase) ||
                                displayName.Contains(k, StringComparison.OrdinalIgnoreCase)
                            ).ToList();
                            if (similarNames.Any())
                            {
                                Log.Warning("🔍 Found similar names: {SimilarNames}", string.Join(", ", similarNames));
                            }
                            Log.Debug("📦 Current carrier cargo contains: {Items}", string.Join(", ", _cargo.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                        }
                    }
                    else
                    {
                        Log.Warning("❓ Unknown transfer direction: {Direction}", direction);
                    }
                }
            }
            Log.Information("📦 AFTER TRANSFER - Carrier cargo state:");
            foreach (var item in _cargo)
            {
                Log.Information("  📦 {Name}: {Quantity}", item.Key, item.Value);
            }
        }
        private string FindExistingCargoKey(string targetName)
        {
            if (_cargo.ContainsKey(targetName))
                return targetName;
            return _cargo.Keys.FirstOrDefault(k => 
                string.Equals(k, targetName, StringComparison.OrdinalIgnoreCase));
        }
        public void NormalizeCargoKeys()
        {
            var itemsToNormalize = new List<(string oldKey, string newKey, int quantity)>();
            foreach (var item in _cargo.ToList())
            {
                string normalizedKey = CommodityMapper.GetDisplayName(item.Key);
                if (!string.Equals(item.Key, normalizedKey, StringComparison.Ordinal))
                {
                    itemsToNormalize.Add((item.Key, normalizedKey, item.Value));
                }
            }
            foreach (var (oldKey, newKey, quantity) in itemsToNormalize)
            {
                _cargo.Remove(oldKey);
                if (_cargo.TryGetValue(newKey, out int existingQty))
                {
                    _cargo[newKey] = existingQty + quantity;
                    Log.Information("🔄 Consolidated cargo: {OldKey} + {NewKey} = {TotalQty}", oldKey, newKey, _cargo[newKey]);
                }
                else
                {
                    _cargo[newKey] = quantity;
                    Log.Information("🔄 Normalized cargo key: {OldKey} → {NewKey}", oldKey, newKey);
                }
            }
        }
    }
}
