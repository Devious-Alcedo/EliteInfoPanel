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
    public enum RouteItemType { Info, Destination, System, FuelWarning }

    public class RouteItemViewModel : ViewModelBase
    {
        #region Public Constructors

        public RouteItemViewModel(string text, string starClass, long? systemAddress, RouteItemType itemType)
        {
            Text = text;
            StarClass = starClass;
            SystemAddress = systemAddress;
            ItemType = itemType;
        }

        #endregion Public Constructors

        #region Public Properties

        public int FontSize { get; set; } = 14;

        public string Icon => ItemType != RouteItemType.System ? string.Empty : IsScoopable ? "🟡" : "🔴";

        public Brush IconColor
        {
            get
            {
                if (!IsReachable)
                    return Brushes.Gray;
                if (JumpRequiresFuel)
                    return Brushes.White;
                return IsScoopable ? Brushes.Gold : Brushes.Red;
            }
        }

        public string IconUrl
        {
            get
            {
                if (ItemType != RouteItemType.System || string.IsNullOrWhiteSpace(StarClass))
                    return null;

                string localPath = StarIconMapper.GetIconPath(StarClass);
                if (string.IsNullOrWhiteSpace(localPath))
                    return null;

                return $"pack://application:,,,/{localPath}";
            }
        }


        public bool IsFuelWarning { get; set; }
        public bool IsNextJump { get; set; }
        public bool IsReachable { get; set; } = true;
        public bool IsScoopable { get; set; }
        public RouteItemType ItemType { get; set; }
        public bool JumpRequiresFuel { get; set; }
        public Brush RefuelColor => Brushes.Gold;
        public string RefuelHint => ShowRefuelHint ? "🔄 Refuel here!" : null;
        public bool ShowRefuelHint { get; set; }
        public string StarClass { get; set; }
        public long? SystemAddress { get; set; }
        public string SystemText => ItemType != RouteItemType.System ? Text : Text?.IndexOf(' ') is int i and >= 0 ? Text.Substring(i + 1) : Text;
        public string Text { get; set; }
        public Brush TextColor => !IsReachable ? Brushes.Gray : IsFuelWarning || JumpRequiresFuel ? Brushes.Red : IsNextJump ? Brushes.LightGreen : IsScoopable ? Brushes.Gold : Brushes.White;

        #endregion Public Properties
    }

    public class RouteViewModel : CardViewModel
    {
        #region Private Fields

        private readonly GameStateService _gameState;
        private double _availableHeight;
        private int _fontSize = 14;
        private int _jumpsUntilRefuel;
        private bool _needsRefueling;

        #endregion Private Fields

        #region Public Constructors

        public RouteViewModel(GameStateService gameState) : base("Nav Route")
        {
            _gameState = gameState;
            CopySystemNameCommand = new RelayCommand(CopySystemName);
            _gameState.DataUpdated += UpdateRoute;
            UpdateRoute();
        }

        #endregion Public Constructors

        #region Public Properties

        public double AvailableHeight
        {
            get => _availableHeight;
            set
            {
                if (SetProperty(ref _availableHeight, value))
                    UpdateRoute();
            }
        }

        public ICommand CopySystemNameCommand { get; }

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

        public int JumpsUntilRefuel
        {
            get => _jumpsUntilRefuel;
            set => SetProperty(ref _jumpsUntilRefuel, value);
        }
        public bool NeedsRefueling
        {
            get => _needsRefueling;
            set => SetProperty(ref _needsRefueling, value);
        }

        #endregion Public Properties

        #region Private Methods

        private int CalculateVisibleSystemCount()
        {
            const double approxSystemHeight = 60;
            const double reservedHeaderSpace = 130;
            double usableHeight = Math.Max(0, AvailableHeight - reservedHeaderSpace);
            return Math.Max(1, (int)(usableHeight / approxSystemHeight));
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

        private void ShowToast(string message)
        {
            System.Diagnostics.Debug.WriteLine($"Toast: {message}");
        }

        private void UpdateRoute()
        {
       
            Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Clear();
                Log.Debug("UpdateRoute: Clearing route items and preparing new update.");

                var route = _gameState.CurrentRoute?.Route ?? new List<NavRouteJson.NavRouteSystem>();

                bool hasRoute = route.Any();
                bool hasDestination = !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);

                // decide if card should be visible
                IsVisible = hasRoute || hasDestination;
                if (!IsVisible)
                {
                    Log.Debug("UpdateRoute: No route and no destination — card hidden.");
                    return;
                }


                // fallback so jump loop can still run
                route ??= new List<NavRouteJson.NavRouteSystem>();


                var firstJump = route.FirstOrDefault(j => j.StarPos?.Length == 3);
                if (firstJump != null)
                {
                    _gameState.CurrentSystemCoordinates = (firstJump.StarPos[0], firstJump.StarPos[1], firstJump.StarPos[2]);
                    Log.Debug("UpdateRoute: Set system coordinates to {X}, {Y}, {Z}", firstJump.StarPos[0], firstJump.StarPos[1], firstJump.StarPos[2]);
                }
                else
                {
                    _gameState.CurrentSystemCoordinates = null;
                    Log.Debug("UpdateRoute: No valid jump with position data found. System coordinates set to null.");
                }


                if (_gameState.RouteWasActive && _gameState.RouteCompleted && !_gameState.IsInHyperspace)
            {
                ShowToast("Route complete! You've arrived at your destination.");
                _gameState.ResetRouteActivity();
            }

        //    bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true;
       //     bool hasDestination = !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
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

                if (_gameState.CurrentStatus?.Destination != null &&
                         !string.IsNullOrWhiteSpace(_gameState.CurrentStatus.Destination.Name))
                {
                    var dest = _gameState.CurrentStatus.Destination;
                    string currentSystem = _gameState.CurrentSystem ?? "(unknown)";
                    string lastRouteSystem = _gameState.CurrentRoute?.Route?.LastOrDefault()?.StarSystem;

                    string label = $"Target: {FormatDestinationName(dest)}";

                    if (string.Equals(currentSystem, lastRouteSystem, StringComparison.OrdinalIgnoreCase))
                    {
                        label += " (Route Destination)";
                    }

                    Log.Debug("RouteCard: Adding target label: {Label}", label);

                    Items.Add(new RouteItemViewModel(label, null, null, RouteItemType.Destination)
                    {
                        FontSize = (int)this.FontSize
                    });
                }
                else
                {
                    Log.Debug("RouteCard: No current destination to display.");
                }




                var fuelStatus = _gameState.CurrentStatus?.Fuel;
            var fsd = _gameState.CurrentLoadout?.Modules?.FirstOrDefault(m => m.Slot == "FrameShiftDrive");
            var loadout = _gameState.CurrentLoadout;
            var cargo = _gameState.CurrentCargo;

            double currentFuel = fuelStatus?.FuelMain ?? 0;
            double maxFuelCapacity = loadout?.FuelCapacity?.Main ?? 0;

            bool canEstimate = fsd != null && loadout != null && cargo != null && currentFuel > 0;
                if (!canEstimate)
                {
                    Log.Debug("Skipping route estimation — missing FSD, loadout or cargo info");
                }else
                {
                    Items.Add(new RouteItemViewModel($"Current Fuel: {currentFuel:0.00}/{maxFuelCapacity:0.00} T", null, null, RouteItemType.Info)
                    {
                        FontSize = (int)this.FontSize
                    });
                }
                Log.Debug("UpdateRoute: CurrentFuel={Fuel}, MaxFuel={MaxFuel}, CanEstimate={CanEstimate}", currentFuel, maxFuelCapacity, canEstimate);

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
                    Log.Debug("UpdateRoute: Processing jump to {System}", jump.StarSystem);

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
                            label += $"\n  [Distance: {jumpDistance:0.00} LY] ({jump.StarClass}{(isScoopable ? " - Fuel" : "")})";
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
                            label += $"\n  [Distance: {jumpDistance:0.00} LY] ({jump.StarClass}{(isScoopable ? " - Fuel" : "")})";

                            label += $"\n  ⚠️ INSUFFICIENT FUEL ({remainingFuel:0.00}T)";


                            if (!refuelNeeded)
                        {
                            refuelNeeded = true;
                            jumpsUntilRefuel = Items.Count(i => i.ItemType == RouteItemType.System) + 1;
                                Log.Debug("UpdateRoute: Inserted fuel warning after {JumpCount} jumps.", jumpsUntilRefuel);

                            }

                            var routeItem = new RouteItemViewModel(label, jump.StarClass, jump.SystemAddress, RouteItemType.System)
                            {
                                FontSize = (int)this.FontSize,
                                IsScoopable = isScoopable,
                                JumpRequiresFuel = willRunOutOfFuel,
                                IsReachable = !willRunOutOfFuel,
                                ShowRefuelHint = showRefuelHint
                            };

                            Log.Information("Adding RouteItem: {System} | StarClass: {Class} | Scoopable: {Scoopable} | IconColor: {Color}",
                                jump.StarSystem, jump.StarClass, isScoopable, routeItem.IconColor.ToString());

                            Items.Add(routeItem);

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

        #endregion Private Methods
    }
}
