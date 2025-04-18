using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;
using TextCopy;

namespace EliteInfoPanel.ViewModels
{
    public class RouteViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private int _fontSize = 14;
        private int _jumpsUntilRefuel;
        private double _availableHeight;

        public double AvailableHeight
        {
            get => _availableHeight;
            set
            {
                if (SetProperty(ref _availableHeight, value))
                    UpdateRoute();
            }
        }

        public int JumpsUntilRefuel
        {
            get => _jumpsUntilRefuel;
            set => SetProperty(ref _jumpsUntilRefuel, value);
        }

        private bool _needsRefueling;
        public bool NeedsRefueling
        {
            get => _needsRefueling;
            set => SetProperty(ref _needsRefueling, value);
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
                        item.FontSize = (int)value;
                }
            }
        }

        public ObservableCollection<RouteItemViewModel> Items { get; } = new();
        public ICommand CopySystemNameCommand { get; }

        public RouteViewModel(GameStateService gameState) : base("Nav Route")
        {
            _gameState = gameState;
            CopySystemNameCommand = new RelayCommand(CopySystemName);
            _gameState.DataUpdated += UpdateRoute;
            UpdateRoute();
        }

        private void UpdateRoute()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Clear();

            var jumps = _gameState.CurrentRoute?.Route?.Where(j => j.StarPos?.Length == 3).ToList();
            if (jumps?.Count > 0)
                _gameState.CurrentSystemCoordinates = (jumps[0].StarPos[0], jumps[0].StarPos[1], jumps[0].StarPos[2]);
            else
                _gameState.CurrentSystemCoordinates = null;

            if (_gameState.RouteWasActive && _gameState.RouteCompleted && !_gameState.IsInHyperspace)
            {
                ShowToast("Route complete! You've arrived at your destination.");
                _gameState.ResetRouteActivity();
            }

            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true;
            bool hasDestination = !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
            IsVisible = hasRoute || hasDestination;
            if (!IsVisible) return;

            bool isTargetInSameSystem = string.Equals(_gameState.CurrentSystem, _gameState.LastFsdTargetSystem, StringComparison.OrdinalIgnoreCase);

            if (_gameState.RemainingJumps.HasValue && !isTargetInSameSystem)
            {
                Items.Add(new RouteItemViewModel($"Jumps Remaining: {_gameState.RemainingJumps.Value}", null, null, RouteItemType.Info)
                {
                    FontSize = (int)this.FontSize
                });
            }

            if (hasDestination)
            {
                string destination = _gameState.CurrentStatus.Destination?.Name;
                string lastRouteSystem = _gameState.CurrentRoute?.Route?.LastOrDefault()?.StarSystem;

                if (!string.Equals(destination, lastRouteSystem, StringComparison.OrdinalIgnoreCase))
                {
                    Items.Add(new RouteItemViewModel($"Target: {FormatDestinationName(_gameState.CurrentStatus.Destination)}", null, null, RouteItemType.Destination)
                    {
                        FontSize = (int)this.FontSize
                    });
                }
            }

            var fuelStatus = _gameState.CurrentStatus?.Fuel;
            var fsd = _gameState.CurrentLoadout?.Modules?.FirstOrDefault(m => m.Slot == "FrameShiftDrive");
            var loadout = _gameState.CurrentLoadout;
            var cargo = _gameState.CurrentCargo;

            double currentFuel = fuelStatus?.FuelMain ?? 0;
            double maxFuelCapacity = loadout?.FuelCapacity?.Main ?? 0;

            bool canEstimate = fsd != null && loadout != null && cargo != null && currentFuel > 0;

            if (canEstimate)
            {
                Items.Add(new RouteItemViewModel($"Current Fuel: {currentFuel:0.00}/{maxFuelCapacity:0.00} T", null, null, RouteItemType.Info)
                {
                    FontSize = (int)this.FontSize
                });
            }

            if (_gameState.CurrentRoute?.Route == null) return;

            double remainingFuel = currentFuel;
            var currentPos = _gameState.CurrentSystemCoordinates;
            bool refuelNeeded = false;
            int jumpsUntilRefuel = 0;

            var nextJumps = _gameState.CurrentRoute.Route
                .SkipWhile(j => string.Equals(j.StarSystem, _gameState.CurrentSystem, StringComparison.OrdinalIgnoreCase))
                .Take(CalculateVisibleSystemCount())
                .ToList();

            for (int i = 0; i < nextJumps.Count; i++)
            {
                var jump = nextJumps[i];
                bool isScoopable = !string.IsNullOrWhiteSpace(jump.StarClass) && "OBAFGKM".Contains(char.ToUpper(jump.StarClass[0]));
                string scoopIcon = isScoopable ? "🟡" : "🔴";
                string label = $"{scoopIcon} {jump.StarSystem}";

                double jumpDistance = 0, fuelUsed = 0;
                bool willRunOutOfFuel = false;
                bool showRefuelHint = false;

                if (canEstimate && jump.StarPos?.Length == 3 && currentPos.HasValue)
                {
                    var targetSystem = (jump.StarPos[0], jump.StarPos[1], jump.StarPos[2]);
                    jumpDistance = VectorUtil.CalculateDistance(currentPos.Value, targetSystem);
                    fuelUsed = FsdJumpRangeCalculator.EstimateFuelUsage(fsd, loadout, jumpDistance, cargo);
                    willRunOutOfFuel = remainingFuel < fuelUsed || remainingFuel - fuelUsed < 1.0;

                    if (!willRunOutOfFuel)
                    {
                        remainingFuel -= fuelUsed;
                        currentPos = targetSystem;
                        label += $"\n  [Distance: {jumpDistance:0.00} LY]";
                        label += $"\n  [⛽ {remainingFuel:0.00}T after jump]";

                        if (i + 1 < nextJumps.Count && nextJumps[i + 1].StarPos?.Length == 3)
                        {
                            var nextTarget = (nextJumps[i + 1].StarPos[0], nextJumps[i + 1].StarPos[1], nextJumps[i + 1].StarPos[2]);
                            double nextDist = VectorUtil.CalculateDistance(currentPos.Value, nextTarget);
                            double nextFuel = FsdJumpRangeCalculator.EstimateFuelUsage(fsd, loadout, nextDist, cargo);
                            showRefuelHint = isScoopable && remainingFuel < nextFuel;
                        }

                        if (!refuelNeeded && remainingFuel < 5.0)
                        {
                            refuelNeeded = true;
                            jumpsUntilRefuel = Items.Count(i => i.ItemType == RouteItemType.System) + 1;
                        }
                    }
                    else
                    {
                        label += $"\n  [Distance: {jumpDistance:0.00} LY]\n  ⚠️ INSUFFICIENT FUEL ({remainingFuel:0.00}T)";

                        if (!refuelNeeded)
                        {
                            refuelNeeded = true;
                            jumpsUntilRefuel = Items.Count(i => i.ItemType == RouteItemType.System) + 1;
                        }

                        Items.Add(new RouteItemViewModel(label, jump.StarClass, jump.SystemAddress, RouteItemType.System)
                        {
                            FontSize = (int)this.FontSize,
                            IsScoopable = isScoopable,
                            JumpRequiresFuel = true,
                            IsReachable = false,
                            ShowRefuelHint = false
                        });

                        currentPos = targetSystem;
                        continue;
                    }
                }

                Items.Add(new RouteItemViewModel(label, jump.StarClass, jump.SystemAddress, RouteItemType.System)
                {
                    FontSize = (int)this.FontSize,
                    IsScoopable = isScoopable,
                    JumpRequiresFuel = willRunOutOfFuel,
                    IsReachable = !willRunOutOfFuel,
                    ShowRefuelHint = showRefuelHint
                });
            }

            if (refuelNeeded && jumpsUntilRefuel > 0)
            {
                int insertIndex = Items.TakeWhile(i => i.ItemType == RouteItemType.Info || i.ItemType == RouteItemType.Destination).Count();
                Items.Insert(insertIndex, new RouteItemViewModel($"⚠️ Need to refuel after {jumpsUntilRefuel} jumps", null, null, RouteItemType.FuelWarning)
                {
                    FontSize = (int)this.FontSize,
                    IsFuelWarning = true
                });
            }
            });
        }

        private int CalculateVisibleSystemCount()
        {
            const double approxSystemHeight = 60;
            const double reservedHeaderSpace = 130;
            double usableHeight = Math.Max(0, AvailableHeight - reservedHeaderSpace);
            return Math.Max(1, (int)(usableHeight / approxSystemHeight));
        }

        private string FormatDestinationName(DestinationInfo destination)
        {
            if (destination == null || string.IsNullOrWhiteSpace(destination.Name)) return null;

            string name = destination.Name;
            if (name == "$EXT_PANEL_ColonisationBeacon_DeploymentSite;") return "Colonisation beacon";
            if (name == "$EXT_PANEL_ColonisationShip:#index=1;") return "Colonisation Ship";

            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\b[A-Z0-9]{3}-[A-Z0-9]{3}\b")) return $"{name} (Carrier)";
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"Beacon|Port|Hub|Station|Ring", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return $"{name} (Station)";

            return name;
        }

        private void CopySystemName(object parameter)
        {
            var routeItem = parameter as RouteItemViewModel;
            if (routeItem == null || string.IsNullOrWhiteSpace(routeItem.Text))
            {
                ShowToast("Nothing to copy.");
                return;
            }

            string text = routeItem.Text;
            int spaceIndex = text.IndexOf(' ');
            int newlineIndex = text.IndexOf('\n');
            string systemName = spaceIndex >= 0 ? text.Substring(spaceIndex + 1, newlineIndex > spaceIndex ? newlineIndex - spaceIndex - 1 : text.Length - spaceIndex - 1).Trim() : text;

            try
            {
                ClipboardService.SetText(systemName);
                ShowToast($"Copied: {systemName}");
            }
            catch (Exception ex)
            {
                ShowToast("Failed to copy to clipboard.");
                Log.Warning(ex, "Clipboard error");
            }
        }

        private void ShowToast(string message)
        {
            System.Diagnostics.Debug.WriteLine($"Toast: {message}");
        }
    }

    public enum RouteItemType { Info, Destination, System, FuelWarning }

    public class RouteItemViewModel : ViewModelBase
    {
        public RouteItemViewModel(string text, string starClass, long? systemAddress, RouteItemType itemType)
        {
            Text = text;
            StarClass = starClass;
            SystemAddress = systemAddress;
            ItemType = itemType;
        }

        public string Text { get; set; }
        public string SystemText => ItemType != RouteItemType.System ? Text : Text?.IndexOf(' ') is int i and >= 0 ? Text.Substring(i + 1) : Text;
        public string StarClass { get; set; }
        public long? SystemAddress { get; set; }
        public RouteItemType ItemType { get; set; }
        public bool IsScoopable { get; set; }
        public bool JumpRequiresFuel { get; set; }
        public bool IsReachable { get; set; } = true;
        public bool IsFuelWarning { get; set; }
        public bool IsNextJump { get; set; }
        public int FontSize { get; set; } = 14;
        public bool ShowRefuelHint { get; set; }

        public string Icon => ItemType != RouteItemType.System ? string.Empty : IsScoopable ? "🟡" : "🔴";
        public Brush IconColor => JumpRequiresFuel ? Brushes.Red : IsScoopable ? Brushes.Gold : Brushes.White;
        public string RefuelHint => ShowRefuelHint ? "🔄 Refuel here!" : null;
        public Brush RefuelColor => Brushes.Gold;
        public Brush TextColor => !IsReachable ? Brushes.Gray : IsFuelWarning || JumpRequiresFuel ? Brushes.Red : IsNextJump ? Brushes.LightGreen : IsScoopable ? Brushes.Gold : Brushes.White;
    }
}
