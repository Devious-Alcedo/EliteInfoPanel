using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly GameStateService _gameState;
        private bool _isLoading = true;
        private bool _isHyperspaceJumping;
        private string _hyperspaceDestination;
        private string _hyperspaceStarClass;
        private SnackbarMessageQueue _toastQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        private bool _isCarrierJumping;
        private CardLayoutManager _layoutManager;

        public bool IsCarrierJumping
        {
            get => _isCarrierJumping;
            set => SetProperty(ref _isCarrierJumping, value);
        }

        public ObservableCollection<CardViewModel> Cards { get; } = new ObservableCollection<CardViewModel>();

        // Individual card ViewModels
        public SummaryViewModel SummaryCard { get; }
        public CargoViewModel CargoCard { get; }
        public BackpackViewModel BackpackCard { get; }
        public RouteViewModel RouteCard { get; }
        public ModulesViewModel ModulesCard { get; }
        public FlagsViewModel FlagsCard { get; }
        private Grid _mainGrid;

        public void SetMainGrid(Grid mainGrid)
        {
            _mainGrid = mainGrid;

            // Initialize layout manager
            var appSettings = SettingsManager.Load();
            _layoutManager = new CardLayoutManager(_mainGrid, appSettings, this);

            // Do initial layout
            UpdateCardLayout();
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsHyperspaceJumping
        {
            get => _isHyperspaceJumping;
            set
            {
                if (_isHyperspaceJumping != value)
                {
                    _isHyperspaceJumping = value;
                    OnPropertyChanged(nameof(IsHyperspaceJumping));
                }
            }
        }

        public string HyperspaceDestination
        {
            get => _hyperspaceDestination;
            set => SetProperty(ref _hyperspaceDestination, value);
        }

        public string HyperspaceStarClass
        {
            get => _hyperspaceStarClass;
            set => SetProperty(ref _hyperspaceStarClass, value);
        }

        public SnackbarMessageQueue ToastQueue
        {
            get => _toastQueue;
            set => SetProperty(ref _toastQueue, value);
        }

        public RelayCommand OpenOptionsCommand { get; set; }
        public RelayCommand CloseCommand { get; } = new RelayCommand(_ => Application.Current.Shutdown());

        public MainViewModel(GameStateService gameState)
        {
            _gameState = gameState;

            // Initialize commands
            OpenOptionsCommand = new RelayCommand(_ => OpenOptions());

            // Initialize card ViewModels
            SummaryCard = new SummaryViewModel(gameState) { Title = "Summary" };
            CargoCard = new CargoViewModel(gameState) { Title = "Cargo" };
            BackpackCard = new BackpackViewModel(gameState) { Title = "Backpack" };
            RouteCard = new RouteViewModel(gameState) { Title = "Nav Route" };
            ModulesCard = new ModulesViewModel(gameState) { Title = "Ship Modules" };
            FlagsCard = new FlagsViewModel(gameState) { Title = "Status Flags" };

            // Add cards to collection
            Cards.Add(SummaryCard);
            Cards.Add(CargoCard);
            Cards.Add(BackpackCard);
            Cards.Add(RouteCard);
            Cards.Add(ModulesCard);
            Cards.Add(FlagsCard);

            UpdateLoadingState();

            // Subscribe to game state events
            _gameState.DataUpdated += OnGameStateUpdated;
            _gameState.HyperspaceJumping += OnHyperspaceJumping;

            // Initial update
            RefreshCardVisibility();
        }

        // Add to MainViewModel
        private bool IsEliteRunning()
        {
            var status = _gameState?.CurrentStatus;
            return status != null && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));
        }

        public void UpdateLoadingState()
        {
            IsLoading = !IsEliteRunning();
        }

        private void OnGameStateUpdated()
        {
            Log.Debug("GameState update received.");

            var status = _gameState?.CurrentStatus;
            if (!string.IsNullOrEmpty(_gameState.CarrierJumpDestinationSystem) &&
                _gameState.FleetCarrierJumpInProgress == false)
            {
                IsCarrierJumping = false;
            }
            if (status == null || status.Flags == Flag.None)
            {
                Log.Debug("Game data not ready. Still waiting...");
                return; // don't flip loading off yet
            }

            UpdateLoadingState(); // will flip IsLoading based on flags

            if (!IsLoading)
                //  Log.Information("Game state confirmed. Hiding loading overlay.");

                RefreshCardVisibility();
        }


        private void OnHyperspaceJumping(bool jumping, string systemName)
        {
            IsHyperspaceJumping = jumping;
            HyperspaceDestination = systemName;
            HyperspaceStarClass = _gameState.HyperspaceStarClass;
            RefreshCardVisibility();
        }

        protected void RunOnUiThread(Action action)
        {
            if (System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
            {
                // We're already on the UI thread
                action();
            }
            else
            {
                // We need to invoke the action on the UI thread
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(action);
            }
        }

        private void RefreshCardVisibility()
        {
            // Make sure we're on the UI thread
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(RefreshCardVisibility);
                return;
            }

            var status = _gameState.CurrentStatus;
            if (status == null) return;

            // Calculate all visibility states
            bool shouldShowPanels = !IsHyperspaceJumping && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            // Set visibility for all cards
            foreach (var card in Cards)
            {
                card.IsVisible = false; // Start by hiding all
            }

            if (!shouldShowPanels) return;

            // Show cards based on game state
            SummaryCard.IsVisible = true;

            bool showBackpack = status.OnFoot;
            bool hasCargo = (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;

            // Only show one of backpack or cargo
            if (showBackpack)
                BackpackCard.IsVisible = true;
            else if (hasCargo)
                CargoCard.IsVisible = true;

            // Show route if available
            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true ||
                          !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);

            if (hasRoute)
                RouteCard.IsVisible = true;

            // Show modules if in main ship
            bool inMainShip = status.Flags.HasFlag(Flag.InMainShip) &&
                            !status.OnFoot &&
                            !status.Flags.HasFlag(Flag.InSRV) &&
                            !status.Flags.HasFlag(Flag.InFighter);

            if (inMainShip)
                ModulesCard.IsVisible = true;

            // Always show flags if we're showing panels
            FlagsCard.IsVisible = true;

            // Now that visibility is set, update the layout
            UpdateCardLayout();
        }

        private void UpdateCardLayout()
        {
            // Use the layout manager if it's been initialized
            if (_layoutManager != null)
            {
                _layoutManager.UpdateLayout();
            }
            else
            {
                // Otherwise, use the original layout method
                UpdateCardLayoutOriginal();
            }
        }

        private void UpdateCardLayoutOriginal()
        {
            // First, clear all columns to ensure fresh layout
            if (_mainGrid != null)
            {
                _mainGrid.ColumnDefinitions.Clear();

                // Remove all cards from the grid (but not other elements like buttons)
                for (int i = _mainGrid.Children.Count - 1; i >= 0; i--)
                {
                    if (_mainGrid.Children[i] is Card)
                    {
                        _mainGrid.Children.RemoveAt(i);
                    }
                }

                // Get visible cards in the order we want to display them
                var visibleCards = new List<CardViewModel>();

                // Always add summary first if visible
                if (SummaryCard.IsVisible)
                    visibleCards.Add(SummaryCard);

                // Add cargo/backpack next (only one will be visible)
                if (CargoCard.IsVisible)
                    visibleCards.Add(CargoCard);
                else if (BackpackCard.IsVisible)
                    visibleCards.Add(BackpackCard);

                // Add route
                if (RouteCard.IsVisible)
                    visibleCards.Add(RouteCard);

                // Add modules (will take extra space)
                if (ModulesCard.IsVisible)
                    visibleCards.Add(ModulesCard);

                // Add flags last
                if (FlagsCard.IsVisible)
                    visibleCards.Add(FlagsCard);

                // Add column definitions for each card
                for (int i = 0; i < visibleCards.Count; i++)
                {
                    // Make modules card take extra space
                    if (visibleCards[i] == ModulesCard)
                        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                    else
                        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                // Now add the cards to the grid
                for (int i = 0; i < visibleCards.Count; i++)
                {
                    var card = visibleCards[i];

                    // Create the materialDesign Card
                    var cardElement = new Card
                    {
                        Margin = new Thickness(5),
                        Padding = new Thickness(5),
                        DataContext = card
                    };

                    // Create appropriate content based on card type
                    if (card == SummaryCard)
                        cardElement.Content = new Controls.SummaryCard { DataContext = card };
                    else if (card == CargoCard)
                        cardElement.Content = new Controls.CargoCard { DataContext = card };
                    else if (card == BackpackCard)
                        cardElement.Content = new Controls.BackpackCard { DataContext = card };
                    else if (card == RouteCard)
                        cardElement.Content = new Controls.RouteCard { DataContext = card };
                    else if (card == ModulesCard)
                        cardElement.Content = new Controls.ModulesCard { DataContext = card };
                    else if (card == FlagsCard)
                        cardElement.Content = new Controls.FlagsCard { DataContext = card };

                    // Add to grid
                    Grid.SetColumn(cardElement, i);
                    _mainGrid.Children.Add(cardElement);
                }
            }
        }
        // Public method to refresh layout when font size changes
        public void RefreshLayout()
        {
            // First refresh visibility to ensure the right cards are shown
            RefreshCardVisibility();

            // Force recreate all cards to apply new font sizes
            RecreateAllCards();

            // Then update the layout to apply new spacing/fonts
            UpdateCardLayout();
        }

        // Forces recreation of all cards to apply new font settings
        private void RecreateAllCards()
        {
            // Make sure we're on the UI thread
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(RecreateAllCards);
                return;
            }

            if (_mainGrid == null) return;

            // Clear all card elements from the grid
            for (int i = _mainGrid.Children.Count - 1; i >= 0; i--)
            {
                if (_mainGrid.Children[i] is Card)
                {
                    _mainGrid.Children.RemoveAt(i);
                }
            }

            // Force layout update
            _mainGrid.UpdateLayout();
        }
    
        private void OpenOptions()
        {
            // This will be handled in the view
            System.Diagnostics.Debug.WriteLine("Open Options requested");
        }

        public void ShowToast(string message)
        {
            ToastQueue.Enqueue(message);
        }
    }
}