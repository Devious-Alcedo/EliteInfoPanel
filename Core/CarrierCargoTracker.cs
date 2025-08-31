using EliteInfoPanel.Util;
using Serilog;
using System.Collections.Generic;
using System.Text.Json;

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
            
#if dev
            Log.Information("🎯 CarrierCargoTracker.Process: {EventType}", eventType);
#endif

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
                    
                    // Find existing cargo using case-insensitive search
                    var existingKey = FindExistingCargoKey(displayName);
                    if (existingKey != null && existingKey != displayName)
                    {
#if dev
                        Log.Information("🔄 Normalizing cargo key: {OldKey} → {NewKey}", existingKey, displayName);
#endif
                        _cargo.Remove(existingKey);
                    }
                    
                    _cargo[displayName] = count;
#if dev
                    Log.Debug("Set commodity count: {DisplayName} = {Count}", displayName, count);
#endif
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

                    // Find existing cargo using case-insensitive search
                    var existingKey = FindExistingCargoKey(displayName);
                    int currentQty = 0;
                    
                    if (existingKey != null)
                    {
                        currentQty = _cargo[existingKey];
                        if (existingKey != displayName)
                        {
#if dev
                            Log.Information("🔄 Normalizing cargo key during add: {OldKey} → {NewKey}", existingKey, displayName);
#endif
                            _cargo.Remove(existingKey);
                        }
                    }

                    _cargo[displayName] = currentQty + count;
#if dev
                    Log.Debug("Added to carrier via market: {DisplayName} + {Count} = {Total}", displayName, count, _cargo[displayName]);
#endif
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

                    // Find existing cargo using case-insensitive search
                    var existingKey = FindExistingCargoKey(displayName);
                    
                    if (existingKey != null)
                    {
                        int currentQty = _cargo[existingKey];
                        int newAmount = Math.Max(0, currentQty - count);
                        
                        _cargo.Remove(existingKey); // Remove old key
                        
                        if (newAmount > 0)
                        {
                            _cargo[displayName] = newAmount; // Add with normalized key
                        }
                        
#if dev
                        Log.Debug("Removed from carrier via market: {DisplayName} - {Count} = {Remaining} (key: {OldKey} → {NewKey})", 
                            displayName, count, newAmount, existingKey, displayName);
#endif
                    }
                    else
                    {
#if dev
                        Log.Warning("Could not find '{DisplayName}' in carrier cargo to remove via market", displayName);
#endif
                    }
                }
            }
        }


        private void ProcessTransfer(JsonElement root)
        {
            if (!root.TryGetProperty("Transfers", out var transfersProp) || transfersProp.ValueKind != JsonValueKind.Array)
                return;

#if dev
            Log.Information("🔄 ProcessTransfer: Processing {Count} transfers", transfersProp.GetArrayLength());
            
            // Log the complete cargo state before processing
            Log.Information("📦 BEFORE TRANSFER - Carrier cargo state:");
            foreach (var item in _cargo)
            {
                Log.Information("  📦 {Name}: {Quantity}", item.Key, item.Value);
            }
#endif

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

#if dev
                    Log.Information("🔄 Processing transfer: {InternalName} → {DisplayName} | {Direction} {Count}",
                        internalName, displayName, direction, count);
#endif

                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // When adding to carrier, find existing item with case-insensitive search
                        var existingKey = FindExistingCargoKey(displayName);
                        int previousQty = 0;
                        
                        if (existingKey != null)
                        {
                            previousQty = _cargo[existingKey];
                            _cargo.Remove(existingKey); // Remove old key
                        }
                        
                        _cargo[displayName] = previousQty + count;

#if dev
                        Log.Information("➕ Added to carrier: {Item} | {PrevQty} + {Count} = {NewQty} (key: {ExistingKey} → {NewKey})",
                            displayName, previousQty, count, _cargo[displayName], existingKey ?? "none", displayName);
#endif
                    }
                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove from carrier - use case-insensitive search
                        var existingKey = FindExistingCargoKey(displayName);
                        
                        if (existingKey != null)
                        {
                            int currentQuantity = _cargo[existingKey];
                            int newAmount = Math.Max(0, currentQuantity - count);
                            
#if dev
                            Log.Information("➖ Removing from carrier: {Item} (key: {ExistingKey}) | {CurrentQty} - {Count} = {NewQty}",
                                displayName, existingKey, currentQuantity, count, newAmount);
#endif
                            
                            _cargo.Remove(existingKey); // Remove old key
                            
                            if (newAmount > 0)
                            {
                                _cargo[displayName] = newAmount; // Add with correct display name
#if dev
                                Log.Information("✅ Updated carrier: {Item} now at {NewQty}", displayName, newAmount);
#endif
                            }
                            else
                            {
#if dev
                                Log.Information("🗑️ Removed completely: {Item} (quantity would be 0)", displayName);
#endif
                            }
                        }
                        else
                        {
#if dev
                            Log.Warning("⚠️ Could not find '{DisplayName}' in carrier cargo to remove", displayName);
#endif
                            
                            // Check for similar names (case variations, etc.)
                            var similarNames = _cargo.Keys.Where(k => 
                                k.Contains(displayName, StringComparison.OrdinalIgnoreCase) ||
                                displayName.Contains(k, StringComparison.OrdinalIgnoreCase)
                            ).ToList();
                            
                            if (similarNames.Any())
                            {
#if dev
                                Log.Warning("🔍 Found similar names: {SimilarNames}", string.Join(", ", similarNames));
#endif
                            }

                            // Log current cargo for debugging
#if dev
                            Log.Debug("📦 Current carrier cargo contains: {Items}",
                                string.Join(", ", _cargo.Select(kvp => $"{kvp.Key}={kvp.Value}")));
#endif
                        }
                    }
                    else
                    {
#if dev
                        Log.Warning("❓ Unknown transfer direction: {Direction}", direction);
#endif
                    }
                }
            }
            
#if dev
            // Log the complete cargo state after processing
            Log.Information("📦 AFTER TRANSFER - Carrier cargo state:");
            foreach (var item in _cargo)
            {
                Log.Information("  📦 {Name}: {Quantity}", item.Key, item.Value);
            }
#endif
        }
        
        /// <summary>
        /// Finds an existing cargo key using case-insensitive search
        /// </summary>
        private string FindExistingCargoKey(string targetName)
        {
            // First try exact match
            if (_cargo.ContainsKey(targetName))
                return targetName;
            
            // Then try case-insensitive match
            return _cargo.Keys.FirstOrDefault(k => 
                string.Equals(k, targetName, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Normalizes all cargo keys to use consistent display names
        /// </summary>
        public void NormalizeCargoKeys()
        {
            var itemsToNormalize = new List<(string oldKey, string newKey, int quantity)>();
            
            foreach (var item in _cargo.ToList())
            {
                // Convert internal names to display names if needed
                string normalizedKey = CommodityMapper.GetDisplayName(item.Key);
                
                if (!string.Equals(item.Key, normalizedKey, StringComparison.Ordinal))
                {
                    itemsToNormalize.Add((item.Key, normalizedKey, item.Value));
                }
            }
            
            foreach (var (oldKey, newKey, quantity) in itemsToNormalize)
            {
                _cargo.Remove(oldKey);
                
                // Combine with existing if there's already an item with the new key
                if (_cargo.TryGetValue(newKey, out int existingQty))
                {
                    _cargo[newKey] = existingQty + quantity;
#if dev
                    Log.Information("🔄 Consolidated cargo: {OldKey} + {NewKey} = {TotalQty}",
                        oldKey, newKey, _cargo[newKey]);
#endif
                }
                else
                {
                    _cargo[newKey] = quantity;
#if dev
                    Log.Information("🔄 Normalized cargo key: {OldKey} → {NewKey}", oldKey, newKey);
#endif
                }
            }
        }
    }
}
