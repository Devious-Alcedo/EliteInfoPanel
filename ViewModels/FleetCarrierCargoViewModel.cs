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

        private void ProcessRealTimeChanges()
        {
            var currentGameState = _gameState.CurrentCarrierCargo;
            if (currentGameState == null || !currentGameState.Any())
            {
                return;
            }

            bool madeChanges = false;

            // Build current game state dictionary
            var currentStateDict = currentGameState.ToDictionary(
                i => i.Name,
                i => i.Quantity,
                StringComparer.OrdinalIgnoreCase);

            // Look for differences from last known state
            foreach (var pair in currentStateDict)
            {
                if (!_lastKnownGameState.TryGetValue(pair.Key, out int previousQuantity) ||
                    previousQuantity != pair.Value)
                {
                    // This is a real change in quantity
                    int delta = _lastKnownGameState.TryGetValue(pair.Key, out previousQuantity) ?
                        pair.Value - previousQuantity : pair.Value;

                    Log.Debug("Real-time change detected: {Item} changed by {Delta} ({OldValue} → {NewValue})",
                        pair.Key, delta, previousQuantity, pair.Value);

                    // Apply this change to our UI model
                    var existingItem = Cargo.FirstOrDefault(i =>
                        string.Equals(i.Name, pair.Key, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        if (pair.Value <= 0)
                        {
                            // Remove completely if quantity is zero or negative
                            Cargo.Remove(existingItem);
                            Log.Debug("Removed {Item} from UI due to zero/negative quantity", pair.Key);
                        }
                        else
                        {
                            // Update existing item
                            existingItem.Quantity = pair.Value; // Set directly to match game state
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
                            Name = pair.Key,
                            Quantity = pair.Value,
                            FontSize = (int)this.FontSize // Add this line
                        });
                        madeChanges = true;
                        Log.Debug("Added new item to UI: {Item} = {Quantity}", pair.Key, pair.Value);
                    }
                }
            }

            // IMPORTANT: Remove items that are no longer in the game state
            foreach (var key in _lastKnownGameState.Keys.ToList())
            {
                if (!currentStateDict.ContainsKey(key))
                {
                    var itemToRemove = Cargo.FirstOrDefault(i =>
                        string.Equals(i.Name, key, StringComparison.OrdinalIgnoreCase));

                    if (itemToRemove != null)
                    {
                        Cargo.Remove(itemToRemove);
                        madeChanges = true;
                        Log.Debug("Removed item completely from UI: {Item} (no longer in game state)", key);
                    }
                }
            }

            // Update last known state to match current
            _lastKnownGameState = new Dictionary<string, int>(currentStateDict, StringComparer.OrdinalIgnoreCase);

            // Clean up any remaining zero-quantity items
            for (int i = Cargo.Count - 1; i >= 0; i--)
            {
                if (Cargo[i].Quantity <= 0)
                {
                    Log.Debug("Cleaning up zero-quantity item: {Item}", Cargo[i].Name);
                    Cargo.RemoveAt(i);
                    madeChanges = true;
                }
            }

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