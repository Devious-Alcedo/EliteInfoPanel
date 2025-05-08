using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using Serilog;
using System.IO;
using System.Text.Json;

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
                        // Update existing item
                        existingItem.Quantity += delta;
                        if (existingItem.Quantity <= 0)
                        {
                            Cargo.Remove(existingItem);
                        }
                        madeChanges = true;

                        Log.Debug("Updated item in UI: {Item} now at {Quantity}",
                            existingItem.Name, existingItem.Quantity);
                    }
                    else if (delta > 0)
                    {
                        // New item
                        Cargo.Add(new CarrierCargoItem
                        {
                            Name = pair.Key,
                            Quantity = delta
                        });
                        madeChanges = true;

                        Log.Debug("Added new item to UI: {Item} = {Quantity}", pair.Key, delta);
                    }
                }
            }

            // Look for items that were removed
            var removedItems = _lastKnownGameState.Keys
                .Where(key => !currentStateDict.ContainsKey(key))
                .ToList();

            foreach (var key in removedItems)
            {
                var existingItem = Cargo.FirstOrDefault(i =>
                    string.Equals(i.Name, key, StringComparison.OrdinalIgnoreCase));

                if (existingItem != null)
                {
                    Cargo.Remove(existingItem);
                    madeChanges = true;
                    Log.Debug("Removed item from UI: {Item}", key);
                }
            }

            // Update last known state
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
                // Only save items with quantity > 0
                var cargoList = Cargo.Where(i => i.Quantity > 0).ToList();

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

        private void IncrementCommodity(object parameter)
        {
            if (parameter is CarrierCargoItem item)
            {
                item.Quantity++;
                Log.Debug("Manually incremented: {Name} to {Quantity}", item.Name, item.Quantity);
                SaveCargoData();
            }
        }

        private void DecrementCommodity(object parameter)
        {
            if (parameter is CarrierCargoItem item && item.Quantity > 0)
            {
                item.Quantity--;
                Log.Debug("Manually decremented: {Name} to {Quantity}", item.Name, item.Quantity);

                // Remove the item if quantity is zero
                if (item.Quantity == 0)
                {
                    Cargo.Remove(item);
                    Log.Debug("Removed {Name} due to zero quantity", item.Name);
                }

                SaveCargoData();
            }
        }

        private bool CanAddCommodity()
        {
            return !string.IsNullOrWhiteSpace(NewCommodityName) && NewCommodityQuantity > 0;
        }

        private void AddCommodity()
        {
            if (!CanAddCommodity()) return;

            // Check if commodity already exists (case-insensitive)
            var existingItem = Cargo.FirstOrDefault(i =>
                string.Equals(i.Name, NewCommodityName, StringComparison.OrdinalIgnoreCase));

            if (existingItem != null)
            {
                // Update existing item
                existingItem.Quantity += NewCommodityQuantity;
                Log.Debug("Added to existing cargo: {Name} now at {Quantity}",
                    existingItem.Name, existingItem.Quantity);
            }
            else
            {
                // Add new item
                var newItem = new CarrierCargoItem
                {
                    Name = NewCommodityName,
                    Quantity = NewCommodityQuantity
                };

                Cargo.Add(newItem);
                Log.Debug("Added new cargo: {Name} = {Quantity}", newItem.Name, newItem.Quantity);
            }

            // Clear input fields
            NewCommodityName = "";
            NewCommodityQuantity = 1;

            // Save data
            SaveCargoData();
        }
    }
}