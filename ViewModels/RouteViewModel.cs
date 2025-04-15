// RouteViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EliteInfoPanel.Core;
using TextCopy;

namespace EliteInfoPanel.ViewModels
{
    public class RouteViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;

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
                        null, null, RouteItemType.Info));
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
                            null, null, RouteItemType.Destination));
                    }
                }

                // Show route systems
                if (_gameState.CurrentRoute?.Route?.Any() == true)
                {
                    foreach (var jump in _gameState.CurrentRoute.Route)
                    {
                        Items.Add(new RouteItemViewModel(
                            jump.StarSystem,
                            jump.StarClass,
                            jump.SystemAddress,
                            RouteItemType.System));
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
            string text = parameter as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowToast("Nothing to copy.");
                return;
            }

            try
            {
                ClipboardService.SetText(text);
                ShowToast($"Copied: {text}");
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