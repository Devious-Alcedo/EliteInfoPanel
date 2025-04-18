using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.EliteInfoPanel.Core;  // Add this namespace for LoadoutJson
using EliteInfoPanel.Util;
using Serilog;
using TextCopy;

namespace EliteInfoPanel.ViewModels
{
    public class RouteViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        public double EstimatedFuelRemaining { get; set; } // in tonnes
        private int _fontSize = 14;

        // Fuel-related properties
        private int _jumpsUntilRefuel;
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

        private double _minimumFuelLevel = 1.0; // Default minimum fuel threshold in tonnes

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

        public ObservableCollection<RouteItemViewModel> Items { get; } = new();
        public ICommand CopySystemNameCommand { get; }

        public RouteViewModel(GameStateService gameState) : base("Nav Route")
        {
            _gameState = gameState;

            // Initialize commands
            CopySystemNameCommand = new RelayCommand(CopySystemName);

            // Subscribe to game state updates
            _gameState.DataUpdated += UpdateRoute;

            // Initial update
            UpdateRoute();
        }

        private void UpdateRoute()
        {
            // Seed starting point from first jump that has valid StarPos
            var jumps = _gameState.CurrentRoute?.Route?.Where(j => j.StarPos?.Length == 3).ToList();
            if (jumps != null && jumps.Count > 0)
            {
                _gameState.CurrentSystemCoordinates = (
                    jumps[0].StarPos[0],
                    jumps[0].StarPos[1],
                    jumps[0].StarPos[2]
                );
            }
            else
            {
                _gameState.CurrentSystemCoordinates = null;
            }

            RunOnUIThread(() =>
            {
                Items.Clear();

                // Check for route completion
                if (_gameState.RouteWasActive && _gameState.RouteCompleted && !_gameState.IsInHyperspace)
                {
                    ShowToast("Route complete! You've arrived at your destination.");
                    _gameState.ResetRouteActivity();
                }

                // Determine visibility
                bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true;
                bool hasDestination = !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
                IsVisible = hasRoute || hasDestination;

                if (!IsVisible)
                    return;

                // Check for remaining jumps
                bool isTargetInSameSystem = string.Equals(_gameState.CurrentSystem, _gameState.LastFsdTargetSystem,
                                            StringComparison.OrdinalIgnoreCase);

                if (_gameState.RemainingJumps.HasValue && !isTargetInSameSystem)
                {
                    Items.Add(new RouteItemViewModel(
                        $"Jumps Remaining: {_gameState.RemainingJumps.Value}",
                        null, null, RouteItemType.Info)
                    {
                        FontSize = (int)this.FontSize
                    });
                }

                // Show destination
                if (!string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name))
                {
                    string destination = _gameState.CurrentStatus.Destination?.Name;
                    string lastRouteSystem = _gameState.CurrentRoute?.Route?.LastOrDefault()?.StarSystem;

                    if (!string.Equals(destination, lastRouteSystem, StringComparison.OrdinalIgnoreCase))
                    {
                        Items.Add(new RouteItemViewModel(
                            $"Target: {FormatDestinationName(_gameState.CurrentStatus.Destination)}",
                            null, null, RouteItemType.Destination)
                        {
                            FontSize = (int)this.FontSize
                        });
                    }
                }

                // Assess fuel situation
                var fuelStatus = _gameState.CurrentStatus?.Fuel;
                var fsd = _gameState.CurrentLoadout?.Modules?.FirstOrDefault(m => m.Slot == "FrameShiftDrive");
                var loadout = _gameState.CurrentLoadout;
                var cargo = _gameState.CurrentCargo;

                double currentFuel = fuelStatus?.FuelMain ?? 0;
                double maxFuelCapacity = loadout?.FuelCapacity?.Main ?? 0;

                // If we have all the required data, add current fuel status
                bool canEstimate = fsd != null && loadout != null && cargo != null && currentFuel > 0;

                if (canEstimate)
                {
                    // Add current fuel status
                    Items.Add(new RouteItemViewModel(
                        $"Current Fuel: {currentFuel:0.00}/{maxFuelCapacity:0.00} T",
                        null, null, RouteItemType.Info)
                    {
                        FontSize = (int)this.FontSize
                    });
                }

                // Show route systems
                if (_gameState.CurrentRoute?.Route?.Any() == true)
                {
                    // Reset for routes
                    double remainingFuel = currentFuel;
                    (double X, double Y, double Z)? currentPos = _gameState.CurrentSystemCoordinates;
                    int jumpsUntilRefuel = 0;
                    bool refuelNeeded = false;

                    // Get systems from route
                    var nextJumps = _gameState.CurrentRoute.Route
                        .SkipWhile(j => string.Equals(j.StarSystem, _gameState.CurrentSystem, StringComparison.OrdinalIgnoreCase))
                        .Take(8); // Show more jumps to better visualize fuel situation

                    foreach (var jump in nextJumps)
                    {
                        // Determine scoopable status
                        bool isScoopable = false;
                        string scoopIcon = "🔴"; // default to non-scoopable

                        if (!string.IsNullOrWhiteSpace(jump.StarClass))
                        {
                            char primaryClass = char.ToUpper(jump.StarClass[0]);
                            if ("OBAFGKM".Contains(primaryClass))
                            {
                                scoopIcon = "🟡"; // scoopable
                                isScoopable = true;
                            }
                        }

                        // Basic system name with scoopable indicator
                        string label = $"{scoopIcon} {jump.StarSystem}";

                        double jumpDistance = 0;
                        double fuelUsed = 0;
                        bool willRunOutOfFuel = false;

                        // Only calculate if we have valid positions and FSD data
                        if (canEstimate && jump.StarPos != null && jump.StarPos.Length == 3 && currentPos.HasValue)
                        {
                            var targetSystem = (jump.StarPos[0], jump.StarPos[1], jump.StarPos[2]);
                            jumpDistance = VectorUtil.CalculateDistance(currentPos.Value, targetSystem);

                            // Calculate fuel usage
                            fuelUsed = FsdJumpRangeCalculator.EstimateFuelUsage(fsd, loadout, jumpDistance, cargo);

                            // Check if we'll run out of fuel
                            willRunOutOfFuel = remainingFuel < fuelUsed || remainingFuel - fuelUsed < 1.0;

                            // Update for next jump
                            if (!willRunOutOfFuel)
                            {
                                remainingFuel -= fuelUsed;
                                currentPos = targetSystem;

                                // Format the label with fuel info
                                label += $"\n  [Distance: {jumpDistance:0.00} LY]";

                                string fuelColor = remainingFuel < 5.0 ? "🟠" : "⛽";
                                label += $"\n  [{fuelColor} {remainingFuel:0.00}T after jump]";

                                // Add refuel indicator for scoopable stars when fuel is low
                                if (isScoopable && remainingFuel < 5.0)
                                {
                                    label += " 🔄 Refuel here!";
                                }

                                // Check if we need to set the critical fuel warning
                                if (!refuelNeeded && remainingFuel < 5.0)
                                {
                                    refuelNeeded = true;
                                    jumpsUntilRefuel = Items.Count(i => i.ItemType == RouteItemType.System) + 1;
                                    // Don't break, continue to display the route
                                }
                            }
                            else
                            {
                                // We've run out of fuel
                                label += $"\n  [Distance: {jumpDistance:0.00} LY]";
                                label += $"\n  ⚠️ INSUFFICIENT FUEL ({remainingFuel:0.00}T)";

                                if (!refuelNeeded)
                                {
                                    refuelNeeded = true;
                                    jumpsUntilRefuel = Items.Count(i => i.ItemType == RouteItemType.System) + 1;
                                }

                                // Exit the loop as we can't go further
                                break;
                            }
                        }

                        // Add the jump to the list
                        Items.Add(new RouteItemViewModel(
                            label,
                            jump.StarClass,
                            jump.SystemAddress,
                            RouteItemType.System)
                        {
                            FontSize = (int)this.FontSize,
                            IsScoopable = isScoopable,
                            JumpRequiresFuel = willRunOutOfFuel
                        });
                    }

                    // After the loop finishes, add the warning if needed
                    if (refuelNeeded && jumpsUntilRefuel > 0)
                    {
                        // Insert at the beginning of Items, after any info or destination items
                        int insertIndex = 0;
                        foreach (var item in Items)
                        {
                            if (item.ItemType == RouteItemType.Info || item.ItemType == RouteItemType.Destination)
                                insertIndex++;
                            else
                                break;
                        }

                        Items.Insert(insertIndex, new RouteItemViewModel(
                            $"⚠️ Need to refuel after {jumpsUntilRefuel} jumps",
                            null, null, RouteItemType.FuelWarning)
                        {
                            FontSize = (int)this.FontSize,
                            IsFuelWarning = true
                        });

                        Log.Debug("Added fuel warning: Need to refuel after {JumpCount} jumps", jumpsUntilRefuel);
                    }
                }
            });
        }
        private int CalculateJumpsUntilRefuel(double currentFuel, (double X, double Y, double Z)? startPos,
                                               List<NavRouteJson.NavRouteSystem> route, LoadoutModule fsd,
                                               LoadoutJson loadout, CargoJson cargo, double minimumFuel)
        {
            if (startPos == null || route == null || !route.Any() || currentFuel <= minimumFuel)
                return 0;

            var currentPos = startPos.Value;
            double remainingFuel = currentFuel;
            int jumpCount = 0;

            // Skip systems we've already visited
            var remainingJumps = route
                .SkipWhile(j => string.Equals(j.StarSystem, _gameState.CurrentSystem, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var jump in remainingJumps)
            {
                // Skip jumps without valid position data
                if (jump.StarPos == null || jump.StarPos.Length != 3)
                    continue;

                var targetSystem = (jump.StarPos[0], jump.StarPos[1], jump.StarPos[2]);
                double jumpDistance = VectorUtil.CalculateDistance(currentPos, targetSystem);

                // Calculate fuel usage
                double fuelUsed = FsdJumpRangeCalculator.EstimateFuelUsage(fsd, loadout, jumpDistance, cargo);

                // Check if there's enough fuel for this jump (include the safety margin)
                if (remainingFuel < fuelUsed + minimumFuel)
                    return jumpCount;

                // Update values for next jump
                remainingFuel -= fuelUsed;
                currentPos = targetSystem;
                jumpCount++;

                // Debug jumps
                Log.Debug("Jump {0}: {1} → Fuel used: {2:F2}T, Remaining: {3:F2}T",
                    jumpCount, jump.StarSystem, fuelUsed, remainingFuel);

                // Check if a scoopable star (can refuel)
                bool isScoopable = false;
                if (!string.IsNullOrWhiteSpace(jump.StarClass))
                {
                    char primaryClass = char.ToUpper(jump.StarClass[0]);
                    if ("OBAFGKM".Contains(primaryClass))
                    {
                        isScoopable = true;
                        // We assume refueling at scoopable stars - flight path would include fuel scooping
                        double maxFuel = loadout?.FuelCapacity?.Main ?? 32; // Default to 32T if unknown
                        remainingFuel = maxFuel;
                        Log.Debug("  ↳ Scoopable star - Refueled to {0:F2}T", remainingFuel);
                    }
                }

                // Check if fuel too low for next jump
                if (remainingFuel <= 2.0 && !isScoopable)
                {
                    Log.Debug("  ↳ Fuel critically low and next star not scoopable");
                }
            }

            // If we made it through all jumps, return the total count
            return jumpCount;
        }

        // Additional method to count jumps until low fuel (for warning display)
        private int CountJumpsUntilLowFuel(double currentFuel, (double X, double Y, double Z)? startPos,
                                      List<NavRouteJson.NavRouteSystem> route, LoadoutModule fsd,
                                      LoadoutJson loadout, CargoJson cargo, double warningThreshold)
        {
            if (startPos == null || route == null || !route.Any() || currentFuel <= warningThreshold)
                return 0;

            var currentPos = startPos.Value;
            double remainingFuel = currentFuel;
            int jumpCount = 0;

            // Process each jump in route
            foreach (var jump in route)
            {
                // Skip jumps without valid position data
                if (jump.StarPos == null || jump.StarPos.Length != 3)
                    continue;

                var targetSystem = (jump.StarPos[0], jump.StarPos[1], jump.StarPos[2]);
                double jumpDistance = VectorUtil.CalculateDistance(currentPos, targetSystem);

                // Calculate fuel usage
                double fuelUsed = FsdJumpRangeCalculator.EstimateFuelUsage(fsd, loadout, jumpDistance, cargo);

                // Don't count jumps we can't make
                if (remainingFuel < fuelUsed)
                    break;

                // Update values for next jump
                remainingFuel -= fuelUsed;
                currentPos = targetSystem;
                jumpCount++;

                // Check if we've reached warning threshold
                if (remainingFuel <= warningThreshold)
                {
                    Log.Debug("Visual fuel warning at jump {0}: {1} → Remaining fuel: {2:F2}T",
                        jumpCount, jump.StarSystem, remainingFuel);
                    return jumpCount;
                }

                // Check if a scoopable star (can refuel)
                if (!string.IsNullOrWhiteSpace(jump.StarClass))
                {
                    char primaryClass = char.ToUpper(jump.StarClass[0]);
                    if ("OBAFGKM".Contains(primaryClass))
                    {
                        // We assume refueling at scoopable stars - flight path would include fuel scooping
                        double maxFuel = loadout?.FuelCapacity?.Main ?? 32; // Default to 32T if unknown
                        remainingFuel = maxFuel;
                    }
                }
            }

            // If we didn't hit the warning threshold in our route, return the total count
            return jumpCount;
        }

        private string FormatDestinationName(DestinationInfo destination)
        {
            if (destination == null || string.IsNullOrWhiteSpace(destination.Name))
                return null;

            var name = destination.Name;
            if (name == "$EXT_PANEL_ColonisationBeacon_DeploymentSite;")
            {
                return "Colonisation beacon";
            }
            if (name == "$EXT_PANEL_ColonisationShip:#index=1;")
            {
                return "Colonisation Ship";
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\b[A-Z0-9]{3}-[A-Z0-9]{3}\b")) // matches FC ID
                return $"{name} (Carrier)";
            else if (System.Text.RegularExpressions.Regex.IsMatch(name, @"Beacon|Port|Hub|Station|Ring",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return $"{name} (Station)";
            else
                return name;
        }

        private void CopySystemName(object parameter)
        {
            var routeItem = parameter as RouteItemViewModel;
            if (routeItem == null)
            {
                ShowToast("Nothing to copy.");
                return;
            }

            // Extract just the system name from the Text property
            string systemName = null;

            if (routeItem.ItemType == RouteItemType.System)
            {
                // The Text now contains: "🔴 SystemName\n  [Distance: 55.28 LY]\n  [Fuel: ⛽ 24.96 t]"
                // We need to extract just the system name
                string text = routeItem.Text;

                // Find the system name by removing the icon prefix and everything after the first newline
                int spaceIndex = text.IndexOf(' ');
                if (spaceIndex >= 0)
                {
                    int newlineIndex = text.IndexOf('\n');
                    if (newlineIndex > spaceIndex)
                    {
                        // Extract just the system name (after the space but before the newline)
                        systemName = text.Substring(spaceIndex + 1, newlineIndex - spaceIndex - 1).Trim();
                    }
                    else
                    {
                        // If no newline, just take everything after the first space
                        systemName = text.Substring(spaceIndex + 1).Trim();
                    }
                }
                else
                {
                    // Fallback to the entire text if we can't parse it
                    systemName = text;
                }
            }
            else if (routeItem.ItemType == RouteItemType.Destination)
            {
                // For destination items, extract just the system name if possible
                string text = routeItem.Text;
                if (text.StartsWith("Target: "))
                {
                    systemName = text.Substring("Target: ".Length);
                }
                else
                {
                    systemName = text;
                }
            }
            else
            {
                // For other item types (like "Jumps Remaining"), use the full text
                systemName = routeItem.Text;
            }

            if (string.IsNullOrWhiteSpace(systemName))
            {
                ShowToast("Nothing to copy.");
                return;
            }

            try
            {
                ClipboardService.SetText(systemName);
                ShowToast($"Copied: {systemName}");
            }
            catch (Exception ex)
            {
                ShowToast("Failed to copy to clipboard.");
                Serilog.Log.Warning(ex, "Clipboard error");
            }
        }

        private void ShowToast(string message)
        {
            // This will be connected to the main view model's toast queue
            System.Diagnostics.Debug.WriteLine($"Toast: {message}");
        }
    }


    public enum RouteItemType
    {
        Info,
        Destination,
        System,
        FuelWarning
    }

    public class RouteItemViewModel : ViewModelBase
    {
        private string _text;
        private string _starClass;
        private long? _systemAddress;
        private RouteItemType _itemType;
        private int _fontSize = 14;
        private bool _isScoopable;
        private bool _jumpRequiresFuel;
        private bool _isFuelWarning;
        private bool _isNextJump;

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public string StarClass
        {
            get => _starClass;
            set => SetProperty(ref _starClass, value);
        }

        public long? SystemAddress
        {
            get => _systemAddress;
            set => SetProperty(ref _systemAddress, value);
        }

        public RouteItemType ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        public bool IsScoopable
        {
            get => _isScoopable;
            set => SetProperty(ref _isScoopable, value);
        }

        public bool JumpRequiresFuel
        {
            get => _jumpRequiresFuel;
            set => SetProperty(ref _jumpRequiresFuel, value);
        }

        public bool IsFuelWarning
        {
            get => _isFuelWarning;
            set => SetProperty(ref _isFuelWarning, value);
        }

        public bool IsNextJump
        {
            get => _isNextJump;
            set => SetProperty(ref _isNextJump, value);
        }

        public Brush TextColor
        {
            get
            {
                if (IsFuelWarning)
                    return Brushes.Red;

                if (JumpRequiresFuel)
                    return Brushes.Red;

                if (IsNextJump)
                    return Brushes.LightGreen;

                if (IsScoopable)
                    return Brushes.Gold;

                return Brushes.White;
            }
        }

        public RouteItemViewModel(string text, string starClass, long? systemAddress, RouteItemType itemType)
        {
            _text = text;
            _starClass = starClass;
            _systemAddress = systemAddress;
            _itemType = itemType;
        }
    }
}