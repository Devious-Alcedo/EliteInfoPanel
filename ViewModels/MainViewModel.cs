using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Private Fields
        public readonly GameStateService _gameState;
        private bool _isCarrierJumping;
        private bool _isLoading = true;
        private CardLayoutManager _layoutManager;
        private Grid _mainGrid;
        private SnackbarMessageQueue _toastQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        private bool _isFullScreenMode;
        private readonly bool _useFloatingWindow;
        private bool _layoutChangePending = false;
        #endregion Private Fields

        #region Public Properties
        public bool IsFullScreenMode
        {
            get => _isFullScreenMode;
            set
            {
                if (SetProperty(ref _isFullScreenMode, value))
                {
                    Log.Information("→ IsFullScreenMode changed: {Value}", value);
                }
            }
        }

        public ObservableCollection<CardViewModel> Cards { get; } = new ObservableCollection<CardViewModel>();

        public BackpackViewModel BackpackCard { get; }

        public CargoViewModel CargoCard { get; }
        public ColonizationViewModel ColonizationCard { get; }
        public RelayCommand CloseCommand { get; } = new RelayCommand(_ => Application.Current.Shutdown());

        public FlagsViewModel FlagsCard { get; }

        public bool IsCarrierJumping
        {
            get => _isCarrierJumping;
            set => SetProperty(ref _isCarrierJumping, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ModulesViewModel ModulesCard { get; }

        public RelayCommand OpenOptionsCommand { get; set; }

        public RouteViewModel RouteCard { get; }

        public SummaryViewModel SummaryCard { get; }

        public SnackbarMessageQueue ToastQueue
        {
            get => _toastQueue;
            set => SetProperty(ref _toastQueue, value);
        }
        #endregion

        #region Constructor
        public MainViewModel(GameStateService gameState, bool useFloatingWindow)
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            _useFloatingWindow = useFloatingWindow;

            // Initialize commands
            OpenOptionsCommand = new RelayCommand(_ => OpenOptions());

            // Initialize card ViewModels
            SummaryCard = new SummaryViewModel(gameState) { Title = "Summary" };
            CargoCard = new CargoViewModel(gameState) { Title = "Cargo" };
            BackpackCard = new BackpackViewModel(gameState) { Title = "Backpack" };
            RouteCard = new RouteViewModel(gameState) { Title = "Nav Route" };
            ModulesCard = new ModulesViewModel(gameState) { Title = "Ship Modules" };
            FlagsCard = new FlagsViewModel(gameState) { Title = "Status Flags" };
            ColonizationCard = new ColonizationViewModel(gameState) { Title = "Colonization Project" };

            // Add cards to collection
            Cards.Add(SummaryCard);
            Cards.Add(CargoCard);
            Cards.Add(BackpackCard);
            Cards.Add(RouteCard);
            Cards.Add(ModulesCard);
            Cards.Add(FlagsCard);
            Cards.Add(ColonizationCard);

            // Subscribe to PropertyChanged events from GameStateService
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Subscribe to HyperspaceJumping events
            _gameState.HyperspaceJumping += OnHyperspaceJumping;

            // Initial update based on current state
            UpdateLoadingState();

            // Initialize card visibilities
            SetInitialCardVisibility();

            // Apply initial font size
            double scale = SettingsManager.Load().UseFloatingWindow
                ? SettingsManager.Load().FloatingFontScale
                : SettingsManager.Load().FullscreenFontScale;

            double baseFontSize = AppSettings.DEFAULT_FULLSCREEN_BASE * scale;

            foreach (var card in Cards)
            {
                card.FontSize = baseFontSize;
            }

            // Add a flag to indicate initialization is complete
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                _initializationComplete = true;
                // Notify all cards that they can now find MainViewModel
                RefreshLayout(true);
            }), DispatcherPriority.Background);
        }

        // Add this field to MainViewModel
        private bool _initializationComplete = false;     
        #endregion

        #region Public Methods
        public void ApplyWindowModeFromSettings()
        {
            var isFullscreen = !SettingsManager.Load().UseFloatingWindow;
            IsFullScreenMode = isFullscreen;
        }

        public void RefreshLayout(bool forceRebuild = false)
        {
            Log.Information("MainViewModel: RefreshLayout called - forceRebuild={0}", forceRebuild);
            if (!_initializationComplete && !forceRebuild)
            {
                Log.Debug("RefreshLayout called before initialization complete, deferring");
                return;
            }
            if (_layoutChangePending && !forceRebuild)
                return; // Avoid redundant refreshes

            _layoutChangePending = true;

            // Use dispatcher to batch layout updates and avoid multiple refreshes in same frame
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    // Refresh visibility first
                    RefreshCardVisibility(false);
                    UpdateColonizationCardVisibility();
                    if (forceRebuild)
                    {
                        // Force recreate all cards to apply new font sizes
                        RecreateAllCards();
                    }

                    // Update the layout
                    UpdateCardLayout(forceRebuild);

                    _layoutChangePending = false;
                    Log.Information("MainViewModel: Layout refresh completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in RefreshLayout");
                    _layoutChangePending = false;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void SetMainGrid(Grid mainGrid)
        {
            _mainGrid = mainGrid;

            // Initialize layout manager
            var appSettings = SettingsManager.Load();
            _layoutManager = new CardLayoutManager(_mainGrid, appSettings, this);

            // Do initial layout
            UpdateCardLayout(true);
        }

        public void ShowToast(string message)
        {
            ToastQueue.Enqueue(message);
        }

        public void UpdateLoadingState()
        {
            bool wasLoading = IsLoading;
            IsLoading = !IsEliteRunning();

            if (wasLoading && !IsLoading)
            {
                // When we first load, we need to do a full refresh
                RefreshCardVisibility(true);
            }
        }
        #endregion

        #region Private Methods
        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Use targeted updates based on which property changed
            switch (e.PropertyName)
            {
                case nameof(GameStateService.CurrentStatus):
                    OnStatusChanged();
                    break;

                case nameof(GameStateService.CurrentCargo):
                    UpdateCargoVisibility();
                    break;

                case nameof(GameStateService.CurrentBackpack):
                    UpdateBackpackVisibility();
                    break;

                case nameof(GameStateService.CurrentRoute):
                    UpdateRouteVisibility();
                    break;

                case nameof(GameStateService.CurrentLoadout):
                    UpdateModulesVisibility();
                    break;

                case nameof(GameStateService.FleetCarrierJumpInProgress):
                case nameof(GameStateService.CarrierJumpDestinationSystem):
                    UpdateCarrierJumpState();
                    if (SummaryCard != null)
                    {
                        // This tells the summary card to update its state
                        SummaryCard.Initialize();
                    }
                    break;

                case nameof(GameStateService.IsHyperspaceJumping):
                    // We still need to handle this to update card visibility
                    RefreshCardVisibility(true);
                    break;
            }
        }

        private void OnStatusChanged()
        {
            var status = _gameState.CurrentStatus;
            if (status == null || status.Flags == Flag.None)
            {
                Log.Debug("Game data not ready. Still waiting...");
                return;
            }

            bool wasLoading = IsLoading;
            UpdateLoadingState();

            if (wasLoading && !IsLoading)
            {
                // Full refresh if loading state changed
                RefreshCardVisibility(true);
            }
            else
            {
                // Otherwise just check if any cards need showing/hiding
                EnsureCorrectCardsVisible();
            }
        }

        private void UpdateCarrierJumpState()
        {
            if (!string.IsNullOrEmpty(_gameState.CarrierJumpDestinationSystem) &&
                _gameState.FleetCarrierJumpInProgress == false)
            {
                IsCarrierJumping = false;
            }
        }

        private void UpdateCargoVisibility()
        {
            if (_gameState.CurrentStatus?.OnFoot == true)
                return; // Backpack takes precedence

            bool hasCargo = (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;
            bool shouldShow = hasCargo && !_gameState.IsHyperspaceJumping;

            if (CargoCard.IsVisible != shouldShow)
            {
                CargoCard.IsVisible = shouldShow;
                UpdateCardLayout();
            }
        }

        private void UpdateBackpackVisibility()
        {
            bool isOnFoot = _gameState.CurrentStatus?.OnFoot == true;
            bool hasItems = (_gameState.CurrentBackpack?.Inventory?.Count ?? 0) > 0;
            bool shouldShow = isOnFoot && hasItems && !_gameState.IsHyperspaceJumping;

            if (BackpackCard.IsVisible != shouldShow)
            {
                BackpackCard.IsVisible = shouldShow;
                UpdateCardLayout();
            }
        }

        private void UpdateRouteVisibility()
        {
            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true ||
                          !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);

            bool shouldShow = hasRoute && !_gameState.IsHyperspaceJumping;

            if (RouteCard.IsVisible != shouldShow)
            {
                RouteCard.IsVisible = shouldShow;
                UpdateCardLayout();
            }
        }

        private void UpdateModulesVisibility()
        {
            bool inMainShip = _gameState.CurrentStatus?.Flags.HasFlag(Flag.InMainShip) == true &&
                            !(_gameState.CurrentStatus?.OnFoot == true) &&
                            !_gameState.CurrentStatus.Flags.HasFlag(Flag.InSRV) &&
                            !_gameState.CurrentStatus.Flags.HasFlag(Flag.InFighter);

            bool shouldShow = inMainShip && !_gameState.IsHyperspaceJumping;

            if (ModulesCard.IsVisible != shouldShow)
            {
                ModulesCard.IsVisible = shouldShow;
                UpdateCardLayout();
            }
        }

        private void OnHyperspaceJumping(bool jumping, string systemName)
        {
            // Refresh card visibility when hyperspace state changes
            RefreshCardVisibility(true);
        }

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

        private void OpenOptions()
        {
            // This will be handled in the view
            System.Diagnostics.Debug.WriteLine("Open Options requested");
        }

        private void RecreateAllCards()
        {
            // Make sure we're on the UI thread
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(RecreateAllCards);
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
        private void UpdateColonizationCardVisibility()
        {
            try
            {
                // Check if the colonization data exists and is active
                bool hasActiveColonization = _gameState.CurrentColonization != null &&
                                            !_gameState.CurrentColonization.ConstructionComplete &&
                                            !_gameState.CurrentColonization.ConstructionFailed;

                // Only update visibility if it's changed
                if (ColonizationCard.IsVisible != hasActiveColonization)
                {
                    ColonizationCard.IsVisible = hasActiveColonization;
                    Log.Debug("ColonizationCard visibility set to {Visible}", hasActiveColonization);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating colonization card visibility");
            }
        }
        private void EnsureCorrectCardsVisible()
        {
            // Make sure we're on the UI thread
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(EnsureCorrectCardsVisible);
                return;
            }

            var status = _gameState.CurrentStatus;
            if (status == null) return;

            // Calculate all visibility states
            bool shouldShowPanels = !_gameState.IsHyperspaceJumping && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            bool visibilityChanged = false;

            // Summary card visibility
            bool shouldShowSummary = shouldShowPanels;
            if (SummaryCard.IsVisible != shouldShowSummary)
            {
                SummaryCard.IsVisible = shouldShowSummary;
                visibilityChanged = true;
            }

            // Backpack visibility
            bool shouldShowBackpack = shouldShowPanels && status.OnFoot;
            if (BackpackCard.IsVisible != shouldShowBackpack)
            {
                BackpackCard.IsVisible = shouldShowBackpack;
                visibilityChanged = true;
            }

            // Cargo visibility
            bool hasCargo = (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;
            bool shouldShowCargo = shouldShowPanels && !shouldShowBackpack && hasCargo;
            if (CargoCard.IsVisible != shouldShowCargo)
            {
                CargoCard.IsVisible = shouldShowCargo;
                visibilityChanged = true;
            }

            // Route visibility
            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true ||
                          !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
            bool shouldShowRoute = shouldShowPanels && hasRoute;
            if (RouteCard.IsVisible != shouldShowRoute)
            {
                RouteCard.IsVisible = shouldShowRoute;
                visibilityChanged = true;
            }

            // Modules visibility
            bool inMainShip = shouldShowPanels && status.Flags.HasFlag(Flag.InMainShip) &&
                            !status.OnFoot &&
                            !status.Flags.HasFlag(Flag.InSRV) &&
                            !status.Flags.HasFlag(Flag.InFighter);
            if (ModulesCard.IsVisible != inMainShip)
            {
                ModulesCard.IsVisible = inMainShip;
                visibilityChanged = true;
            }

            // Flags visibility
            bool shouldShowFlags = shouldShowPanels;
            if (FlagsCard.IsVisible != shouldShowFlags)
            {
                FlagsCard.IsVisible = shouldShowFlags;
                visibilityChanged = true;
            }

            // Only update layout if visibility changed
            if (visibilityChanged)
            {
                UpdateCardLayout(false);
            }
        }

        private void SetInitialCardVisibility()
        {
            // Default state - hide all cards initially
            foreach (var card in Cards)
            {
                card.IsVisible = false;
            }

            // We'll let the status update handle showing the right cards
        }

        private void RefreshCardVisibility(bool updateLayout = true)
        {
            // Make sure we're on the UI thread
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => RefreshCardVisibility(updateLayout));
                return;
            }

            var status = _gameState.CurrentStatus;
            if (status == null) return;

            // Calculate all visibility states
            bool shouldShowPanels = !_gameState.IsHyperspaceJumping && (
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
            if (updateLayout)
                UpdateCardLayout(false);
        }

        private void UpdateCardLayout(bool forceRebuild = false)
        {
            // Use the layout manager if it's been initialized
            if (_layoutManager != null)
            {
                _layoutManager.UpdateLayout(forceRebuild);
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
                        cardElement.Content = new EliteInfoPanel.Controls.SummaryCard { DataContext = card };
                    else if (card == CargoCard)
                        cardElement.Content = new EliteInfoPanel.Controls.CargoCard { DataContext = card };
                    else if (card == BackpackCard)
                        cardElement.Content = new EliteInfoPanel.Controls.BackpackCard { DataContext = card };
                    else if (card == RouteCard)
                        cardElement.Content = new EliteInfoPanel.Controls.RouteCard { DataContext = card };
                    else if (card == ModulesCard)
                        cardElement.Content = new EliteInfoPanel.Controls.ModulesCard { DataContext = card };
                    else if (card == FlagsCard)
                        cardElement.Content = new EliteInfoPanel.Controls.FlagsCard { DataContext = card };

                    // Add to grid
                    Grid.SetColumn(cardElement, i);
                    _mainGrid.Children.Add(cardElement);
                }
            }
        }
        #endregion
    }
}