// SummaryViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class SummaryViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private bool _showFuelBar;
        private double _fuelMain;
        private double _fuelReservoir;
        private double _fuelBarRatio;

        public ObservableCollection<SummaryItemViewModel> Items { get; } = new();

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

        public SummaryViewModel(GameStateService gameState) : base("Summary")
        {
            _gameState = gameState;

            // Subscribe to game state updates
            _gameState.DataUpdated += UpdateSummary;

            // Initial update
            UpdateSummary();
        }
        /// <summary>
        /// Updates an ObservableCollection safely on the UI thread
        /// </summary>
        protected void UpdateCollection<T>(System.Collections.ObjectModel.ObservableCollection<T> collection,
                                          Action<System.Collections.ObjectModel.ObservableCollection<T>> updateAction)
        {
            if (collection == null || updateAction == null) return;

            // Get the application dispatcher
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                // Fallback to current dispatcher if Application.Current is null
                dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            }

            if (dispatcher.CheckAccess())
            {
                // We're already on the UI thread
                try
                {
                    updateAction(collection);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error updating collection on UI thread");
                }
            }
            else
            {
                // We need to invoke the action on the UI thread
                try
                {
                    dispatcher.Invoke(() => updateAction(collection), System.Windows.Threading.DispatcherPriority.DataBind);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error dispatching collection update to UI thread");
                }
            }
        }
        // Public method to force a refresh of the data
        public void Initialize()
        {
            Log.Information("SummaryViewModel: Manual initialization requested");
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            // Make sure we're on the UI thread
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdateSummary);
                return;
            }

            try
            {
                // Now we're safely on the UI thread
                Items.Clear();

                if (_gameState.CurrentStatus == null)
                    return;

                // Add Commander Name
                if (!string.IsNullOrEmpty(_gameState.CommanderName))
                {
                    Items.Add(new SummaryItemViewModel(
                        "Commander",
                        $"CMDR {_gameState.CommanderName}",
                        Brushes.WhiteSmoke,
                        PackIconKind.AccountCircle));
                }

                // Add Ship Name and ID
                if (!string.IsNullOrEmpty(_gameState.ShipLocalised) || !string.IsNullOrEmpty(_gameState.ShipName))
                {
                    string shipDisplayName = !string.IsNullOrEmpty(_gameState.ShipLocalised)
                        ? _gameState.ShipLocalised
                        : ShipNameHelper.GetLocalisedName(_gameState.ShipName);

                    string shipText = shipDisplayName;

                    // Add ship name and ID if available
                    if (!string.IsNullOrEmpty(_gameState.UserShipName) || !string.IsNullOrEmpty(_gameState.UserShipId))
                    {
                        shipText += " - ";

                        if (!string.IsNullOrEmpty(_gameState.UserShipName))
                            shipText += _gameState.UserShipName;

                        if (!string.IsNullOrEmpty(_gameState.UserShipId))
                            shipText += $" [{_gameState.UserShipId}]";
                    }

                    Items.Add(new SummaryItemViewModel(
                        "Ship",
                        shipText,
                        Brushes.LightBlue,
                        PackIconKind.SpaceStation));
                }

                // Add balance
                if (_gameState.Balance.HasValue)
                {
                    string balanceText = $"{_gameState.Balance.Value:N0} Cr";
                    Items.Add(new SummaryItemViewModel(
                        "Balance",
                        balanceText,
                        Brushes.LightGreen,
                        PackIconKind.CurrencyUsd));
                }

                // Add current system if available
                if (!string.IsNullOrEmpty(_gameState.CurrentSystem))
                {
                    Items.Add(new SummaryItemViewModel(
                        "System",
                        $"System: {_gameState.CurrentSystem}",
                        Brushes.Orange,
                        PackIconKind.Earth));
                }

                // Add Squadron if available
                if (!string.IsNullOrEmpty(_gameState.SquadronName))
                {
                    Items.Add(new SummaryItemViewModel(
                        "Squadron",
                        $"Squadron: {_gameState.SquadronName}",
                        Brushes.LightGoldenrodYellow,
                        PackIconKind.AccountGroup));
                }

                // Add heat level if too high
                if (_gameState.CurrentStatus?.Heat > 0.75f)
                {
                    var heatColor = _gameState.CurrentStatus.Heat > 0.95f ? Brushes.Red : Brushes.Orange;
                    Items.Add(new SummaryItemViewModel(
                        "Heat",
                        $"Heat: {_gameState.CurrentStatus.Heat * 100:F0}%",
                        heatColor,
                        PackIconKind.Thermometer));
                }

                // Add carrier jump countdown if available
                if (_gameState.JumpCountdown.HasValue && _gameState.JumpCountdown.Value.TotalSeconds > 0)
                {
                    var timeLeft = _gameState.JumpCountdown.Value;
                    var jumpColor = timeLeft.TotalMinutes < 5 ? Brushes.Red :
                                   timeLeft.TotalMinutes < 10 ? Brushes.Yellow :
                                   Brushes.LightGreen;

                    string jumpText = $"Carrier Jump: {timeLeft:hh\\:mm\\:ss}";
                    if (!string.IsNullOrEmpty(_gameState.CarrierJumpDestinationSystem))
                    {
                        jumpText += $" to {_gameState.CarrierJumpDestinationSystem}";
                    }

                    Items.Add(new SummaryItemViewModel(
                        "CarrierJump",
                        jumpText,
                        jumpColor,
                        PackIconKind.RocketLaunch));
                }

                // Update fuel information
                UpdateFuelInfo();
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Log.Error(ex, "Error in UpdateSummary");
            }
        }
        private void UpdateFuelInfo()
        {
            // Make sure we're on the UI thread
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdateFuelInfo);
                return;
            }

            try
            {
                var status = _gameState.CurrentStatus;

                if (status?.Fuel == null)
                {
                    ShowFuelBar = false;
                    return;
                }

                // Check if we're in an SRV
                if (status.Flags.HasFlag(Flag.InSRV) && status.SRV != null)
                {
                    // SRV Fuel mode
                    FuelMain = Math.Round(status.SRV.Fuel, 2);
                    FuelReservoir = 0; // SRV doesn't have reservoir
                    FuelBarRatio = FuelMain; // SRV fuel is already a ratio (0.0-1.0)
                    ShowFuelBar = true;
                }
                else if (status.Fuel != null)
                {
                    // Standard ship fuel mode
                    FuelMain = Math.Round(status.Fuel.FuelMain, 2);
                    FuelReservoir = Math.Round(status.Fuel.FuelReservoir, 2);

                    // Get max fuel capacity from loadout
                    var loadout = _gameState.CurrentLoadout;
                    if (loadout?.FuelCapacity?.Main > 0)
                    {
                        double max = loadout.FuelCapacity.Main;
                        FuelBarRatio = Math.Min(1.0, FuelMain / max);
                        ShowFuelBar = true;
                    }
                    else
                    {
                        // No fuel capacity data available
                        ShowFuelBar = false;
                    }
                }
                else
                {
                    ShowFuelBar = false;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Log.Error(ex, "Error in UpdateFuelInfo");
                ShowFuelBar = false;
            }
        }
    }
}