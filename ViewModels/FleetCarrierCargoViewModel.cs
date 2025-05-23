using EliteInfoPanel.Controls;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace EliteInfoPanel.ViewModels
{
    public class FleetCarrierCargoViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private readonly string _cargoSavePath;
        private bool _initialSyncComplete = false;
        private string _newCommodityName;
        private bool _isInMainWindow = true;
        public bool IsInMainWindow
        {
            get => _isInMainWindow;
            set => SetProperty(ref _isInMainWindow, value);
        }

        public RelayCommand OpenInNewWindowCommand { get; }
        private int _newCommodityQuantity = 1;
        private Dictionary<string, int> _lastKnownGameState = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public RelayCommand UpdateQuantityCommand { get; }
        public RelayCommand DeleteItemCommand { get; }
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
            OpenInNewWindowCommand = new RelayCommand(_ => OpenInNewWindow());

            // Initialize commands
            UpdateQuantityCommand = new RelayCommand(UpdateItemQuantity);
            DeleteItemCommand = new RelayCommand(DeleteItem);
            IncrementCommand = new RelayCommand(IncrementCommodity);
            DecrementCommand = new RelayCommand(DecrementCommodity);
            AddCommodityCommand = new RelayCommand(_ => AddCommodity(), _ => CanAddCommodity());

            // Get the current cargo from GameStateService
            if (_gameState.CurrentCarrierCargo != null)
            {
                foreach (var item in _gameState.CurrentCarrierCargo)
                {
                    Cargo.Add(new CarrierCargoItem
                    {
                        Name = item.Name,
                        Quantity = item.Quantity,
                        FontSize = (int)this.FontSize
                    });
                }
                SetContextVisibility(Cargo.Count > 0);
            }

            // Capture initial game state for delta tracking
            CaptureGameState();

            // Subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Mark initialization as complete
            _initialSyncComplete = true;
            Log.Information("Fleet carrier cargo initialization complete - real-time updates enabled");
        }
        private void OpenInNewWindow()
        {
            // Load settings
            var settings = SettingsManager.Load();

            // Create a new instance of the viewmodel with IsInMainWindow=false
            var popupViewModel = new FleetCarrierCargoViewModel(_gameState)
            {
                IsInMainWindow = false
            };

            // Create a new window
            var window = new Window
            {
                Title = "Fleet Carrier Cargo",
                Width = settings.FleetCarrierWindowWidth,
                Height = settings.FleetCarrierWindowHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = settings.FleetCarrierWindowLeft,
                Top = settings.FleetCarrierWindowTop,
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                Content = new FleetCarrierCargoCard { DataContext = popupViewModel }
            };

            // Add event handlers to save position
            window.LocationChanged += (s, e) =>
            {
                if (window.WindowState == WindowState.Normal)
                {
                    settings.FleetCarrierWindowLeft = window.Left;
                    settings.FleetCarrierWindowTop = window.Top;
                    SettingsManager.Save(settings);
                }
            };

            window.SizeChanged += (s, e) =>
            {
                if (window.WindowState == WindowState.Normal)
                {
                    settings.FleetCarrierWindowWidth = window.Width;
                    settings.FleetCarrierWindowHeight = window.Height;
                    SettingsManager.Save(settings);
                }
            };

            // Ensure window is within screen bounds
            EnsureWindowIsVisible(window, settings);

            // Show the window
            window.Show();
        }

        
        private void EnsureWindowIsVisible(Window window, AppSettings settings)
        {
            // Get screen information
            var screens = WpfScreenHelper.Screen.AllScreens;
            var screenBounds = WpfScreenHelper.Screen.AllScreens.First().Bounds;

            // Check if window position is valid
            bool isPositionValid = false;
            foreach (var screen in screens)
            {
                var bounds = screen.Bounds;
                if (settings.FleetCarrierWindowLeft >= bounds.Left &&
                    settings.FleetCarrierWindowTop >= bounds.Top &&
                    settings.FleetCarrierWindowLeft + settings.FleetCarrierWindowWidth <= bounds.Right &&
                    settings.FleetCarrierWindowTop + settings.FleetCarrierWindowHeight <= bounds.Bottom)
                {
                    isPositionValid = true;
                    break;
                }
            }

            // If position is invalid, center on primary screen
            if (!isPositionValid)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // After window is loaded, save the new position
                window.Loaded += (s, e) =>
                {
                    settings.FleetCarrierWindowLeft = window.Left;
                    settings.FleetCarrierWindowTop = window.Top;
                    settings.FleetCarrierWindowWidth = window.Width;
                    settings.FleetCarrierWindowHeight = window.Height;
                    SettingsManager.Save(settings);
                };
            }
        }
        private void UpdateItemQuantity(object parameter)
        {
            if (parameter is CarrierCargoItem item)
            {
                // Validate the quantity
                if (item.Quantity >= 0)
                {
                    if (item.Quantity == 0)
                    {
                        // If quantity is zero, delete the item
                        DeleteItem(item);
                        return;
                    }

                    // Update the GameState with the new quantity
                    _gameState.UpdateCarrierCargoItem(item.Name, item.Quantity);

                    // Save data after update
                   
                    Log.Debug("Updated {Name} quantity to {Quantity}", item.Name, item.Quantity);
                }
                else
                {
                    // If negative quantity, revert to 1 as minimum
                    item.Quantity = 1;
                    _gameState.UpdateCarrierCargoItem(item.Name, item.Quantity);
                    Log.Warning("Invalid quantity input for {Name}, set to minimum of 1", item.Name);
                }
            }
        }

        private void DeleteItem(object parameter)
        {
            if (parameter is CarrierCargoItem item)
            {
                Log.Debug("Deleting item: {Name}", item.Name);

                // Update GameState (setting quantity to 0 removes the item)
                _gameState.UpdateCarrierCargoItem(item.Name, 0);

                // Remove from UI collection directly
                Cargo.Remove(item);

                // Save after deletion
     
            }
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
                    // CurrentCarrierCargo already uses display names
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
                // If game state is empty but we have items, clear them
                if (Cargo.Any())
                {
                    Cargo.Clear();
                    SetContextVisibility(false);
                    Log.Information("Cleared all carrier cargo items - game state is empty");
                }
                return;
            }

            bool madeChanges = false;

            // Build current game state dictionary using display names (which is what CurrentCarrierCargo uses)
            var currentStateDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in currentGameState)
            {
                // CurrentCarrierCargo already contains display names
                currentStateDict[item.Name] = item.Quantity;
            }

            // Look for differences from last known state
            foreach (var pair in currentStateDict)
            {
                if (!_lastKnownGameState.TryGetValue(pair.Key, out int previousQuantity) ||
                    previousQuantity != pair.Value)
                {
                    // This is a real change in quantity
                    Log.Debug("Real-time change detected: {Item} changed by {Delta} ({OldValue} → {NewValue})",
                        pair.Key, pair.Value - previousQuantity, previousQuantity, pair.Value);

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
                            Name = pair.Key,
                            Quantity = pair.Value,
                            FontSize = (int)this.FontSize
                        });
                        madeChanges = true;
                        Log.Debug("Added new item to UI: {Item} = {Quantity}", pair.Key, pair.Value);
                    }
                }
            }

            // Check for items that were removed completely
            var itemsToRemove = new List<CarrierCargoItem>();
            foreach (var item in Cargo)
            {
                if (!currentStateDict.ContainsKey(item.Name))
                {
                    itemsToRemove.Add(item);
                    madeChanges = true;
                }
            }

            foreach (var item in itemsToRemove)
            {
                Cargo.Remove(item);
                Log.Debug("Removed item completely from UI: {Item} (no longer in game state)", item.Name);
            }

            // Update last known state
            _lastKnownGameState = new Dictionary<string, int>(currentStateDict, StringComparer.OrdinalIgnoreCase);

            // Update visibility
            if (madeChanges)
            {
                SetContextVisibility(Cargo.Count > 0);
                Log.Information("Applied real-time changes to fleet carrier cargo - now {Count} items", Cargo.Count);
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