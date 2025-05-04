// CargoViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class CargoItemViewModel : ViewModelBase
    {

        #region Private Fields

        private int _count;
        private int _fontSize = 14;
        private string _name;

        #endregion Private Fields

        #region Public Constructors

        public CargoItemViewModel(string name, int count)
        {
            _name = name;
            _count = count;
        }

        #endregion Public Constructors

        #region Public Properties

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        #endregion Public Properties
    }

    public class CargoViewModel : CardViewModel
    {

        #region Private Fields

        private readonly GameStateService _gameState;

        #endregion Private Fields

        #region Public Constructors

        public CargoViewModel(GameStateService gameState) : base("Cargo")
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

            // Subscribe to property changes from GameStateService
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Initial update
            UpdateCargo();
        }

        #endregion Public Constructors

        #region Public Properties

        public override double FontSize
        {
            get => base.FontSize;
            set
            {
                if (base.FontSize != value)
                {
                    base.FontSize = value;

                    foreach (var item in Items)
                    {
                        item.FontSize = (int)value;
                    }
                }
            }
        }

        public ObservableCollection<CargoItemViewModel> Items { get; } = new();

        #endregion Public Properties

        #region Private Methods

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Add specific logging for cargo changes
            if (e.PropertyName == nameof(GameStateService.CurrentCargo))
            {
                Log.Debug("CargoViewModel: Received CurrentCargo change notification");
                UpdateCargo();
            }
            else if (e.PropertyName == nameof(GameStateService.CurrentLoadout))
            {
                UpdateCargoTitle();
            }
            else if (e.PropertyName == nameof(GameStateService.CurrentStatus) ||
                     e.PropertyName == nameof(GameStateService.IsHyperspaceJumping))
            {
                UpdateVisibility();
            }
        }

        private void UpdateCargo()
        {
            try
            {
                // Log before any processing
                Log.Debug("CargoViewModel: UpdateCargo called - checking inventory");

                // Get cargo data
                var cargo = _gameState.CurrentCargo;
                bool hasInventory = cargo?.Inventory != null && cargo.Inventory.Count > 0;

                // Log cargo state immediately
                Log.Debug("CargoViewModel: Cargo data - HasInventory:{HasInventory}, ItemCount:{ItemCount}",
                    hasInventory, cargo?.Inventory?.Count ?? 0);

                RunOnUIThread(() =>
                {
                    // Clear existing items
                    Items.Clear();

                    // CRITICAL FIX: Set visibility directly based on inventory state
                    if (hasInventory && !(_gameState.CurrentStatus?.OnFoot == true) && !_gameState.IsHyperspaceJumping)
                    {
                        Log.Debug("CargoViewModel: Setting IsVisible = true");
                        IsVisible = true;

                        // Add items to display
                        foreach (var item in cargo.Inventory.OrderByDescending(i => i.Count))
                        {
                            string displayName = CommodityMapper.GetDisplayName(item.Name);
                            Items.Add(new CargoItemViewModel(displayName, item.Count)
                            {
                                FontSize = (int)this.FontSize
                            });
                        }

                        UpdateCargoTitle();
                        Log.Debug("CargoViewModel: Added {Count} items to display", Items.Count);
                    }
                    else
                    {
                        Log.Debug("CargoViewModel: Setting IsVisible = false");
                        IsVisible = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating cargo");
            }
        }
        private void UpdateCargoTitle()
        {
            try
            {
                int used = 0;
                int total = 0;

                if (_gameState.CurrentCargo?.Inventory != null)
                    used = _gameState.CurrentCargo.Inventory.Sum(i => i.Count);

                if (_gameState.CurrentLoadout != null)
                    total = _gameState.CurrentLoadout.CargoCapacity;

                Title = $"Cargo {used}/{total}";
                Log.Debug("CargoViewModel: Updated title to {Title}", Title);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating cargo title");
            }
        }

        // In CargoViewModel.cs
        private void UpdateVisibility()
        {
            try
            {
                // Get direct access to cargo
                var cargo = _gameState.CurrentCargo;
                bool hasInventory = cargo?.Inventory != null && cargo.Inventory.Count > 0;
                bool isOnFoot = _gameState.CurrentStatus?.OnFoot == true;
                bool isJumping = _gameState.IsHyperspaceJumping;

                // Direct visibility calculation
                bool shouldShow = hasInventory && !isOnFoot && !isJumping;

                // This might be directly setting IsVisible without respecting user preferences
                if (IsVisible != shouldShow)
                {
                    IsVisible = shouldShow; // THIS would also be a problem!
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating cargo visibility");
            }
        }

        #endregion Private Methods        
    }
}