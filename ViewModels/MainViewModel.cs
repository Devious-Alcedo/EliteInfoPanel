using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
        public bool IsDevelopmentMode => SettingsManager.Load().DevelopmentMode;
        public ObservableCollection<CardViewModel> Cards { get; } = new ObservableCollection<CardViewModel>();

        public BackpackViewModel BackpackCard { get; }
        public FleetCarrierCargoViewModel FleetCarrierCard { get; }

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
            FleetCarrierCard = new FleetCarrierCargoViewModel(_gameState) { Title = "Fleet Carrier Cargo" };
            // Add cards to collection
            Cards.Add(SummaryCard);
            Cards.Add(CargoCard);
            Cards.Add(BackpackCard);
            Cards.Add(RouteCard);
            Cards.Add(ModulesCard);
            Cards.Add(FlagsCard);
            Cards.Add(ColonizationCard);
            Cards.Add(FleetCarrierCard); // ✅ reuse the existing instance


            // Subscribe to PropertyChanged events from GameStateService
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Subscribe to HyperspaceJumping events
            _gameState.HyperspaceJumping += OnHyperspaceJumping;

            // Initial update based on current state
            UpdateLoadingState();

            // Apply initial font size
            double scale = SettingsManager.Load().UseFloatingWindow
                ? SettingsManager.Load().FloatingFontScale
                : SettingsManager.Load().FullscreenFontScale;

            double baseFontSize = AppSettings.DEFAULT_FULLSCREEN_BASE * scale;

            foreach (var card in Cards)
            {
                card.FontSize = baseFontSize;
            }

            // Check if game state is already loaded
            if (_gameState.FirstLoadCompleted)
            {
                // Data already loaded, initialize UI immediately
                Log.Information("MainViewModel: GameState already loaded, initializing UI immediately");
                InitializeUI();
            }
            else
            {
                // Wait for data to be loaded
                Log.Information("MainViewModel: Waiting for GameState to complete loading");
                _gameState.FirstLoadCompletedEvent += () => {
                    Log.Information("MainViewModel: GameState loading completed, now initializing UI");
                    InitializeUI();
                };
            }

            // Apply user preferences AFTER everything else
            ApplyUserCardPreferences();

            // IMPORTANT: Add this explicit check for colonization data
            // It needs to happen AFTER ApplyUserCardPreferences to override it if needed
            if (_gameState.CurrentColonization != null)
            {
                Log.Information("MainViewModel: Found colonization data after initialization - making card visible");
                UpdateColonizationCardVisibility();

            }
            EventAggregator.Instance.Subscribe<CardVisibilityChangedEvent>(OnCardVisibilityChanged);
            EventAggregator.Instance.Subscribe<LayoutRefreshRequestEvent>(OnLayoutRefreshRequested);

        }
        private void OnCardVisibilityChanged(CardVisibilityChangedEvent e)
        {
            // We receive notifications here but don't trigger an immediate refresh
            // Instead, we queue a single refresh
            if (!_layoutChangePending)
            {
                _layoutChangePending = true;

                // Use dispatcher to batch all visibility changes
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _layoutChangePending = false;
                    UpdateCardLayout(e.RequiresLayoutRefresh);
                }), DispatcherPriority.Background);
            }
        }

        private void OnLayoutRefreshRequested(LayoutRefreshRequestEvent e)
        {
            RefreshLayout(e.ForceRebuild);
        }
        private void ApplyUserCardPreferences()
        {
            var settings = SettingsManager.Load();

            Log.Information("Applying user card preferences from settings");

            // Apply user preferences to each card
            SummaryCard.IsUserEnabled = settings.ShowSummary;
            FlagsCard.IsUserEnabled = settings.ShowFlags;
            CargoCard.IsUserEnabled = settings.ShowCargo;
            BackpackCard.IsUserEnabled = settings.ShowBackpack;
            RouteCard.IsUserEnabled = settings.ShowRoute;
            ModulesCard.IsUserEnabled = settings.ShowModules;
            ColonizationCard.IsUserEnabled = settings.ShowColonisation;
            FleetCarrierCard.IsUserEnabled = settings.ShowFleetCarrierCargoCard;

            // Log the visibility status
            Log.Debug("Card preferences applied - Cargo: {0}, Colonization: {1}",
                settings.ShowCargo, settings.ShowColonisation);

            // Force refresh of visibility
            RefreshCardVisibility(true);
        }

        private void InitializeUI()
        {
            // Set the initialization flag
            _initializationComplete = true;

            // Initialize all cards
            SetInitialCardVisibility();

            // Force a layout refresh
            RefreshLayout(true);

            Log.Information("MainViewModel: UI initialization completed");
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

            // Use dispatcher for a single update
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Single batch update of all cards
                    UpdateCardVisibility();

                    if (forceRebuild)
                    {
                        RecreateAllCards();
                    }

                    UpdateCardLayout(forceRebuild);

                    _layoutChangePending = false;
                    Log.Information("MainViewModel: Layout refresh completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in RefreshLayout");
                    _layoutChangePending = false;
                }
            }), DispatcherPriority.Background);
        }
        private void UpdateCardVisibility()
        {
            var status = _gameState.CurrentStatus;
            if (status == null) return;

            var settings = SettingsManager.Load();

            // Determine global conditions for visibility
            bool globalShowCondition = !_gameState.IsHyperspaceJumping && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            if (!globalShowCondition)
            {
                foreach (var card in Cards.Where(c => !(c is ColonizationViewModel) && !(c is FleetCarrierCargoViewModel)))
                {
                    card.SetContextVisibility(false);
                }
                return;
            }


            // Now evaluate each card once - in a specific order

            // Summary card
            SummaryCard.SetContextVisibility(true); // CHANGED: Use SetContextVisibility
            SummaryCard.IsUserEnabled = settings.ShowSummary; // ADDED: Set user preference directly

            // Determine mutually exclusive cards
            bool showBackpack = status.OnFoot &&
                                (_gameState.CurrentBackpack?.Inventory?.Count > 0);
            bool showCargo = !showBackpack &&
                             (_gameState.CurrentCargo?.Inventory?.Count > 0);

            BackpackCard.SetContextVisibility(showBackpack); // CHANGED: Use SetContextVisibility
            BackpackCard.IsUserEnabled = settings.ShowBackpack; // ADDED: Set user preference directly

            CargoCard.SetContextVisibility(showCargo); // CHANGED: Use SetContextVisibility
            CargoCard.IsUserEnabled = settings.ShowCargo; // ADDED: Set user preference directly

            // Route card
            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true ||
                           !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
            RouteCard.SetContextVisibility(hasRoute); // CHANGED: Use SetContextVisibility
            RouteCard.IsUserEnabled = settings.ShowRoute; // ADDED: Set user preference directly

            // Modules card  
            bool inMainShip = status.Flags.HasFlag(Flag.InMainShip) &&
                            !status.OnFoot &&
                            !status.Flags.HasFlag(Flag.InSRV) &&
                            !status.Flags.HasFlag(Flag.InFighter);
            ModulesCard.SetContextVisibility(inMainShip); // CHANGED: Use SetContextVisibility
            ModulesCard.IsUserEnabled = settings.ShowModules; // ADDED: Set user preference directly

            // Flags card
            FlagsCard.SetContextVisibility(true); // CHANGED: Use SetContextVisibility
            FlagsCard.IsUserEnabled = settings.ShowFlags; // ADDED: Set user preference directly

            // Colonization card - evaluated once
           
            ColonizationCard.SetContextVisibility(true); // Always set the context to true
            ColonizationCard.IsUserEnabled = settings.ShowColonisation; // Let user setting control visibility
                                                                        // Fleet Carrier Cargo card (user controlled only)
            FleetCarrierCard.SetContextVisibility(true); // Always context-visible
            FleetCarrierCard.IsUserEnabled = settings.ShowFleetCarrierCargoCard;  // Respect user setting
            Log.Information("FleetCarrierCard.SetContextVisibility({Visible}), IsUserEnabled: {Enabled}",
                true, settings.ShowFleetCarrierCargoCard);

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
                case nameof(GameStateService.CurrentColonization):
                    Log.Information("GameStateService.CurrentColonization changed - updating card visibility");
                    UpdateColonizationCardVisibility();
                    break;
                case nameof(GameStateService.CurrentCarrierCargo):
                    UpdateCarrierCargoVisibility();
                    break;


            }
        }
        // In MainViewModel.UpdateCarrierCargoVisibility or similar
        private void UpdateCarrierCargoVisibility()
        {
            bool hasCarrierCargo = _gameState.CurrentCarrierCargo?.Any() == true;
            bool isJumping = _gameState.IsHyperspaceJumping;

            bool shouldShow = hasCarrierCargo && !isJumping &&
                             SettingsManager.Load().ShowFleetCarrierCargoCard;

            Log.Information("Fleet Carrier card visibility check: HasCargo={HasCargo}, IsJumping={IsJumping}, ShouldShow={ShouldShow}",
                hasCarrierCargo, isJumping, shouldShow);

            FleetCarrierCard.SetContextVisibility(shouldShow);
        }

        // Add this method back to MainViewModel
        private void UpdateColonizationCardVisibility()
        {
            try
            {
                // Get user preference 
                var settings = SettingsManager.Load();
                bool userEnabled = settings.ShowColonisation;

                // IMPORTANT: Check if we have actual data, regardless of what the status says
                bool hasData = _gameState.CurrentColonization != null &&
                              _gameState.CurrentColonization.ResourcesRequired?.Count > 0;

                Log.Information("Updating ColonizationCard visibility: UserEnabled={UserEnabled}, HasData={HasData}",
                              userEnabled, hasData);

                // Set context visibility to true if we have data (override the usual game state logic)
                if (hasData)
                {
                    // Always set context visibility to true if we have data
                    ColonizationCard.SetContextVisibility(true);
                    ColonizationCard.IsUserEnabled = userEnabled;

                    Log.Information("ColonizationCard should be visible: HasData=true, UserEnabled={UserEnabled}",
                                  userEnabled);

                    // Force a layout refresh
                    RefreshLayout(true);
                }
                else if (ColonizationCard.IsVisible)
                {
                    // Only hide if we don't have data and it's currently visible
                    ColonizationCard.SetContextVisibility(false);
                    Log.Information("ColonizationCard hidden due to no data");
                    RefreshLayout(true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating colonization card visibility");
            }
        }
        private void UpdateColonizationData()
        {
            try
            {
                // Check if the colonization data exists and is active
                bool hasActiveColonization = _gameState.CurrentColonization != null &&
                                            !_gameState.CurrentColonization.ConstructionComplete &&
                                            !_gameState.CurrentColonization.ConstructionFailed;

                // Get user preferences
                var settings = SettingsManager.Load();
                bool userEnabled = settings.ShowColonisation;

                Log.Information("MainViewModel: Updating colonization data - HasData={HasData}, UserEnabled={UserEnabled}",
                    hasActiveColonization, userEnabled);

                // FIXED: Instead of directly setting IsVisible, use the proper methods
                // Set context visibility based on data availability
                ColonizationCard.SetContextVisibility(hasActiveColonization);

                // Set user preference
                ColonizationCard.IsUserEnabled = userEnabled;

                // The final visibility will be determined by CardViewModel.UpdateIsVisible()
                // which combines both context visibility and user preference

                // Check if we need to refresh the layout (this won't change)
                bool shouldBeVisible = hasActiveColonization && userEnabled;
                if (shouldBeVisible)
                {
                    // Force layout refresh to ensure colonization card is displayed
                    RefreshLayout(true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating colonization data from MainViewModel");
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
            try
            {
                if (_gameState.CurrentStatus?.OnFoot == true)
                    return; // Backpack takes precedence

                bool hasCargo = (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;
                bool shouldBeContextVisible = hasCargo && !_gameState.IsHyperspaceJumping;

                // Log what's happening
                Log.Debug("MainViewModel.UpdateCargoVisibility: hasCargo={0}, " +
                         "shouldBeContextVisible={1}, IsUserEnabled={2}",
                         hasCargo, shouldBeContextVisible, CargoCard.IsUserEnabled);

                // Update context visibility (user preference untouched)
                CargoCard.SetContextVisibility(shouldBeContextVisible);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating cargo visibility");
            }
        }

        private void UpdateBackpackVisibility()
        {
            bool isOnFoot = _gameState.CurrentStatus?.OnFoot == true;
            bool hasItems = (_gameState.CurrentBackpack?.Inventory?.Count ?? 0) > 0;
            bool shouldShow = isOnFoot && hasItems && !_gameState.IsHyperspaceJumping;

            if (BackpackCard.IsVisible != shouldShow)
            {
                BackpackCard.SetContextVisibility(shouldShow);
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
                RouteCard.SetContextVisibility(shouldShow);
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
                ModulesCard.SetContextVisibility(shouldShow);
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
            // Automatically return true if in development mode
            if (SettingsManager.Load().DevelopmentMode)
            {
                return true;
            }

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

            // Set context visibility for each card

            // Summary card visibility
            bool oldSummaryVisible = SummaryCard.IsVisible;
            SummaryCard.SetContextVisibility(shouldShowPanels);
            if (oldSummaryVisible != SummaryCard.IsVisible)
                visibilityChanged = true;

            // Backpack visibility
            bool shouldShowBackpack = shouldShowPanels && status.OnFoot;
            bool oldBackpackVisible = BackpackCard.IsVisible;
            BackpackCard.SetContextVisibility(shouldShowBackpack);
            if (oldBackpackVisible != BackpackCard.IsVisible)
                visibilityChanged = true;

            // Cargo visibility
            bool hasCargo = (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;
            bool shouldShowCargo = shouldShowPanels && !shouldShowBackpack && hasCargo;
            bool oldCargoVisible = CargoCard.IsVisible;
            CargoCard.SetContextVisibility(shouldShowCargo);
            if (oldCargoVisible != CargoCard.IsVisible)
                visibilityChanged = true;

            // Route visibility
            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true ||
                          !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
            bool shouldShowRoute = shouldShowPanels && hasRoute;
            bool oldRouteVisible = RouteCard.IsVisible;
            RouteCard.SetContextVisibility(shouldShowRoute);
            if (oldRouteVisible != RouteCard.IsVisible)
                visibilityChanged = true;

            // Modules visibility
            bool inMainShip = shouldShowPanels && status.Flags.HasFlag(Flag.InMainShip) &&
                            !status.OnFoot &&
                            !status.Flags.HasFlag(Flag.InSRV) &&
                            !status.Flags.HasFlag(Flag.InFighter);
            bool oldModulesVisible = ModulesCard.IsVisible;
            ModulesCard.SetContextVisibility(inMainShip);
            if (oldModulesVisible != ModulesCard.IsVisible)
                visibilityChanged = true;

            // Flags visibility
            bool shouldShowFlags = shouldShowPanels;
            bool oldFlagsVisible = FlagsCard.IsVisible;
            FlagsCard.SetContextVisibility(shouldShowFlags);
            if (oldFlagsVisible != FlagsCard.IsVisible)
                visibilityChanged = true;

            // carrier cargo visibility
            bool shouldShowCarrierCargo = shouldShowPanels;
            bool oldCarrierVisible = FleetCarrierCard.IsVisible;
            FleetCarrierCard.SetContextVisibility(shouldShowCarrierCargo);
            if (oldCarrierVisible != FleetCarrierCard.IsVisible)
                visibilityChanged = true;



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
                // FIXED: Use SetContextVisibility instead of direct assignment
                card.SetContextVisibility(false);
            }

            if (_gameState.CurrentColonization != null)
            {
                var settings = SettingsManager.Load();
                Log.Information("Colonization data found during initial visibility setup");

                // FIXED: Set both context visibility and user preference correctly
                ColonizationCard.SetContextVisibility(true);
                ColonizationCard.IsUserEnabled = settings.ShowColonisation;
            }
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

            // Get user preferences
            var settings = SettingsManager.Load();

            // Calculate global visibility state
            bool shouldShowPanels = !_gameState.IsHyperspaceJumping && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            if (!shouldShowPanels)
            {
                foreach (var card in Cards.Where(c => !(c is ColonizationViewModel)))
                {
                    card.SetContextVisibility(false);
                }
                return;
            }

            // Now set context visibility for individual cards based on conditions

            // Summary is always visible if global state is true
            SummaryCard.SetContextVisibility(true);

            // Determine conditions for backpack and cargo
            bool backpackCondition = status.OnFoot;
            bool hasCargo = (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;

            // Set context visibility appropriately
            BackpackCard.SetContextVisibility(backpackCondition);
            CargoCard.SetContextVisibility(hasCargo && !backpackCondition);

            // Show route if available
            bool hasRoute = _gameState.CurrentRoute?.Route?.Any() == true ||
                            !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name);
            RouteCard.SetContextVisibility(hasRoute);

            // Show modules if in main ship
            bool inMainShip = status.Flags.HasFlag(Flag.InMainShip) &&
                            !status.OnFoot &&
                            !status.Flags.HasFlag(Flag.InSRV) &&
                            !status.Flags.HasFlag(Flag.InFighter);
            ModulesCard.SetContextVisibility(inMainShip);

            // Flags are always contextually visible if global state is true
            FlagsCard.SetContextVisibility(true);

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
                // Add fleet carrier card
                if (FleetCarrierCard.IsVisible)
                    visibleCards.Add(FleetCarrierCard);
                Log.Information("FleetCarrierCard.IsVisible = {Visible}", FleetCarrierCard.IsVisible);

                // Add flags last
                if (FlagsCard.IsVisible)
                    visibleCards.Add(FlagsCard);

                // Add colonization card 
                if (ColonizationCard.IsVisible)
                    visibleCards.Add(ColonizationCard);
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
                    else if (card == ColonizationCard)
                        cardElement.Content = new EliteInfoPanel.Controls.ColonizationCard { DataContext = card };
                    else if (card == FleetCarrierCard)
                        cardElement.Content = new EliteInfoPanel.Controls.FleetCarrierCargoCard { DataContext = card };


                    // Add to grid
                    Grid.SetColumn(cardElement, i);
                    _mainGrid.Children.Add(cardElement);
                }
            }
        }
        #endregion
    }
}