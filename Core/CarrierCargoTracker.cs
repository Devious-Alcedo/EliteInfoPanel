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
                    var existingKey = FindExistingCargoKey(internalName);
                    if (existingKey != null && existingKey != internalName)
                    {
                        Log.Information("🔄 Normalizing (internal) cargo key: {OldKey} → {NewKey}", existingKey, internalName);
                        _cargo.Remove(existingKey);
                    }
                    _cargo[internalName] = count;
                    Log.Debug("Set commodity count: {Internal} (display: {Display}) = {Count}", internalName, CommodityMapper.GetDisplayName(internalName), count);
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
                    var existingKey = FindExistingCargoKey(internalName);
                    int currentQty = 0;
                    if (existingKey != null)
                    {
                        currentQty = _cargo[existingKey];
                        if (!string.Equals(existingKey, internalName, StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Information("🔄 Normalizing cargo key during add: {OldKey} → {NewKey}", existingKey, internalName);
                            _cargo.Remove(existingKey);
                        }
                    }
                    _cargo[internalName] = currentQty + count;
                    Log.Debug("Added to carrier via market: {Internal} (display {Display}) + {Count} = {Total}", internalName, CommodityMapper.GetDisplayName(internalName), count, _cargo[internalName]);
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
                    var existingKey = FindExistingCargoKey(internalName);
                    if (existingKey != null)
                    {
                        int currentQty = _cargo[existingKey];
                        int newAmount = Math.Max(0, currentQty - count);
                        _cargo.Remove(existingKey);
                        if (newAmount > 0)
                        {
                            _cargo[internalName] = newAmount;
                        }
                        Log.Debug("Removed from carrier via market: {Internal} (display {Display}) - {Count} = {Remaining}", internalName, CommodityMapper.GetDisplayName(internalName), count, newAmount);
                    }
                    else
                    {
                        Log.Warning("Could not find '{Internal}' in carrier cargo to remove via market", internalName);
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
                    Log.Information("🔄 Processing transfer: {Internal} (display {Display}) | {Direction} {Count}", internalName, CommodityMapper.GetDisplayName(internalName), direction, count);
                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        var existingKey = FindExistingCargoKey(internalName);
                        int previousQty = 0;
                        if (existingKey != null)
                        {
                            previousQty = _cargo[existingKey];
                            if (!string.Equals(existingKey, internalName, StringComparison.OrdinalIgnoreCase))
                                _cargo.Remove(existingKey);
                        }
                        _cargo[internalName] = previousQty + count;
                        Log.Information("➕ Added to carrier: {Internal} (display {Display}) | {PrevQty} + {Count} = {NewQty}", internalName, CommodityMapper.GetDisplayName(internalName), previousQty, count, _cargo[internalName]);
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        var existingKey = FindExistingCargoKey(internalName);
                        if (existingKey != null)
                        {
                            int currentQuantity = _cargo[existingKey];
                            int newAmount = Math.Max(0, currentQuantity - count);
                            _cargo.Remove(existingKey);
                            if (newAmount > 0)
                            {
                                _cargo[internalName] = newAmount;
                            }
                            Log.Information("➖ Removed from carrier: {Internal} (display {Display}) | {Current} - {Count} = {NewQty}", internalName, CommodityMapper.GetDisplayName(internalName), currentQuantity, count, newAmount);
                        }
                        else
                        {
                            Log.Warning("⚠️ Could not find '{Internal}' in carrier cargo to remove", internalName);
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
            // No-op now that we store only internal keys.
        }
    }
}
