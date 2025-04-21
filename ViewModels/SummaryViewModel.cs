using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class SummaryViewModel : CardViewModel
    {
        #region Private Fields
        private readonly GameStateService _gameState;
        private System.Timers.Timer _carrierCountdownTimer;
        private SummaryItemViewModel _carrierCountdownItem;
        private string _fuelPanelTitle;
        private bool _showFuelBar;
        private double _fuelMain;
        private double _fuelReservoir;
        private double _fuelBarRatio;
        private bool _initialized = false;
        private bool _hasCommander;
        private bool _hasShip;
        private bool _hasFuel;

        #endregion

        #region Public Properties
        public ObservableCollection<SummaryItemViewModel> Items { get; } = new();

        public override double FontSize
        {
            get => base.FontSize;
            set
            {
                if (base.FontSize != value)
                {
                    base.FontSize = value;

                    // Update font size for all items
                    foreach (var item in Items)
                    {
                        item.FontSize = (int)value;
                    }
                }
            }
        }

        public string FuelPanelTitle
        {
            get => _fuelPanelTitle;
            set => SetProperty(ref _fuelPanelTitle, value);
        }

        public bool ShowFuelBar
        {
            get => _showFuelBar;
            set => SetProperty(ref _showFuelBar, value);
        }

        public double FuelMain
        {
            get => _fuelMain;
            set => SetProperty(ref _fuelMain, value);
        }

        public double FuelReservoir
        {
            get => _fuelReservoir;
            set => SetProperty(ref _fuelReservoir, value);
        }

        public double FuelBarRatio
        {
            get => _fuelBarRatio;
            set => SetProperty(ref _fuelBarRatio, value);
        }
        #endregion

        #region Constructor
        public SummaryViewModel(GameStateService gameState) : base("Summary")
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

            // Subscribe to property changes on the game state
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Removing LoadoutUpdated subscription for now
            // _gameState.LoadoutUpdated += OnLoadoutUpdated;

            // Force immediate initialization
            InitializeAllItems();

            // Schedule a delayed second initialization attempt
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                // Just call InitializeAllItems directly
                InitializeAllItems();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        #endregion

        #region Public Methods
        public void Initialize()
        {
            Log.Information("SummaryViewModel: Manual initialization requested");
            InitializeAllItems();
        }
        #endregion

        #region Private Methods
        private SummaryItemViewModel FindItemByTag(string tag)
        {
            return Items.FirstOrDefault(x => x.Tag == tag);
        }
        private void ForceInitializeItems()
        {
            // Force updates for all items, even if fields not all available yet
            UpdateCommanderItem();
            UpdateSquadronItem();
            UpdateShipItem();
            UpdateBalanceItem();
            UpdateSystemItem();
            UpdateFuelInfo();
            UpdateCarrierCountdown();

            // Schedule one more attempt after a longer delay
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                if (Items.Count < 2)
                {
                    Log.Information("🔄 Final attempt to initialize summary items");
                    InitializeAllItems();
                }
            }), DispatcherPriority.Background,
            TimeSpan.FromSeconds(2));
        }
        private async void EnsureDataIsLoaded()
        {
            // Wait a brief moment to let the application fully initialize
            await Task.Delay(500);

            // Check if we have data already
            if (Items.Count == 0 || _gameState.CommanderName == null)
            {
                Log.Information("SummaryViewModel initialization check - no data detected, forcing refresh");

                // Attempt to refresh the data
                InitializeAllItems();

                // If still no data, try once more after a delay
                if (Items.Count == 0)
                {
                    await Task.Delay(1000);
                    Log.Information("SummaryViewModel performing second initialization attempt");
                    InitializeAllItems();
                }
            }
        }


        private void RemoveNonCustomItems()
        {
            Log.Debug("🧹 Running RemoveNonCustomItems...");

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                Log.Debug("🔍 Inspecting item at index {Index}: Tag = {Tag}, Content = {Content}", i, item.Tag, item.Content);

                if (item.Tag == "CarrierJumpCountdown")
                {
                    Log.Debug("✅ Keeping CarrierJumpCountdown item");
                    continue;
                }

                Log.Debug("❌ Removing item with Tag = {Tag}", item.Tag);
                Items.RemoveAt(i);
            }

            Log.Debug("📦 Items after cleanup: {Count}", Items.Count);
        }
        private void OnLoadoutUpdated()
        {
            if (!_initialized)
            {
                Log.Information("📦 SummaryViewModel received LoadoutUpdated event — checking readiness...");
               
            }
        }


        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
         
            switch (e.PropertyName)
            {
                case nameof(GameStateService.CommanderName):
                    _hasCommander = true;
                    break;
                case nameof(GameStateService.ShipName):
                case nameof(GameStateService.ShipLocalised):
                case nameof(GameStateService.UserShipName):
                case nameof(GameStateService.UserShipId):
                    _hasShip = true;
                    break;
                case nameof(GameStateService.CurrentLoadout):
                case nameof(GameStateService.CurrentStatus):
                    _hasFuel = _gameState.CurrentLoadout != null && _gameState.CurrentStatus?.Fuel != null;
                    break;
            }

            // Trigger init when all critical info is available, once
            if (!_initialized && _hasCommander && _hasShip && _hasFuel)
            {
                Log.Information("🟢 SummaryViewModel: all critical data ready — initializing summary");
                _initialized = true;
                InitializeAllItems();
            }

            // Regular updates
            switch (e.PropertyName)
            {
                case nameof(GameStateService.CommanderName):
                    UpdateCommanderItem();
                    break;
                case nameof(GameStateService.SquadronName):
                    UpdateSquadronItem();
                    break;
                case nameof(GameStateService.ShipName):
                case nameof(GameStateService.ShipLocalised):
                case nameof(GameStateService.UserShipName):
                case nameof(GameStateService.UserShipId):
                    UpdateShipItem();
                    break;
                case nameof(GameStateService.Balance):
                    UpdateBalanceItem();
                    break;
                case nameof(GameStateService.CurrentSystem):
                    UpdateSystemItem();
                    break;
                case nameof(GameStateService.CurrentStatus):
                case nameof(GameStateService.CurrentLoadout):
                    UpdateFuelInfo();
                    break;
                case nameof(GameStateService.FleetCarrierJumpTime):
                case nameof(GameStateService.CarrierJumpDestinationSystem):
                    UpdateCarrierCountdown();
                    break;
            }
        }

        private void InitializeAllItems()
        {
            try
            {
                Log.Information("SummaryViewModel: InitializeAllItems called");
                RemoveNonCustomItems();

                // Even if CurrentStatus is null, still try to update the other items
                // that might have data available

                UpdateCommanderItem();
                UpdateSquadronItem();
                UpdateShipItem();
                UpdateBalanceItem();
                UpdateSystemItem();

                // These are more dependent on CurrentStatus, so check before calling
                if (_gameState.CurrentStatus != null)
                {
                    UpdateFuelInfo();
                    UpdateCarrierCountdown();
                }

                // Log final state
                Log.Debug("📋 Final Summary Items after initialization: {Count} items", Items.Count);
                foreach (var item in Items)
                {
                    Log.Debug("  - Tag: {Tag}, Content: {Content}", item.Tag, item.Content);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in InitializeAllItems");
            }
        }
        private void UpdateCommanderItem()
        {
            try
            {
                // More defensive check that handles the null case better
                if (_gameState == null)
                {
                    Log.Warning("UpdateCommanderItem: GameState is null");
                    return;
                }

                if (string.IsNullOrEmpty(_gameState.CommanderName))
                {
                    // Log this to help diagnose startup issues
                    Log.Debug("UpdateCommanderItem: CommanderName is null or empty");
                    return;
                }

                var item = FindItemByTag("Commander");
                if (item != null)
                {
                    item.Content = $"CMDR {_gameState.CommanderName}";
                    Log.Debug("Updated existing Commander item: {Content}", item.Content);
                }
                else
                {
                    var newItem = new SummaryItemViewModel(
                        "Commander",
                        $"CMDR {_gameState.CommanderName}",
                        Brushes.WhiteSmoke,
                        PackIconKind.AccountCircle)
                    {
                        FontSize = (int)this.FontSize
                    };

                    Items.Add(newItem);
                    Log.Debug("Added new Commander item: {Content}", newItem.Content);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating Commander item");
            }
        }
        private void UpdateSquadronItem()
        {
            try
            {
                var item = FindItemByTag("Squadron");

                if (string.IsNullOrEmpty(_gameState.SquadronName))
                {
                    // Remove squadron item if it exists and no squadron name
                    if (item != null)
                    {
                        Items.Remove(item);
                    }
                }
                else
                {
                    if (item != null)
                    {
                        item.Content = _gameState.SquadronName;
                    }
                    else
                    {
                        Items.Add(new SummaryItemViewModel(
                            "Squadron",
                            _gameState.SquadronName,
                            Brushes.LightGoldenrodYellow,
                            PackIconKind.AccountGroup)
                        {
                            FontSize = (int)this.FontSize
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating Squadron item");
            }
        }

        private void UpdateShipItem()
        {
            try
            {
                if (string.IsNullOrEmpty(_gameState.ShipName))
                    return;

                string shipDisplayName = !string.IsNullOrEmpty(_gameState.ShipLocalised)
                    ? _gameState.ShipLocalised
                    : ShipNameHelper.GetLocalisedName(_gameState.ShipName);

                string shipText = shipDisplayName;

                if (!string.IsNullOrEmpty(_gameState.UserShipName) || !string.IsNullOrEmpty(_gameState.UserShipId))
                {
                    shipText += " - ";

                    if (!string.IsNullOrEmpty(_gameState.UserShipName))
                        shipText += _gameState.UserShipName;

                    if (!string.IsNullOrEmpty(_gameState.UserShipId))
                        shipText += $" [{_gameState.UserShipId}]";
                }

                var item = FindItemByTag("Ship");
                if (item != null)
                {
                    item.Content = shipText;
                }
                else
                {
                    Items.Add(new SummaryItemViewModel(
                        "Ship",
                        shipText,
                        Brushes.LightBlue,
                        PackIconKind.SpaceStation)
                    {
                        FontSize = (int)this.FontSize
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating Ship item");
            }
        }

        private void UpdateBalanceItem()
        {
            try
            {
                if (!_gameState.Balance.HasValue)
                    return;

                string balanceText = $"{_gameState.Balance.Value:N0} Cr";
                var item = FindItemByTag("Balance");

                if (item != null)
                {
                    item.Content = balanceText;
                }
                else
                {
                    Items.Add(new SummaryItemViewModel(
                        "Balance",
                        balanceText,
                        Brushes.LightGreen,
                        PackIconKind.CurrencyUsd)
                    {
                        FontSize = (int)this.FontSize
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating Balance item");
            }
        }

        private void UpdateSystemItem()
        {
            try
            {
                if (string.IsNullOrEmpty(_gameState.CurrentSystem))
                    return;

                var item = FindItemByTag("System");
                if (item != null)
                {
                    item.Content = _gameState.CurrentSystem;
                }
                else
                {
                    Items.Add(new SummaryItemViewModel(
                        "System",
                        _gameState.CurrentSystem,
                        Brushes.Orange,
                        PackIconKind.Earth)
                    {
                        FontSize = (int)this.FontSize
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating System item");
            }
        }

        private string FormatCountdownText(TimeSpan time, string destination)
        {
            string timeText = time.TotalMinutes >= 60
                ? time.ToString(@"hh\:mm\:ss")
                : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";

            return $"Carrier Jump: {timeText}\nto {destination}";
        }

        private void UpdateCarrierCountdown()
        {
            try
            {
                if (_gameState.JumpCountdown is TimeSpan countdown && countdown.TotalSeconds > 0)
                {
                    StartCarrierCountdown(countdown, _gameState.CarrierJumpDestinationSystem);
                }
                else
                {
                    StopCarrierCountdown();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating carrier countdown");
            }
        }

        private void StopCarrierCountdown()
        {
            Log.Debug("🛑 Stopping CarrierCountdown");
            _carrierCountdownTimer?.Stop();
            _carrierCountdownTimer = null;

            if (_carrierCountdownItem != null && Items.Contains(_carrierCountdownItem))
            {
                Items.Remove(_carrierCountdownItem);
                _carrierCountdownItem = null;
            }
        }

        private void StartCarrierCountdown(TimeSpan initialCountdown, string destination)
        {
            var existing = FindItemByTag("CarrierJumpCountdown");

            if (existing == null)
            {
                _carrierCountdownItem = new SummaryItemViewModel(
                    "CarrierJumpCountdown",
                    FormatCountdownText(initialCountdown, destination),
                    Brushes.Gold,
                    PackIconKind.RocketLaunch)
                {
                    FontSize = (int)this.FontSize
                };

                Items.Add(_carrierCountdownItem);
                Log.Debug("➕ Added CarrierJumpCountdown to Items.");
            }
            else
            {
                _carrierCountdownItem = existing;
                _carrierCountdownItem.Content = FormatCountdownText(initialCountdown, destination);

                if (_carrierCountdownItem.Foreground != Brushes.Red &&
                    _carrierCountdownItem.Foreground != Brushes.LightGreen)
                {
                    _carrierCountdownItem.Foreground = Brushes.Gold;
                    _carrierCountdownItem.Pulse = false;
                }

                Log.Debug("🔁 Reusing existing CarrierJumpCountdown item.");
            }

            if (Items.IndexOf(_carrierCountdownItem) != Items.Count - 1)
            {
                Items.Remove(_carrierCountdownItem);
                Items.Add(_carrierCountdownItem);
            }

            Log.Debug("🚀 CarrierJumpCountdown item initialized");
            Log.Debug("   - Content: {Content}", _carrierCountdownItem.Content);
            Log.Debug("   - Foreground: {Foreground}", _carrierCountdownItem.Foreground.ToString());
            Log.Debug("   - Pulse: {Pulse}", _carrierCountdownItem.Pulse);
            Log.Debug("   - Items count: {Count}", Items.Count);

            if (_carrierCountdownTimer != null)
                return;

            var targetTime = DateTime.UtcNow.Add(initialCountdown);

            _carrierCountdownTimer = new System.Timers.Timer(1000);
            _carrierCountdownTimer.AutoReset = true;

            _carrierCountdownTimer.Elapsed += (s, e) =>
            {
                var remaining = targetTime - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    _carrierCountdownTimer.Stop();
                    _carrierCountdownTimer.Dispose();
                    _carrierCountdownTimer = null;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StopCarrierCountdown();
                        Log.Information("Carrier jump countdown reached zero — notifying GameStateService");

                        // Force a property change notification on CarrierJumpCountdownSeconds
                        OnPropertyChanged(nameof(_gameState.CarrierJumpCountdownSeconds));
                        OnPropertyChanged(nameof(_gameState.ShowCarrierJumpOverlay));

                        var status = _gameState.CurrentStatus;
                        var station = _gameState.CurrentStationName;
                        var carrierDest = _gameState.CarrierJumpDestinationSystem;

                        if (status?.Flags.HasFlag(Flag.Docked) == true &&
                            !string.IsNullOrEmpty(station) &&
                            !string.IsNullOrEmpty(carrierDest) &&
                            _gameState.IsOnFleetCarrier)
                        {
                            Log.Information("✅ Conditions met for carrier jump overlay");

                            // Explicitly set properties to ensure overlay shows
                            _gameState.GetType()
                                .GetProperty("CarrierJumpCountdownSeconds", BindingFlags.NonPublic | BindingFlags.Instance)
                                ?.SetValue(_gameState, 0);

                            // Notify that ShowCarrierJumpOverlay may have changed
                            _gameState.GetType()
                                .GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                                ?.Invoke(_gameState, new object[] { nameof(_gameState.ShowCarrierJumpOverlay) });
                        }
                    });

                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _carrierCountdownItem.Content = FormatCountdownText(remaining, _gameState.CarrierJumpDestinationSystem);

                    // Style logic
                    if (remaining.TotalMinutes <= 2.75)
                    {
                        if (_carrierCountdownItem.Foreground != Brushes.Red)
                        {
                            _carrierCountdownItem.Foreground = Brushes.Red;
                            _carrierCountdownItem.Pulse = true;
                        }
                    }
                    else if (remaining.TotalMinutes <= 10)
                    {
                        if (_carrierCountdownItem.Foreground != Brushes.Gold)
                        {
                            _carrierCountdownItem.Foreground = Brushes.Gold;
                            _carrierCountdownItem.Pulse = false;
                        }
                    }
                    else
                    {
                        if (_carrierCountdownItem.Foreground != Brushes.LightGreen)
                        {
                            _carrierCountdownItem.Foreground = Brushes.LightGreen;
                            _carrierCountdownItem.Pulse = false;
                        }
                    }
                });
            };

            _carrierCountdownTimer.Start();
        }

        private bool UpdateFuelInfo()
        {
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                return (bool)System.Windows.Application.Current.Dispatcher.Invoke(
                    new Func<bool>(UpdateFuelInfo));
            }

            try
            {
                var status = _gameState.CurrentStatus;
                if (status?.Fuel == null)
                {
                    if (ShowFuelBar)
                    {
                        ShowFuelBar = false;
                        return true;
                    }
                    return false;
                }

                bool fuelChanged = false;
                double currentFuelMain = 0;
                double currentFuelReservoir = 0;

                if (status.Flags.HasFlag(Flag.InSRV) && status.SRV != null)
                {
                    currentFuelMain = Math.Round(status.SRV.Fuel, 2);
                    currentFuelReservoir = 0;

                    if (Math.Abs(FuelMain - currentFuelMain) > 0.01)
                    {
                        FuelMain = currentFuelMain;
                        fuelChanged = true;
                    }

                    if (Math.Abs(FuelReservoir - currentFuelReservoir) > 0.01)
                    {
                        FuelReservoir = currentFuelReservoir;
                        fuelChanged = true;
                    }

                    if (Math.Abs(FuelBarRatio - FuelMain) > 0.01)
                    {
                        FuelBarRatio = FuelMain;
                        fuelChanged = true;
                    }

                    if (!ShowFuelBar)
                    {
                        ShowFuelBar = true;
                        fuelChanged = true;
                    }
                }
                else if (status.Fuel != null)
                {
                    // Find the FSD module in the loadout
                    var loadout = _gameState.CurrentLoadout;
                    var cargo = _gameState.CurrentCargo;

                    // Debug FSD module identification
                    var fsd = loadout?.Modules?.FirstOrDefault(m => m.Slot == "FrameShiftDrive");

                    if (fsd != null && loadout != null && status != null && cargo != null)
                    {
                        // Calculate the jump ranges
                        double maxRange = FsdJumpRangeCalculator.CalculateMaxJumpRange(fsd, loadout, status, cargo);
                        double currentRange = FsdJumpRangeCalculator.CalculateCurrentJumpRange(fsd, loadout, status, cargo);

                        // Update the fuel panel title
                        FuelPanelTitle = $"Fuel - Current: {currentRange:0.00} LY | Max: {maxRange:0.00} LY";

                        // Update fuel levels
                        currentFuelMain = Math.Round(status.Fuel.FuelMain, 2);
                        currentFuelReservoir = Math.Round(status.Fuel.FuelReservoir, 2);

                        if (Math.Abs(FuelMain - currentFuelMain) > 0.01)
                        {
                            FuelMain = currentFuelMain;
                            fuelChanged = true;
                        }

                        if (Math.Abs(FuelReservoir - currentFuelReservoir) > 0.01)
                        {
                            FuelReservoir = currentFuelReservoir;
                            fuelChanged = true;
                        }

                        if (loadout?.FuelCapacity?.Main > 0)
                        {
                            double max = loadout.FuelCapacity.Main;
                            double ratio = Math.Min(1.0, FuelMain / max);

                            if (Math.Abs(FuelBarRatio - ratio) > 0.01)
                            {
                                FuelBarRatio = ratio;
                                fuelChanged = true;
                            }

                            if (!ShowFuelBar)
                            {
                                ShowFuelBar = true;
                                fuelChanged = true;
                            }
                        }
                        else if (ShowFuelBar)
                        {
                            ShowFuelBar = false;
                            fuelChanged = true;
                        }
                    }
                    else
                    {
                        // Log which components are missing
                        Log.Warning("Missing components for jump range calculation: FSD={0}, Loadout={1}, Status={2}, Cargo={3}",
                            fsd != null, loadout != null, status != null, cargo != null);

                        if (ShowFuelBar)
                        {
                            ShowFuelBar = false;
                            fuelChanged = true;
                        }
                    }
                }
                else if (ShowFuelBar)
                {
                    ShowFuelBar = false;
                    fuelChanged = true;
                }

                return fuelChanged;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UpdateFuelInfo");
                if (ShowFuelBar)
                {
                    ShowFuelBar = false;
                    return true;
                }
                return false;
            }
        }
        #endregion
    }
}