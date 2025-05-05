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
        private bool _ignoreJournalUpdates = false;
        private string _newCommodityName;
        private int _newCommodityQuantity = 1;
        private bool _isInitializing = true;
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
            Log.Information("Saved cargo data loaded: {Success}, {Count} items",
                hasSavedData, Cargo.Count);

            // ONLY NOW subscribe to property changes - AFTER loading saved data
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Initialization complete - now we'll handle journal updates
            _isInitializing = false;
            Log.Information("Fleet carrier cargo initialization complete");
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CurrentCarrierCargo))
            {
                Log.Information("FleetCarrierCargoViewModel received CurrentCarrierCargo update");

                // Skip updates during initialization
                if (_isInitializing)
                {
                    Log.Information("Ignoring carrier cargo update during initialization");
                    return;
                }

                // Now synchronize with the game state
                UpdateFromGameState();
            }
        }
        private void UpdateFromGameState()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var gameItems = _gameState.CurrentCarrierCargo;
                if (gameItems == null || !gameItems.Any())
                {
                    Log.Warning("UpdateFromGameState: No carrier cargo items available");
                    return;
                }

                Log.Debug("Updating UI from game state: {Count} items", gameItems.Count);

                // Process all items from game state
                var itemsToUpdate = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in gameItems)
                {
                    itemsToUpdate[item.Name] = item.Quantity;
                }

                // Update UI collection
                foreach (var pair in itemsToUpdate)
                {
                    var existingItem = Cargo.FirstOrDefault(i =>
                        string.Equals(i.Name, pair.Key, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        // Update quantity of existing item
                        if (existingItem.Quantity != pair.Value)
                        {
                            Log.Debug("  Updating {Name}: {OldQty} → {NewQty}",
                                pair.Key, existingItem.Quantity, pair.Value);
                            existingItem.Quantity = pair.Value;
                        }
                    }
                    else
                    {
                        // Add new item
                        Log.Debug("  Adding new item: {Name} = {Qty}", pair.Key, pair.Value);
                        Cargo.Add(new CarrierCargoItem
                        {
                            Name = pair.Key,
                            Quantity = pair.Value
                        });
                    }
                }

                // Remove items that are no longer in the game state
                for (int i = Cargo.Count - 1; i >= 0; i--)
                {
                    var item = Cargo[i];
                    if (!itemsToUpdate.ContainsKey(item.Name) || itemsToUpdate[item.Name] <= 0)
                    {
                        Log.Debug("  Removing item: {Name}", item.Name);
                        Cargo.RemoveAt(i);
                    }
                }

                // Save updated data
                SaveCargoData();

                // Update visibility
                SetContextVisibility(Cargo.Count > 0);

                Log.Information("Fleet carrier cargo updated from game state: {Count} items", Cargo.Count);
            });
        }
        private void UpdateCargo()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var cargoItems = _gameState.CurrentCarrierCargo;
                if (cargoItems == null || !cargoItems.Any())
                {
                    Log.Warning("UpdateCargo: No carrier cargo items available");
                    return;
                }

                Log.Debug("UpdateCargo: Processing {Count} items from game state", cargoItems.Count);

                // Track which items we've seen for removal purposes
                var seenItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Process all items in CurrentCarrierCargo
                foreach (var gameItem in cargoItems)
                {
                    seenItems.Add(gameItem.Name);

                    // Try to find existing item in our UI collection
                    var existingItem = Cargo.FirstOrDefault(i =>
                        string.Equals(i.Name, gameItem.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        // Update quantity
                        if (existingItem.Quantity != gameItem.Quantity)
                        {
                            Log.Debug("Updating {0}: {1} → {2}",
                                gameItem.Name, existingItem.Quantity, gameItem.Quantity);
                            existingItem.Quantity = gameItem.Quantity;
                        }
                    }
                    else
                    {
                        // Add new item
                        Log.Debug("Adding new item: {0} = {1}", gameItem.Name, gameItem.Quantity);
                        Cargo.Add(new CarrierCargoItem
                        {
                            Name = gameItem.Name,
                            Quantity = gameItem.Quantity
                        });
                    }
                }

                // Remove items that aren't in the game state
                for (int i = Cargo.Count - 1; i >= 0; i--)
                {
                    var item = Cargo[i];
                    if (!seenItems.Contains(item.Name))
                    {
                        Log.Debug("Removing item not in game state: {0}", item.Name);
                        Cargo.RemoveAt(i);
                    }
                }

                // Set visibility and save data
                SetContextVisibility(Cargo.Count > 0);
                SaveCargoData();

                Log.Information("Fleet carrier cargo updated: {0} items in UI", Cargo.Count);
            });
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
                        foreach (var item in savedItems)
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
                var cargoList = Cargo.ToList();
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

            // Check if commodity already exists
            var existingItem = Cargo.FirstOrDefault(i =>
                i.Name.Equals(NewCommodityName, StringComparison.OrdinalIgnoreCase));

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