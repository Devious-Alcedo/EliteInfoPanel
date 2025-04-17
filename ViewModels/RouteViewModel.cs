// RouteViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EliteInfoPanel.Core;
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
                var fuelStatus = _gameState.CurrentStatus?.Fuel;
                var fsd = _gameState.CurrentLoadout?.Modules?.FirstOrDefault(m => m.Slot == "FrameShiftDrive");
                var loadout = _gameState.CurrentLoadout;
                var cargo = _gameState.CurrentCargo;

                double remainingFuel = fuelStatus?.FuelMain ?? 0;

                // Show route systems
                if (_gameState.CurrentRoute?.Route?.Any() == true)
                {
                    // Seed starting coordinates from the first jump's StarPos
                    var firstJumpWithStarPos = _gameState.CurrentRoute.Route.FirstOrDefault(j => j.StarPos is { Length: 3 });
                    if (firstJumpWithStarPos?.StarPos is { Length: 3 } starPos)
                    {
                        _gameState.CurrentSystemCoordinates = (starPos[0], starPos[1], starPos[2]);
                    }
                    else
                    {
                        _gameState.CurrentSystemCoordinates = null;
                    }


                    var nextJumps = _gameState.CurrentRoute.Route
                        .SkipWhile(j => string.Equals(j.StarSystem, _gameState.CurrentSystem, StringComparison.OrdinalIgnoreCase))
                        .Take(4);

                    foreach (var jump in nextJumps)

                    {
                        // Determine scoopable status
                        string scoopIcon = "🔴"; // default to non-scoopable
                        if (!string.IsNullOrWhiteSpace(jump.StarClass))
                        {
                            char primaryClass = char.ToUpper(jump.StarClass[0]);
                            if ("OBAFGKM".Contains(primaryClass))
                            {
                                scoopIcon = "🟡"; // scoopable
                            }
                        }

                        string label = $"{scoopIcon} {jump.StarSystem}";


                        double jumpDistance = 0;
                        double fuelUsed = 0;

                        if (fsd != null && loadout != null && cargo != null && remainingFuel > 0)
                        {
                            if (jump.StarPos != null && jump.StarPos.Length == 3 && _gameState.CurrentSystemCoordinates != null)
                            {
                                var currentSystem = (jump.StarPos[0], jump.StarPos[1], jump.StarPos[2]);
                                jumpDistance = VectorUtil.CalculateDistance(_gameState.CurrentSystemCoordinates.Value, currentSystem);
                                _gameState.CurrentSystemCoordinates = currentSystem;

                                fuelUsed = FsdJumpRangeCalculator.EstimateFuelUsage(fsd, loadout, jumpDistance, cargo);
                                remainingFuel -= fuelUsed;
                                remainingFuel = Math.Max(remainingFuel, 0);

                                label += $"\n  [Distance: {jumpDistance:0.00} LY]\n  [Fuel: ⛽ {remainingFuel:0.00} t]";

                            }
                        }

                        Items.Add(new RouteItemViewModel(
                            label,
                            jump.StarClass,
                            jump.SystemAddress,
                            RouteItemType.System)
                        {
                            FontSize = (int)this.FontSize
                        });
                    }

                }
            });
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
        System
    }

    public class RouteItemViewModel : ViewModelBase
    {
        private string _text;
        private string _starClass;
        private long? _systemAddress;
        private RouteItemType _itemType;
        private int _fontSize = 14;
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

        public RouteItemViewModel(string text, string starClass, long? systemAddress, RouteItemType itemType)
        {
            _text = text;
            _starClass = starClass;
            _systemAddress = systemAddress;
            _itemType = itemType;
        }
    }
}