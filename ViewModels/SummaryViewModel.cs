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
            // null check
            if (_gameState == null)
            {
                Log.Error("GameStateService is null in UpdateSummary");
                return;
            }
            if (System.Windows.Application.Current == null || !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(UpdateSummary);
                return;
            }


            try
            {
                RemoveNonCustomItems();
                int fontSize = (int)this.FontSize;
                if (_gameState.CurrentStatus == null)
                    return;

                // Commander
                if (!string.IsNullOrEmpty(_gameState.CommanderName))
                {
                    Items.Add(new SummaryItemViewModel(
                        "Commander",
                        $"CMDR {_gameState.CommanderName}",
                        Brushes.WhiteSmoke,
                        PackIconKind.AccountCircle)
                    {
                        FontSize = fontSize
                    });
                }

                // Squadron
                if (!string.IsNullOrEmpty(_gameState.SquadronName))
                {
                    Items.Add(new SummaryItemViewModel(
                        "Squadron",
                        _gameState.SquadronName,
                        Brushes.LightGoldenrodYellow,
                        PackIconKind.AccountGroup)
                    {
                        FontSize = fontSize
                    });
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
                        PackIconKind.SpaceStation)
                    {
                        FontSize = fontSize
                    });
                }

                // Balance
                if (_gameState.Balance.HasValue)
                {
                    string balanceText = $"{_gameState.Balance.Value:N0} Cr";
                    Items.Add(new SummaryItemViewModel(
                        "Balance",
                        balanceText,
                        Brushes.LightGreen,
                        PackIconKind.CurrencyUsd)
                    {
                        FontSize = fontSize
                    });
                }

                // Current System
                if (!string.IsNullOrEmpty(_gameState.CurrentSystem))
                {
                    Items.Add(new SummaryItemViewModel(
                        "System",
                        _gameState.CurrentSystem,
                        Brushes.Orange,
                        PackIconKind.Earth)
                    {
                        FontSize = fontSize
                    });
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
                    FontSize = (int)this.FontSize

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
                    // Find the FSD module in the loadout
                    var loadout = _gameState.CurrentLoadout;
                    var cargo = _gameState.CurrentCargo;

                    // Debug FSD module identification
                    var fsd = loadout?.Modules?.FirstOrDefault(m => m.Slot == "FrameShiftDrive");

                    if (fsd != null && loadout != null && status != null && cargo != null)
                    {
                        // Log detailed information about the parameters for debugging
                        var fsdKey = FsdJumpRangeCalculator.GetFsdSpecKeyFromItem(fsd.Item);
                        double cargoMass = cargo?.Inventory?.Sum(i => i.Count) ?? 0;

                        Log.Information("🚀 Jump Range Debug - Parameters:");
                        Log.Information("  - FSD Module: {0}, Key: {1}", fsd.Item, fsdKey);
                        Log.Information("  - Loadout: UnladenMass={0}, Game MaxJumpRange={1}",
                            loadout.UnladenMass, loadout.MaxJumpRange);
                        Log.Information("  - Fuel: Main={0}, Reserve={1}",
                            status.Fuel.FuelMain, status.Fuel.FuelReservoir);
                        Log.Information("  - Cargo Mass: {0}", cargoMass);

                        // Log FSD engineering if present
                        if (fsd.Engineering != null && fsd.Engineering.Modifiers != null)
                        {
                            var optMassModifier = fsd.Engineering.Modifiers
                                .FirstOrDefault(m => m.Label.Equals("FSDOptimalMass", StringComparison.OrdinalIgnoreCase));
                            if (optMassModifier != null)
                            {
                                Log.Information("  - FSD Engineering: OptimalMass={0}", optMassModifier.Value);
                            }
                        }

                        // Try different base constant values for debugging
                        if (fsdKey != null)
                        {
                            int size = int.Parse(fsdKey[0].ToString());
                            char rating = fsdKey[1];

                            // Get constants from dictionaries (assuming these are accessible)
                            // This is just pseudocode - adjust to match your actual implementation
                            double classConstant = 0;
                            double ratingConstant = 0;

                            // Use reflection to get the constants if they're private
                            var classConstants = typeof(FsdJumpRangeCalculator)
                                .GetField("ClassConstants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                                ?.GetValue(null) as Dictionary<int, double>;

                            var ratingConstants = typeof(FsdJumpRangeCalculator)
                                .GetField("RatingConstants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                                ?.GetValue(null) as Dictionary<char, double>;

                            if (classConstants != null && ratingConstants != null)
                            {
                                classConstants.TryGetValue(size, out classConstant);
                                ratingConstants.TryGetValue(rating, out ratingConstant);

                                Log.Information("  - Constants: Class={0}, Rating={1}", classConstant, ratingConstant);
                            }
                        }

                        // Calculate the jump ranges
                        double maxRange = FsdJumpRangeCalculator.CalculateMaxJumpRange(fsd, loadout, status, cargo);
                        double currentRange = FsdJumpRangeCalculator.CalculateCurrentJumpRange(fsd, loadout, status, cargo);

                        // Compare with game provided value if available
                        if (loadout.MaxJumpRange > 0)
                        {
                            Log.Information("  - COMPARISON - Calculated Max: {0:0.00} LY, Game Max: {1:0.00} LY, Ratio: {2:0.00}",
                                maxRange, loadout.MaxJumpRange, loadout.MaxJumpRange / maxRange);
                        }

                        Log.Information("  - Final calculated values - Current: {0:0.00} LY, Max: {1:0.00} LY",
                            currentRange, maxRange);

                        FuelPanelTitle = $"Fuel - Current: {currentRange:0.00} LY | Max: {maxRange:0.00} LY";
                    }
                    else
                    {
                        // Log which components are missing
                        Log.Warning("Missing components for jump range calculation: FSD={0}, Loadout={1}, Status={2}, Cargo={3}",
                            fsd != null, loadout != null, status != null, cargo != null);

                        FuelPanelTitle = "Fuel - Jump range: Unknown";
                    }

                    FuelMain = Math.Round(status.Fuel.FuelMain, 2);
                    FuelReservoir = Math.Round(status.Fuel.FuelReservoir, 2);
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
