using System;
using System.Collections.ObjectModel;
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
        private readonly GameStateService _gameState;
        private DispatcherTimer _carrierCountdownTimer;
        private SummaryItemViewModel _carrierCountdownItem;
        public ObservableCollection<SummaryItemViewModel> Items { get; } = new();

        private bool _showFuelBar;
        public bool ShowFuelBar
        {
            get => _showFuelBar;
            set => SetProperty(ref _showFuelBar, value);
        }

        private double _fuelMain;
        public double FuelMain
        {
            get => _fuelMain;
            set => SetProperty(ref _fuelMain, value);
        }

        private double _fuelReservoir;
        public double FuelReservoir
        {
            get => _fuelReservoir;
            set => SetProperty(ref _fuelReservoir, value);
        }

        private double _fuelBarRatio;
        public double FuelBarRatio
        {
            get => _fuelBarRatio;
            set => SetProperty(ref _fuelBarRatio, value);
        }

        public SummaryViewModel(GameStateService gameState) : base("Summary")
        {
            _gameState = gameState;
            _gameState.DataUpdated += UpdateSummary;
            UpdateSummary();
        }

        public void Initialize()
        {
            Log.Information("SummaryViewModel: Manual initialization requested");
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdateSummary);
                return;
            }

            try
            {
                Items.Clear();

                if (_gameState.CurrentStatus == null)
                    return;

                // Commander
                if (!string.IsNullOrEmpty(_gameState.CommanderName))
                {
                    Items.Add(new SummaryItemViewModel(
                        $"CMDR {_gameState.CommanderName}",
                        Brushes.WhiteSmoke,
                        PackIconKind.AccountCircle));
                }

                // Squadron
                if (!string.IsNullOrEmpty(_gameState.SquadronName))
                {
                    Items.Add(new SummaryItemViewModel(
                        _gameState.SquadronName,
                        Brushes.LightGoldenrodYellow,
                        PackIconKind.AccountGroup));
                }

                // Ship
                if (!string.IsNullOrEmpty(_gameState.ShipName))
                {
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

                    Items.Add(new SummaryItemViewModel(
                        shipText,
                        Brushes.LightBlue,
                        PackIconKind.SpaceStation));
                }

                // Balance
                if (_gameState.Balance.HasValue)
                {
                    string balanceText = $"{_gameState.Balance.Value:N0} Cr";
                    Items.Add(new SummaryItemViewModel(
                        balanceText,
                        Brushes.LightGreen,
                        PackIconKind.CurrencyUsd));
                }

                // Current System
                if (!string.IsNullOrEmpty(_gameState.CurrentSystem))
                {
                    Items.Add(new SummaryItemViewModel(
                        _gameState.CurrentSystem,
                        Brushes.Orange,
                        PackIconKind.Earth));
                }

                // Heat
                if (_gameState.CurrentStatus?.Heat > 0.75f)
                {
                    var heatColor = _gameState.CurrentStatus.Heat > 0.95f ? Brushes.Red : Brushes.Orange;
                    Items.Add(new SummaryItemViewModel(
                        $"Heat: {_gameState.CurrentStatus.Heat * 100:F0}%",
                        heatColor,
                        PackIconKind.Thermometer));
                }


                UpdateFuelInfo();
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
                Log.Error(ex, "Error in UpdateSummary");
            }
        }
        private string FormatCountdownText(TimeSpan time, string destination)
        {
            string timeText = time.TotalMinutes >= 60
                ? time.ToString(@"hh\:mm\:ss")
                : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";

            return $"Carrier Jump: {timeText}\nto {destination}";

        }
        private void StopCarrierCountdown()
        {
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
            StopCarrierCountdown(); // Stop any existing one

            _carrierCountdownItem = new SummaryItemViewModel(
                "CarrierJumpCountdown",
                FormatCountdownText(initialCountdown, destination),
                Brushes.Gold, // Initial color
                PackIconKind.RocketLaunch)
            {
                FontSize = 24
            };

            Items.Add(_carrierCountdownItem);

            _carrierCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            DateTime targetTime = DateTime.UtcNow.Add(initialCountdown);

            _carrierCountdownTimer.Tick += (s, e) =>
            {
                TimeSpan remaining = targetTime - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    StopCarrierCountdown();
                    return;
                }

                _carrierCountdownItem.Content = FormatCountdownText(remaining, destination);
                _carrierCountdownItem.FontSize = 24;

                // Set color and pulse
                if (remaining.TotalMinutes <= 2.75)
                {
                    _carrierCountdownItem.Foreground = Brushes.Red;
                    _carrierCountdownItem.Pulse = true;
                }
                else if (remaining.TotalMinutes <= 5)
                {
                    _carrierCountdownItem.Foreground = Brushes.Gold;
                    _carrierCountdownItem.Pulse = false;
                }
                else
                {
                    _carrierCountdownItem.Foreground = Brushes.LightGreen;
                    _carrierCountdownItem.Pulse = false;
                }
            };


            _carrierCountdownTimer.Start();
        }


        private void UpdateFuelInfo()
        {
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

                if (status.Flags.HasFlag(Flag.InSRV) && status.SRV != null)
                {
                    FuelMain = Math.Round(status.SRV.Fuel, 2);
                    FuelReservoir = 0;
                    FuelBarRatio = FuelMain;
                    ShowFuelBar = true;
                }
                else if (status.Fuel != null)
                {
                    FuelMain = Math.Round(status.Fuel.FuelMain, 2);
                    FuelReservoir = Math.Round(status.Fuel.FuelReservoir, 2);

                    var loadout = _gameState.CurrentLoadout;
                    if (loadout?.FuelCapacity?.Main > 0)
                    {
                        double max = loadout.FuelCapacity.Main;
                        FuelBarRatio = Math.Min(1.0, FuelMain / max);
                        ShowFuelBar = true;
                    }
                    else
                    {
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
                Log.Error(ex, "Error in UpdateFuelInfo");
                ShowFuelBar = false;
            }
        }
    }
}
