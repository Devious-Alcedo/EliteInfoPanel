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

            // Set up save path
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteInfoPanel");
            Directory.CreateDirectory(appDataFolder);
            _cargoSavePath = Path.Combine(appDataFolder, "CarrierCargo.json");

            // Initialize commands
            IncrementCommand = new RelayCommand(IncrementCommodity);
            DecrementCommand = new RelayCommand(DecrementCommodity);
            AddCommodityCommand = new RelayCommand(_ => AddCommodity(), _ => CanAddCommodity());

            // Load saved data first
            if (LoadSavedCargoData())
            {
                // If we successfully loaded saved data, ignore initial journal updates
                _ignoreJournalUpdates = true;
            }

            // Subscribe to property changed events
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Initial update if we don't have saved data
            if (Cargo.Count == 0)
            {
                UpdateCargo();
            }
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CurrentCarrierCargo))
            {
                Log.Information("FleetCarrierCargoViewModel received CurrentCarrierCargo update");

                // Skip the first update if we loaded saved data
                if (_ignoreJournalUpdates)
                {
                    _ignoreJournalUpdates = false;
                    Log.Information("Ignoring initial carrier cargo update in favor of saved data");
                    return;
                }

                UpdateCargo();
            }
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

                // Create dictionary of existing items
                var existingItems = new Dictionary<string, CarrierCargoItem>();
                foreach (var item in Cargo)
                {
                    existingItems[item.Name] = item;
                }

                // Track which items were updated
                var updatedItems = new HashSet<string>();

                foreach (var newItem in cargoItems)
                {
                    if (existingItems.TryGetValue(newItem.Name, out var existingItem))
                    {
                        // Update existing item
                        existingItem.Quantity = newItem.Quantity;
                    }
                    else
                    {
                        // Add new item
                        Cargo.Add(new CarrierCargoItem
                        {
                            Name = newItem.Name,
                            Quantity = newItem.Quantity
                        });
                    }

                    updatedItems.Add(newItem.Name);
                }

                // Remove items that no longer exist
                for (int i = Cargo.Count - 1; i >= 0; i--)
                {
                    var item = Cargo[i];
                    if (!updatedItems.Contains(item.Name) || item.Quantity <= 0)
                    {
                        Cargo.RemoveAt(i);
                    }
                }

                // Always ensure the card is visible if we have cargo
                SetContextVisibility(Cargo.Count > 0);

                // Save the updated data
                SaveCargoData();

                Log.Information("Fleet carrier cargo updated with {Count} items", Cargo.Count);
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
                SaveCargoData();
            }
        }

        private void DecrementCommodity(object parameter)
        {
            if (parameter is CarrierCargoItem item && item.Quantity > 0)
            {
                item.Quantity--;

                // Remove the item if quantity is zero
                if (item.Quantity == 0)
                {
                    Cargo.Remove(item);
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
                Log.Debug("Updated existing cargo: {Name} to {Quantity}",
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