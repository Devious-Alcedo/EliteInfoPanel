using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using Serilog;
using System.IO;
using System.Text.Json;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace EliteInfoPanel.ViewModels
{
    public class FleetCarrierCargoViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private readonly string _cargoSavePath;
        private bool _initialSyncComplete = false;
        private string _newCommodityName;
        private int _newCommodityQuantity = 1;
        private Dictionary<string, int> _lastKnownGameState = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<CarrierCargoItem> Cargo { get; } = new();
        public override double FontSize
        {
            get => base.FontSize;
            set
            {
                if (base.FontSize != value)
                {
                    base.FontSize = value;

                    // Update font size for all cargo items
                    foreach (var item in Cargo)
                    {
                        item.FontSize = (int)value;
                    }
                }
            }
        }

        public string NewCommodityName
        {
            get => _newCommodityName;
            set => SetProperty(ref _newCommodityName, value);
        }

        public int NewCommodityQuantity
        {
            get => _newCommodityQuantity;
            set => SetProperty(ref _newCommodityQuantity, Math.Max(1, value));
        }

        public RelayCommand IncrementCommand { get; }
        public RelayCommand DecrementCommand { get; }
        public RelayCommand AddCommodityCommand { get; }

        public FleetCarrierCargoViewModel(GameStateService gameState) : base("Fleet Carrier Cargo")
        {
            _gameState = gameState;

            // Set up save path in AppData
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteInfoPanel");
            Directory.CreateDirectory(appDataFolder);
            _cargoSavePath = Path.Combine(appDataFolder, "CarrierCargo.json");

            // Initialize commands
            IncrementCommand = new RelayCommand(IncrementCommodity);
            DecrementCommand = new RelayCommand(DecrementCommodity);
            AddCommodityCommand = new RelayCommand(_ => AddCommodity(), _ => CanAddCommodity());

            // First try to load saved data
            bool hasSavedData = LoadSavedCargoData();

            var savedData = Cargo.ToDictionary(
                    i => i.Name,
                    i => i.Quantity,
                    StringComparer.OrdinalIgnoreCase);  // Use case-insensitive keys

            // Initialize GameStateService with our saved data
            _gameState.InitializeCargoFromSavedData(savedData);

            if (hasSavedData)
            {
                Log.Information("Loaded {Count} cargo items from saved data", Cargo.Count);
                SetContextVisibility(Cargo.Count > 0);
            }

            // Capture initial game state for delta tracking
            CaptureGameState();

            // NOW subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Mark initialization as complete
            _initialSyncComplete = true;
            Log.Information("Fleet carrier cargo initialization complete - real-time updates enabled");
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CurrentCarrierCargo) && _initialSyncComplete)
            {
                // We only want to process real-time changes since initialization
                ProcessRealTimeChanges();
            }
        }

        private void CaptureGameState()
        {
            _lastKnownGameState.Clear();

            if (_gameState.CurrentCarrierCargo != null)
            {
                foreach (var item in _gameState.CurrentCarrierCargo)
                {
                    _lastKnownGameState[item.Name] = item.Quantity;
                }
                Log.Debug("Captured initial game state with {Count} items", _lastKnownGameState.Count);
            }
        }
        private string FindInternalName(string displayName)
        {
            // Quick check if it's already internal
            if (_gameState.CarrierCargo.ContainsKey(displayName))
                return displayName;

            // Search through CarrierCargo dictionary
            foreach (var kvp in _gameState.CarrierCargo)
            {
                if (string.Equals(CommodityMapper.GetDisplayName(kvp.Key), displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return displayName; // fallback
        }
        private void ProcessRealTimeChanges()
        {
            var currentGameState = _gameState.CurrentCarrierCargo;
            if (currentGameState == null || !currentGameState.Any())
            {
                return;
            }

            bool madeChanges = false;

            // Build current game state dictionary using INTERNAL names
            var currentStateDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in currentGameState)
            {
                // Find the internal name for this display name
                string internalName = FindInternalName(item.Name);
                if (!string.IsNullOrEmpty(internalName))
                {
                    currentStateDict[internalName] = item.Quantity;
                }
            }

            // Look for differences from last known state
            foreach (var pair in currentStateDict)
            {
                if (!_lastKnownGameState.TryGetValue(pair.Key, out int previousQuantity) ||
                    previousQuantity != pair.Value)
                {
                    // This is a real change in quantity
                    string displayName = CommodityMapper.GetDisplayName(pair.Key);

                    Log.Debug("Real-time change detected: {Item} changed by {Delta} ({OldValue} → {NewValue})",
                        displayName, pair.Value - previousQuantity, previousQuantity, pair.Value);

                    // Apply this change to our UI model using display names
                    var existingItem = Cargo.FirstOrDefault(i =>
                        string.Equals(i.Name, displayName, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        if (pair.Value <= 0)
                        {
                            // Remove completely if quantity is zero or negative
                            Cargo.Remove(existingItem);
                            Log.Debug("Removed {Item} from UI due to zero/negative quantity", displayName);
                        }
                        else
                        {
                            // Update existing item
                            existingItem.Quantity = pair.Value;
                            Log.Debug("Updated item in UI: {Item} now at {Quantity}",
                                existingItem.Name, existingItem.Quantity);
                        }
                        madeChanges = true;
                    }
                    else if (pair.Value > 0)
                    {
                        // New item with positive quantity
                        Cargo.Add(new CarrierCargoItem
                        {
                            Name = displayName,
                            Quantity = pair.Value,
                            FontSize = (int)this.FontSize
                        });
                        madeChanges = true;
                        Log.Debug("Added new item to UI: {Item} = {Quantity}", displayName, pair.Value);
                    }
                }
            }
            var currentDisplayNames = currentStateDict.Keys
                     .Select(k => CommodityMapper.GetDisplayName(k))
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (int i = Cargo.Count - 1; i >= 0; i--)
            {
                if (!currentDisplayNames.Contains(Cargo[i].Name))
                {
                    Log.Debug("Removed item completely from UI: {Item} (no longer in game state)", Cargo[i].Name);
                    Cargo.RemoveAt(i);
                    madeChanges = true;
                }
            }

            // Update last known state with internal names
            _lastKnownGameState = new Dictionary<string, int>(currentStateDict, StringComparer.OrdinalIgnoreCase);

            // Save if changes were made
            if (madeChanges)
            {
                SaveCargoData();
                SetContextVisibility(Cargo.Count > 0);
                Log.Information("Applied real-time changes to fleet carrier cargo - now {Count} items", Cargo.Count);
            }
        }
        private bool LoadSavedCargoData()
        {
            try
            {
                if (File.Exists(_cargoSavePath))
                {
                    string json = File.ReadAllText(_cargoSavePath);
                    var savedItems = JsonSerializer.Deserialize<List<CarrierCargoItem>>(json);

                    if (savedItems != null && savedItems.Any())
                    {
                        Cargo.Clear();
                        foreach (var item in savedItems.Where(i => i.Quantity > 0))
                        {
                            Cargo.Add(item);
                        }
                        Log.Information("Loaded {Count} cargo items from saved data", Cargo.Count);
                        return true;
                    }
                }

                Log.Information("No saved cargo data found or data was empty");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading saved cargo data");
                return false;
            }
        }

        private void SaveCargoData()
        {
            try
            {
                // Only save items with quantity > 0 and don't trigger any UI updates
                var cargoList = Cargo.Where(i => i.Quantity > 0).ToList();

                // Clean up the Cargo collection if needed (shouldn't be needed if we're maintaining it correctly)
                for (int i = Cargo.Count - 1; i >= 0; i--)
                {
                    if (Cargo[i].Quantity <= 0)
                    {
                        Cargo.RemoveAt(i);
                    }
                }

                string json = JsonSerializer.Serialize(cargoList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_cargoSavePath, json);
                Log.Debug("Saved {Count} cargo items to disk", cargoList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving cargo data");
            }
        }

        // In FleetCarrierCargoViewModel.cs - Update IncrementCommodity
        // In FleetCarrierCargoViewModel.cs
        private void IncrementCommodity(object parameter)
        {
            if (parameter is CarrierCargoItem item)
            {
                // Calculate new quantity first
                int newQuantity = item.Quantity + 1;
                Log.Debug("Incrementing {Name}: {OldQty} → {NewQty}", item.Name, item.Quantity, newQuantity);

                // Update GameState FIRST (this will trigger UI updates)
                _gameState.UpdateCarrierCargoItem(item.Name, newQuantity);

                // Update our local UI model to match
                item.Quantity = newQuantity;

                // Save data after all updates
                SaveCargoData();
            }
        }

        private void DecrementCommodity(object parameter)
        {
            if (parameter is CarrierCargoItem item && item.Quantity > 0)
            {
                // Calculate new quantity first
                int newQuantity = item.Quantity - 1;
                Log.Debug("Decrementing {Name}: {OldQty} → {NewQty}", item.Name, item.Quantity, newQuantity);

                // Update GameState FIRST
                _gameState.UpdateCarrierCargoItem(item.Name, newQuantity);

                // Update our local model
                item.Quantity = newQuantity;

                // Remove item from UI if quantity is zero
                if (newQuantity == 0)
                {
                    Cargo.Remove(item);
                    Log.Debug("Removed {Name} from UI list due to zero quantity", item.Name);
                }
                _gameState.UpdateCarrierCargoItem(item.Name, newQuantity);
                // Save data after all updates
                SaveCargoData();
            }
        }

        private void AddCommodity()
        {
            if (!CanAddCommodity()) return;

            // Normalize name to handle case sensitivity
            string normalizedName = NewCommodityName.Trim();
            int newQuantity = NewCommodityQuantity;

            Log.Debug("Adding commodity: {Name} = {Quantity}", normalizedName, newQuantity);

            // Check if commodity already exists (case-insensitive)
            var existingItem = Cargo.FirstOrDefault(i =>
                string.Equals(i.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

            // Update GameState FIRST
            _gameState.UpdateCarrierCargoItem(normalizedName, existingItem != null 
                ? existingItem.Quantity + newQuantity 
                : newQuantity);

            // Clear input fields
            NewCommodityName = "";
            NewCommodityQuantity = 1;

            // NOTE: We don't need to manually update our UI model or save
            // The PropertyChanged event from GameState will trigger our ProcessRealTimeChanges
            // which will update our UI model and save the data
        }
        private bool CanAddCommodity()
        {
            return !string.IsNullOrWhiteSpace(NewCommodityName) && NewCommodityQuantity > 0;
        }

    }
}