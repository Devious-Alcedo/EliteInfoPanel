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
        private SummaryItemViewModel FindItemByTag(string tag)
        {
            return Items.FirstOrDefault(x => x.Tag == tag);
        }
        private string _fuelPanelTitle;
        public string FuelPanelTitle
        {
            get => _fuelPanelTitle;
            set => SetProperty(ref _fuelPanelTitle, value);
        }

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


        private void UpdateSummary()
        {
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdateSummary);
                return;
            }

            try
            {
                RemoveNonCustomItems();

                if (_gameState.CurrentStatus == null)
                    return;

                // Commander
                if (!string.IsNullOrEmpty(_gameState.CommanderName))
                {
                    Items.Add(new SummaryItemViewModel(
                        "Commander",
                        $"CMDR {_gameState.CommanderName}",
                        Brushes.WhiteSmoke,
                        PackIconKind.AccountCircle));
                }

                // Squadron
                if (!string.IsNullOrEmpty(_gameState.SquadronName))
                {
                    Items.Add(new SummaryItemViewModel(
                        "Squadron",
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
                        "Ship",
                        shipText,
                        Brushes.LightBlue,
                        PackIconKind.SpaceStation));
                }

                // Balance
                if (_gameState.Balance.HasValue)
                {
                    string balanceText = $"{_gameState.Balance.Value:N0} Cr";
                    Items.Add(new SummaryItemViewModel(
                        "Balance",
                        balanceText,
                        Brushes.LightGreen,
                        PackIconKind.CurrencyUsd));
                }

                // Current System
                if (!string.IsNullOrEmpty(_gameState.CurrentSystem))
                {
                    Items.Add(new SummaryItemViewModel(
                        "System",
                        _gameState.CurrentSystem,
                        Brushes.Orange,
                        PackIconKind.Earth));
                }

                // Heat
             


                UpdateFuelInfo();
                if (_gameState.JumpCountdown is TimeSpan countdown && countdown.TotalSeconds > 0)
                {
                    StartCarrierCountdown(countdown, _gameState.CarrierJumpDestinationSystem);
                }

                else
                {
                    StopCarrierCountdown();
                }

                Log.Debug("📋 Final Summary Items:");
                foreach (var item in Items)
                {
                    Log.Debug("  - Tag: {Tag}, Content: {Content}", item.Tag, item.Content);
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
                    FontSize = 24
                };

                Items.Add(_carrierCountdownItem);
                Log.Debug("➕ Added CarrierJumpCountdown to Items.");
            }
            else
            {
                _carrierCountdownItem = existing;
                _carrierCountdownItem.Content = FormatCountdownText(initialCountdown, destination);

                // ✅ Only reset color if not red or green
                if (_carrierCountdownItem.Foreground != Brushes.Red &&
                    _carrierCountdownItem.Foreground != Brushes.LightGreen)
                {
                    _carrierCountdownItem.Foreground = Brushes.Gold;
                    _carrierCountdownItem.Pulse = false;
                }

                Log.Debug("🔁 Reusing existing CarrierJumpCountdown item.");
            }

            // ✅ Move to end of list (if not already last)
            if (Items.IndexOf(_carrierCountdownItem) != Items.Count - 1)
            {
                Items.Remove(_carrierCountdownItem);
                Items.Add(_carrierCountdownItem);
            }

            Log.Debug("🚀 CarrierJumpCountdown item state:");
            Log.Debug("   - Content: {Content}", _carrierCountdownItem.Content);
            Log.Debug("   - Foreground: {Foreground}", _carrierCountdownItem.Foreground.ToString());
            Log.Debug("   - Pulse: {Pulse}", _carrierCountdownItem.Pulse);
            Log.Debug("   - Items count (post-add if new): {Count}", Items.Count);

            if (_carrierCountdownTimer != null)
                return;

            _carrierCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            var targetTime = DateTime.UtcNow.Add(initialCountdown);

            _carrierCountdownTimer.Tick += (s, e) =>
            {
                var remaining = targetTime - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    StopCarrierCountdown();

                    var status = _gameState.CurrentStatus;
                    var station = _gameState.CurrentStationName;
                    var carrierDest = _gameState.CarrierJumpDestinationSystem;

                    if (status?.Flags.HasFlag(Flag.Docked) == true &&
                        !string.IsNullOrEmpty(station) &&
                        !string.IsNullOrEmpty(carrierDest) &&
                        _gameState.CurrentSystem == carrierDest)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            if (App.Current.MainWindow?.DataContext is MainViewModel mainVm)
                            {
                                mainVm.IsCarrierJumping = true;
                            }
                        });
                    }

                    return;
                }

                _carrierCountdownItem.Content = FormatCountdownText(remaining, destination);
                _carrierCountdownItem.FontSize = 24;

                // ✅ Style logic
                if (remaining.TotalMinutes <= 2.75 && _carrierCountdownItem.Foreground != Brushes.Red)
                {
                    _carrierCountdownItem.Foreground = Brushes.Red;
                    _carrierCountdownItem.Pulse = true;
                }
                else if (remaining.TotalMinutes <= 10 && _carrierCountdownItem.Foreground != Brushes.Gold)
                {
                    _carrierCountdownItem.Foreground = Brushes.Gold;
                    _carrierCountdownItem.Pulse = false;
                }
                else if (_carrierCountdownItem.Foreground != Brushes.LightGreen)
                {
                    _carrierCountdownItem.Foreground = Brushes.LightGreen;
                    _carrierCountdownItem.Pulse = false;
                }

                Log.Debug("⏱ Tick Update: {Content}", _carrierCountdownItem.Content);
                Log.Debug(" - Foreground: {Foreground}", _carrierCountdownItem.Foreground.ToString());
                Log.Debug(" - Pulse: {Pulse}", _carrierCountdownItem.Pulse);
                Log.Debug(" - Items.Count: {Count}", Items.Count);

                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];
                    Log.Debug("   - Item[{0}] Tag={1}, Content={2}", i, item.Tag, item.Content);
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
                    double maxRange = FsdJumpRangeCalculator.EstimateMaxJumpRange(_gameState.CurrentLoadout);
                    FuelPanelTitle = $"Fuel - Max Range: {maxRange:0.00}LY";
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
