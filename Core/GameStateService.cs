using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using EliteInfoPanel.Services;
using System.Windows.Media;
using System.Windows.Threading;
using EliteInfoPanel.Core.EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using Serilog;
using Serilog.Core;
using EliteInfoPanel.Core.Services;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService : INotifyPropertyChanged
    {
        #region Private Fields

        private const string CarrierCargoFile = "CarrierCargo.json";
        private const string RouteProgressFile = "RouteProgress.json";
        private static readonly SolidColorBrush CountdownGoldBrush = new SolidColorBrush(Colors.Gold);
        private static readonly SolidColorBrush CountdownGreenBrush = new SolidColorBrush(Colors.Green);
        private static readonly SolidColorBrush CountdownRedBrush = new SolidColorBrush(Colors.Red);
        private readonly CarrierCargoTracker _carrierCargoTracker = new();
        private readonly MqttService _mqttService;
        private readonly string CarrierCargoFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EliteInfoPanel", "carrier_cargo_state.json");
        private readonly string ColonizationDataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EliteInfoPanel",
            "ColonizationData.json");

        private bool _cargoTrackingInitialized = false;
        private Dictionary<string, int> _carrierCargo = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<long, ColonizationData> _colonizationDepots = new();
        private int _combatRank;
        private string _commanderName;
        private int _cqcRank;
        private BackpackJson _currentBackpack;
        private CargoJson _currentCargo;
        private List<CarrierCargoItem> _currentCarrierCargo = new();
        private DockingState _currentDockingState = DockingState.NotDocking;
        private LoadoutJson _currentLoadout;
        private FCMaterialsJson _currentMaterials;
        private NavRouteJson _currentRoute;
        private string _currentStationName;
        private StatusJson _currentStatus;
        private string _currentSystem;
        private (double X, double Y, double Z)? _currentSystemCoordinates;
        private CancellationTokenSource _dockingCts = new CancellationTokenSource();
        private int _exobiologistRank;
        private int _explorationRank;
        private bool _firstLoadCompleted = false;
        private string _hyperspaceDestination;
        private string _hyperspaceStarClass;
        private CancellationTokenSource _hyperspaceTimeoutCts;
        private bool _isDocking;
        private bool _isHyperspaceJumping;
        private bool _isInHyperspace = false;
        private bool _isInitializing = true;
        private bool _isOnFleetCarrier;
        private bool _isRouteLoaded = false;
        private bool _isUpdating = false;
        private readonly Dictionary<string, ManualCargoChange> _manualCarrierCargoChanges = new(StringComparer.OrdinalIgnoreCase);
        private string ManualCarrierCargoFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EliteCompanion", "ManualCarrierCargo.json");
        private readonly object _cargoLock = new object();
        private bool _suppressNextCargoUpdate = false;
        private string _lastFsdTargetSystem;
        private string _lastVisitedSystem;
        private string _legalState = "Clean";
        private double _maxJumpRange;
        private int _mercenaryRank;
        private bool _mqttInitialized = false;
        private HashSet<string> _pendingNotifications = new HashSet<string>();
        private int? _remainingJumps;
        private RouteProgressState _routeProgress = new();
        private bool _routeWasActive = false;
        private long? _selectedDepotMarketId;
        private string _shipLocalised;
        private string _shipName;
        private string _squadronName;
        private int _tradeRank;
        private string _userShipId;
        private string _userShipName;
        private List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>(); // legacy
        private string gamePath;
        private long lastJournalPosition = 0;
        private string latestJournalPath;
        private IGameFilesService _filesService;
        private IFileWatcherService _fileWatcherService;
        private ICarrierCargoService _carrierCargoService;
        private IColonizationService _colonizationService;
        private IRouteProgressService _routeProgressService;
        private IStatusService _statusService;
        private IJournalReader _journalReader;
        private ICarrierJumpManager _carrierJumpManager;

        // Add this field to track the app's UTC start time
        private readonly DateTime _appStartTimeUtc = DateTime.UtcNow;

        #endregion Private Fields

        #region Public Constructors

        internal GameStateService(string path,
            IGameFilesService filesService,
            IFileWatcherService fileWatcherService,
            ICarrierCargoService carrierCargoService,
            IColonizationService colonizationService,
            IRouteProgressService routeProgressService,
            IStatusService statusService,
            IJournalReader journalReader,
            ICarrierJumpManager carrierJumpManager)
        {
            _filesService = filesService;
            _fileWatcherService = fileWatcherService;
            _carrierCargoService = carrierCargoService;
            _colonizationService = colonizationService;
            _routeProgressService = routeProgressService;
            _statusService = statusService;
            _journalReader = journalReader;
            _carrierJumpManager = carrierJumpManager;
            // Subscribe to cargo updates from the service and marshal to UI thread
            _carrierCargoService.CargoUpdated += (sender, args) =>
            {
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        using (BeginUpdate())
                        {
                            _carrierCargo = new Dictionary<string, int>(args.Cargo, StringComparer.OrdinalIgnoreCase);
                            UpdateCurrentCarrierCargoFromDictionary();
                            OnPropertyChanged(nameof(CarrierCargo));
                        }
                    });
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Error applying CargoUpdated event");
                }
            };

            // Subscribe to colonization updates
            _colonizationService.ColonizationUpdated += (sender, args) =>
            {
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _colonizationDepots[args.MarketId] = args.Data;
                        if (!_selectedDepotMarketId.HasValue)
                            _selectedDepotMarketId = args.MarketId;
                        OnPropertyChanged(nameof(ColonizationDepots));
                        OnPropertyChanged(nameof(SelectedColonizationDepot));
                        OnPropertyChanged(nameof(HasValidColonizationData));
                    });
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Error applying ColonizationUpdated event");
                }
            };

            // Subscribe to route progress updates
            _routeProgressService.RouteUpdated += (sender, args) =>
            {
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _routeProgress = args.State ?? new RouteProgressState();
                        OnPropertyChanged(nameof(TotalRemainingJumps));
                    });
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Error applying RouteUpdated event");
                }
            };
            var settings = SettingsManager.Load();
            gamePath = path;

            // Set up individual file watchers for each important file
            SetupFileWatcher("Status.json", () => LoadStatusData());
            SetupFileWatcher("NavRoute.json", () => LoadNavRouteData());

            SetupFileWatcher("Cargo.json", () => LoadCargoData());
            SetupFileWatcher("Backpack.json", () => LoadBackpackData());
            SetupFileWatcher("FCMaterials.json", () => LoadMaterialsData());

            // Journal needs special handling - set up a directory watcher
            SetupJournalWatcher();

            // Initial load of all data
            LoadAllData();

            // Initialize carrier jump timers
            InitializeCarrierJumpTimers();
            
            // Scan journal for pending carrier jump
            ScanForPendingCarrierJump();
            Task.Run(InitializeMqttAsync);
            if (CurrentStatus != null)
            {
                Task.Run(() => PublishStatusToMqtt(CurrentStatus));
                Log.Information("? Initial flag state pushed to MQTT on startup");
            }
        }

        #endregion Public Constructors

        #region Public Events

        public event Action FirstLoadCompletedEvent;

        // Event for hyperspace jump notification
        public event Action<bool, string> HyperspaceJumping;

        public event Action LoadoutUpdated;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Private Enums

        private enum DockingState
        {
            NotDocking,
            DockingRequested,
            DockingGranted,
            Docked
        }

        #endregion Private Enums

        #region Public Properties

        public long? Balance => CurrentStatus?.Balance;
        public Dictionary<string, int> CarrierCargo
        {
            get => _carrierCargo;
            private set
            {
                _carrierCargo = value;
                OnPropertyChanged(nameof(CarrierCargo)); // ? ADD THIS
            }
        }

        // Carrier jump properties moved to GameStateService_CarrierJump.cs partial class

        public IReadOnlyDictionary<long, ColonizationData> ColonizationDepots => _colonizationDepots;
        public int CombatRank
        {
            get => _combatRank;
            private set => SetProperty(ref _combatRank, value);
        }

        public string CommanderName
        {
            get => _commanderName;
            private set => SetProperty(ref _commanderName, value);
        }

        public int CqcRank
        {
            get => _cqcRank;
            private set => SetProperty(ref _cqcRank, value);
        }

        public BackpackJson CurrentBackpack
        {
            get => _currentBackpack;
            private set => SetProperty(ref _currentBackpack, value);
        }

        public CargoJson CurrentCargo
        {
            get => _currentCargo;
            private set => SetProperty(ref _currentCargo, value);
        }

        public List<CarrierCargoItem> CurrentCarrierCargo
        {
            get => _currentCarrierCargo;
            private set => SetProperty(ref _currentCarrierCargo, value);
        }

        public ColonizationData CurrentColonization
        {
            get
            {
                if (_selectedDepotMarketId.HasValue &&
                    _colonizationDepots.TryGetValue(_selectedDepotMarketId.Value, out var depot))
                {
                    return depot;
                }
                return null;
            }
            private set
            {
                if (value != null)
                {
                    _colonizationDepots[value.MarketID] = value;
                    _selectedDepotMarketId = value.MarketID;
                    OnPropertyChanged();
                    SaveAllColonizationData();
                }
            }
        }

        /// <summary>
        /// Returns true if there is valid, active colonization data available for display
        /// </summary>
        public bool HasValidColonizationData
        {
            get
            {
                return CurrentColonization != null &&
                       CurrentColonization.LastUpdated != DateTime.MinValue &&
                       CurrentColonization.ResourcesRequired?.Count > 0 &&
                       !CurrentColonization.ConstructionComplete &&
                       !CurrentColonization.ConstructionFailed;
            }
        }

        public LoadoutJson CurrentLoadout
        {
            get => _currentLoadout;
            set
            {
                if (_currentLoadout != value)
                {
                    _currentLoadout = value;
                    OnPropertyChanged(); // "CurrentLoadout" itself changed
                    OnPropertyChanged(nameof(TotalRemainingJumps)); // total jumps might be affected
                    OnPropertyChanged(nameof(MaxJumpRange)); // max range might be recalculated
                }
            }
        }

        public FCMaterialsJson CurrentMaterials
        {
            get => _currentMaterials;
            private set => SetProperty(ref _currentMaterials, value);
        }

        public NavRouteJson CurrentRoute
        {
            get => _currentRoute;
            set
            {
                if (_currentRoute != value)
                {
                    _currentRoute = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalRemainingJumps)); // crucial
                }
            }
        }

        public string CurrentShip => !string.IsNullOrEmpty(ShipLocalised) ? ShipLocalised :
                                     !string.IsNullOrEmpty(ShipName) ? ShipName : "Unknown";

        public string CurrentStationName
        {
            get => _currentStationName;
            private set => SetProperty(ref _currentStationName, value);
        }

        public StatusJson CurrentStatus
        {
            get => _currentStatus;
            private set => SetProperty(ref _currentStatus, value);
        }

        public string CurrentSystem
        {
            get => _currentSystem;
            private set => SetProperty(ref _currentSystem, value);
        }

        public (double X, double Y, double Z)? CurrentSystemCoordinates
        {
            get => _currentSystemCoordinates;
            set => SetProperty(ref _currentSystemCoordinates, value);
        }

        public int ExobiologistRank
        {
            get => _exobiologistRank;
            private set => SetProperty(ref _exobiologistRank, value);
        }

        public int ExplorationRank
        {
            get => _explorationRank;
            private set => SetProperty(ref _explorationRank, value);
        }

        public bool FirstLoadCompleted => _firstLoadCompleted;

        public double FuelMain => CurrentStatus?.Fuel?.FuelMain ?? 0;
        public double FuelReserve => CurrentStatus?.Fuel?.FuelReservoir ?? 0;
        public string HyperspaceDestination
        {
            get => _hyperspaceDestination;
            private set => SetProperty(ref _hyperspaceDestination, value);
        }

        public string HyperspaceStarClass
        {
            get => _hyperspaceStarClass;
            private set => SetProperty(ref _hyperspaceStarClass, value);
        }

        public bool IsDocking
        {
            get => _isDocking;
            private set => SetProperty(ref _isDocking, value);
        }

        public bool IsHyperspaceJumping
        {
            get => _isHyperspaceJumping;
            private set
            {
                if (SetProperty(ref _isHyperspaceJumping, value))
                {
                    Log.Information("HyperspaceJumping = {Value}", value);
                    HyperspaceJumping?.Invoke(value, HyperspaceDestination);
                }
            }
        }

        public bool IsInHyperspace => _isInHyperspace;

        public bool IsOnFleetCarrier
        {
            get => _isOnFleetCarrier;
            private set
            {
                if (SetProperty(ref _isOnFleetCarrier, value))
                {
                    Log.Information("IsOnFleetCarrier changed to {Value}", value);
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // Notify dependent property
                }
            }
        }

        // JumpArrived and JumpCountdown removed - handled by CarrierJumpState

        public string LastFsdTargetSystem
        {
            get => _lastFsdTargetSystem;
            private set => SetProperty(ref _lastFsdTargetSystem, value);
        }

        public string LastVisitedSystem
        {
            get => _lastVisitedSystem;
            private set => SetProperty(ref _lastVisitedSystem, value);
        }

        public string LegalState
        {
            get => _legalState;
            private set => SetProperty(ref _legalState, value);
        }

        public double MaxJumpRange
        {
            get => _maxJumpRange;
            private set => SetProperty(ref _maxJumpRange, value);
        }

        public int MercenaryRank
        {
            get => _mercenaryRank;
            private set => SetProperty(ref _mercenaryRank, value);
        }

        public int? RemainingJumps
        {
            get => _remainingJumps;
            private set => SetProperty(ref _remainingJumps, value);
        }

        public bool RouteCompleted => CurrentRoute?.Route?.Count == 0;

        public bool RouteWasActive => _routeWasActive;

        public ColonizationData SelectedColonizationDepot
        {
            get => _selectedDepotMarketId.HasValue && _colonizationDepots.TryGetValue(_selectedDepotMarketId.Value, out var depot)
                ? depot : null;
            private set
            {
                if (value != null)
                {
                    _selectedDepotMarketId = value.MarketID;
                    OnPropertyChanged();
                }
            }
        }

        public long? SelectedDepotMarketId
        {
            get => _selectedDepotMarketId;
            set
            {
                if (SetProperty(ref _selectedDepotMarketId, value))
                {
                    OnPropertyChanged(nameof(CurrentColonization));
                }
            }
        }

        public string ShipLocalised
        {
            get => _shipLocalised;
            private set => SetProperty(ref _shipLocalised, value);
        }

        public string ShipName
        {
            get => _shipName;
            private set => SetProperty(ref _shipName, value);
        }

        // ShowCarrierJumpOverlay moved to GameStateService_CarrierJump.cs partial class

        public string SquadronName
        {
            get => _squadronName;
            private set => SetProperty(ref _squadronName, value);
        }

        public int TotalRemainingJumps
        {
            get
            {
                if (CurrentRoute?.Route == null || string.IsNullOrEmpty(CurrentSystem))
                    return 0;

                var routeSystems = CurrentRoute.Route;
                int currentIndex = routeSystems.FindIndex(s =>
                    string.Equals(s.StarSystem, CurrentSystem, StringComparison.OrdinalIgnoreCase));

                if (currentIndex >= 0)
                {
                    return routeSystems.Count - currentIndex - 1;
                }
                else
                {
                    // Current system not found, assume entire route is remaining
                    return routeSystems.Count;
                }
            }
        }

        public int TradeRank
        {
            get => _tradeRank;
            private set => SetProperty(ref _tradeRank, value);
        }

        public string UserShipId
        {
            get => _userShipId;
            set => SetProperty(ref _userShipId, value);
        }

        public string UserShipName
        {
            get => _userShipName;
            set => SetProperty(ref _userShipName, value);
        }

        #endregion Public Properties

        #region Public Methods
        // RemoveColonizationDepot moved to GameStateService_Colonization.cs
        public void BatchUpdate(Action updateAction)
        {
            using (BeginUpdate())
            {
                updateAction?.Invoke();
            }
        }

        // DebugCarrierJumpState moved to GameStateService_CarrierJump.cs

        // SyncCarrierCargoState moved to GameStateService_Cargo.cs

        // DebugCargoTransferState moved to GameStateService_Cargo.cs

        // DebugJournalPosition moved to GameStateService_Journal.cs

        // ForceProcessRecentCargoEvents moved to GameStateService_Cargo.cs

        // ForceProcessRecentJournalEvents removed - now handled by ScanForPendingCarrierJump in partial class

        public void ForceRefreshColonizationData()
        {
            Log.Information("?? Force refreshing colonization data from journal");

            Task.Run(async () =>
            {
                try
                {
                    await ProcessJournalAsync();
                    Log.Information("?? Force refresh completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "?? Error during force refresh");
                }
            });
        }

        // GetActiveColonizationDepots moved to GameStateService_Colonization.cs

        // EnsureCarrierCargoTrackingInitialized moved to GameStateService_Cargo.cs

        // InitializeCargoFromSavedData moved to GameStateService_Cargo.cs

        // ProcessJournalAsync moved to GameStateService_Journal.cs

        // PruneCompletedRouteSystems moved to GameStateService_Route.cs

        // PublishCurrentStateToMqtt moved to GameStateService_Mqtt.cs

        // RefreshMqttSettingsAsync moved to GameStateService_Mqtt.cs

        // ResetFleetCarrierJumpState removed - now handled by CarrierJumpState class

        public void ResetRouteActivity()
        {
            _routeWasActive = false;
        }

        public void SelectColonizationDepot(long marketId)
        {
            if (_colonizationDepots.ContainsKey(marketId))
            {
                SelectedDepotMarketId = marketId;
            }
        }
        // In GameStateService.cs
        // UpdateCarrierCargoItem moved to GameStateService_Cargo.cs

        // Only one SynchronizeCarrierCargoState method should exist
        // SynchronizeCarrierCargoState moved to GameStateService_Cargo.cs

        /// <summary>
        /// Gets the original game quantity for an item before manual changes
        /// </summary>
        // GetOriginalGameValueForItem moved to GameStateService_Cargo.cs
        /// <summary>
        /// Cleans up duplicate cargo entries caused by case sensitivity issues
        /// </summary>
        // CleanupDuplicateCargoEntries moved to GameStateService_Cargo.cs

        /// <summary>
        /// Manual fix for specific cargo discrepancies - useful for debugging
        /// </summary>
        // FixCarrierCargoItem moved to GameStateService_Cargo.cs

        /// <summary>
        /// Forces a refresh of both ship and carrier cargo states
        /// </summary>
        // RefreshCargoStates moved to GameStateService_Cargo.cs

        /// <summary>
        /// Analyzes recent cargo transfer events to understand discrepancies
        /// </summary>
        // AnalyzeRecentCargoTransfers moved to GameStateService_Cargo.cs

        /// <summary>
        /// Processes game file updates while preserving manual changes
        /// </summary>
        // ProcessCarrierCargoGameUpdate moved to GameStateService_Cargo.cs

        /// <summary>
        /// Saves manual changes to persistent storage
        /// </summary>
        // SaveManualCarrierCargoChanges moved to GameStateService_Cargo.cs

        /// <summary>
        /// Loads manual changes from persistent storage
        /// </summary>
        // LoadManualCarrierCargoChanges moved to GameStateService_Cargo.cs

        /// <summary>
        /// Clears expired manual changes
        /// </summary>
        // ClearExpiredManualCargoChanges moved to GameStateService_Cargo.cs
        // UpdateColonizationDepot moved to GameStateService_Colonization.cs
        public void UpdateLoadout(LoadoutJson loadout)
        {
            CurrentLoadout = loadout;
        }

        #endregion Public Methods

        #region Protected Methods

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_isUpdating)
            {
                // Queue notification for later
                _pendingNotifications.Add(propertyName);
            }
            else
            {
                Log.Debug("[OnPropertyChanged] {PropertyName}", propertyName);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;

            if (propertyName == nameof(CurrentStatus) && value is StatusJson status)
            {
                var activeFlags = Enum.GetValues(typeof(Flag))
                    .Cast<Flag>()
                    .Where(f => f != Flag.None && status.Flags.HasFlag(f))
                    .Select(f => f.ToString())
                    .ToList();
                Log.Debug("Status changed: Flags={Flags}, Raw=0x{Raw:X8}", string.Join(", ", activeFlags), (uint)status.Flags);
            }
            else if (propertyName == nameof(CarrierCargo) && value is Dictionary<string, int> cargo)
            {
                Log.Debug("Carrier cargo changed: {Count} items", cargo.Count);
            }
            else if (propertyName == nameof(StatusJson.Flags) || propertyName == "Flags")
            {
                var flagsValue = value is Flag flag ? flag : default;
                var activeFlags = Enum.GetValues(typeof(Flag))
                    .Cast<Flag>()
                    .Where(f => f != Flag.None && flagsValue.HasFlag(f))
                    .Select(f => f.ToString())
                    .ToList();
                Log.Debug("Flags changed: {Flags}, Raw=0x{Raw:X8}", string.Join(", ", activeFlags), (uint)flagsValue);
            }
            else
            {
                Log.Debug("SetProperty: {Property}", propertyName);
            }

            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion Protected Methods

        #region Private Methods

        // BackpacksEqual moved to GameStateService_Utils.cs

        /// <summary>
        /// Begins a batch update operation that defers property change notifications
        /// </summary>
        private IDisposable BeginUpdate()
        {
            _isUpdating = true;
            return new UpdateScope(this);
        }

        // UpdateScope nested class moved to partial file Core/GameStateService.UpdateScope.cs

        // DeserializeJsonFile moved to GameStateService_Utils.cs

        private void EnsureDevelopmentFilesExist(string devPath)
        {
            // Create minimal versions of required files if they don't exist
            string[] requiredFiles = {
        "Status.json",
        "NavRoute.json",
        "Cargo.json",
        "Backpack.json",
        "FCMaterials.json"
    };

            foreach (var file in requiredFiles)
            {
                string filePath = Path.Combine(devPath, file);
                if (!File.Exists(filePath))
                {

                    // Create an empty file with minimal valid JSON structure
                    File.WriteAllText(filePath, "{}");
                    Log.Information("Created empty development file: {File}", filePath);
                }
            }

            // Ensure at least one journal file exists
            string journalPath = Path.Combine(devPath, "Journal.log");
            if (!Directory.GetFiles(devPath, "Journal.*.log").Any())
            {
                File.WriteAllText(journalPath, "");
                Log.Information("Created empty development journal: {File}", journalPath);
            }
        }

        // EnsureHyperspaceTimeout moved to GameStateService_CarrierJump.cs

        // FCMaterialItemsEqual moved to GameStateService_Utils.cs

        /// <summary>
        /// Updates ship cargo to reflect transfers to/from carrier.
        /// Elite: Dangerous doesn't immediately update Cargo.json during transfers,
        /// so we need to simulate the changes for accurate UI display.
        /// </summary>
        // UpdateShipCargoFromTransfers moved to GameStateService_Cargo.cs

        // InferClassAndRatingFromItem moved to GameStateService_Utils.cs

        // InitializeMqttAsync moved to GameStateService_Mqtt.cs

        // InventoriesEqual moved to GameStateService_Utils.cs

        // ItemListsEqual moved to GameStateService_Utils.cs

        // Helper methods for comparing complex objects
        // JsonEquals moved to GameStateService_Utils.cs

        private void LoadAllData()
        {
            // Wrap this in a try-catch to ensure we don't crash the application
            try
            {
                Log.Information("GameStateService: Beginning initial load of all data");

                Task statusTask = Task.Run(() => LoadStatusData());
                Task routeTask = Task.Run(() => LoadNavRouteData());
                Task cargoTask = Task.Run(() => LoadCargoData());
                Task backpackTask = Task.Run(() => LoadBackpackData());
                Task materialsTask = Task.Run(() => LoadMaterialsData());
                Task loadoutTask = Task.Run(() => LoadLoadoutData());

                Task.WaitAll(statusTask, routeTask, cargoTask, backpackTask, materialsTask, loadoutTask);
                
                // CRITICAL FIX: Initialize carrier cargo tracking at the same time as ship cargo
                // This ensures carrier cargo works exactly like ship cargo from startup
                LoadCarrierCargoFromDisk();
                _carrierCargoTracker.Initialize(_carrierCargo);
                _cargoTrackingInitialized = true; // Enable tracking immediately
                Log.Information("?? CARRIER CARGO: Initialized alongside ship cargo - tracking enabled with {Count} items", _carrierCargo.Count);
                
                LoadPersistedColonizationData();
                latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                LoadRouteProgress();
                
                // CRITICAL FIX: Initialize carrier cargo tracking immediately alongside ship cargo
                // This ensures carrier cargo tracking works the same as ship cargo from the start
                LoadCarrierCargoFromDisk();
                _carrierCargoTracker.Initialize(_carrierCargo);
                _cargoTrackingInitialized = true; // Enable tracking BEFORE journal processing
                Log.Information("?? CARRIER CARGO: Initialized tracking alongside ship cargo - {Count} items loaded", _carrierCargo.Count);
                LoadCarrierCargoFromDisk(); // ? Add this near LoadRouteProgress();
                
                // CRITICAL: Set cargo tracking as initialized BEFORE processing journal
                // This allows current day's events to be processed during startup
                _cargoTrackingInitialized = true;
                Log.Information("? Cargo tracking initialized - ready to process journal events");

                Task.Run(async () => await ProcessJournalAsync()).Wait();
                
                // Log cargo tracking state after journal processing
                Log.Information("After journal processing: _cargoTrackingInitialized = {Initialized}", _cargoTrackingInitialized);
                if (CurrentColonization != null)
                {
                    Log.Information("Colonization data found during initial load: Progress={Progress:P2}, Resources={Count}",
                        CurrentColonization.ConstructionProgress,
                        CurrentColonization.ResourcesRequired?.Count ?? 0);
                }
                else
                {
                    Log.Warning("No colonization data found during initial load");
                }
                // Explicitly notify properties that are important for initialization
                OnPropertyChanged(nameof(CurrentLoadout));
                OnPropertyChanged(nameof(CurrentCargo));
                OnPropertyChanged(nameof(CurrentRoute));
                OnPropertyChanged(nameof(CommanderName));
                OnPropertyChanged(nameof(ShipName));
                OnPropertyChanged(nameof(CurrentSystem));
                OnPropertyChanged(nameof(Balance));
                OnPropertyChanged(nameof(CurrentStatus));

                // Mark initialization as complete
                _firstLoadCompleted = true;
                Log.Information("? GameStateService: First load completed");
                
                // CRITICAL: Synchronize carrier cargo state after loading
                // This ensures the tracker and gamestate cargo are aligned for the first transfers
                SyncCarrierCargoState();
                
                // Check for stale carrier jump states
                _carrierJumpState?.Reset();

                // Explicitly notify key jump-related properties
                OnPropertyChanged(nameof(FleetCarrierJumpInProgress));
                OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                OnPropertyChanged(nameof(JumpArrived));
                // --- ADD THESE LINES TO FIX COUNTDOWN NOT UPDATING ON RESTART ---
                OnPropertyChanged(nameof(CarrierJumpScheduledTime));
                OnPropertyChanged(nameof(FleetCarrierJumpTime));
                OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
                OnPropertyChanged(nameof(ShowCarrierJumpCountdown));
                // --- END FIX ---
                LoadCarrierCargoFromDisk();
                LoadPersistedColonizationData();
                // Note: cargo tracking was already initialized before journal processing
                // Notify subscribers
                FirstLoadCompletedEvent?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GameStateService: Error during initial data load");
                throw; // Re-throw to ensure the application handles this error
            }
        }

        private bool LoadBackpackData()
        {
            var oldBackpack = CurrentBackpack;
            CurrentBackpack = LoadJsonFile<BackpackJson>("Backpack.json", CurrentBackpack, BackpacksEqual);

            return !ReferenceEquals(oldBackpack, CurrentBackpack);
        }

        private bool LoadCargoData()
        {
            try
            {
                Log.Debug("GameStateService: Loading cargo data");
                var oldCargo = CurrentCargo;

                // Use the optimized loading method
                CurrentCargo = LoadJsonFile<CargoJson>("Cargo.json", CurrentCargo, (c1, c2) =>
                {
                    return c1 != null && c2 != null && InventoriesEqual(c1.Inventory, c2.Inventory);
                });

                bool hasChanged = !ReferenceEquals(oldCargo, CurrentCargo);

                // Initialize empty inventory if needed
                if (CurrentCargo?.Inventory == null)
                {
                    CurrentCargo = CurrentCargo ?? new CargoJson();
                    CurrentCargo.Inventory = new List<CargoJson.CargoItem>();
                    Log.Warning("GameStateService: Cargo.json loaded but Inventory is null - initialized empty list");
                }

                // Log detailed info on change
                if (hasChanged)
                {
                    Log.Information("GameStateService: Cargo inventory changed - now {Count} items",
                        CurrentCargo.Inventory.Count);

                    foreach (var item in CurrentCargo.Inventory)
                    {
                        Log.Debug("  - {Name}: {Count}", item.Name, item.Count);
                    }
                }

                return hasChanged;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading cargo data");
                return false;
            }
        }

        private void LoadCarrierCargoFromDisk()
        {
            try
            {
                Log.Information("LoadCarrierCargoFromDisk via service, initialized: {Initialized}", _cargoTrackingInitialized);
                _carrierCargo = _carrierCargoService.Load();
                _carrierCargoService.Initialize(_carrierCargo);
                UpdateCurrentCarrierCargoFromDictionary();
                Log.Information("Loaded {Count} carrier cargo items from disk", _carrierCargo.Count);

                LoadManualCarrierCargoChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load carrier cargo from disk");
                _carrierCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // Improved file loading method for GameStateService.cs
        private T LoadJsonFile<T>(string fileName, T currentValue, Func<T, T, bool> comparer = null) where T : class, new()
        {
            string filePath = Path.Combine(gamePath, fileName);

            try
            {
                if (!File.Exists(filePath))
                {
                    Log.Debug("File not found: {FilePath}", filePath);
                    return currentValue;
                }

                // Use FileShare.ReadWrite to safely access files that might be written by the game
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length == 0)
                {
                    Log.Debug("File is empty: {FilePath}", filePath);
                    return currentValue;
                }

                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Debug("File contains no data: {FilePath}", filePath);
                    return currentValue;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var newValue = JsonSerializer.Deserialize<T>(json, options) ?? new T();

                // If no custom comparer provided, just check if values are different
                bool hasChanged = comparer != null
                    ? !comparer(currentValue, newValue)
                    : !JsonEquals(currentValue, newValue);

                if (hasChanged)
                {
                    Log.Debug("File {FileName} has changed, updating data", fileName);
                    return newValue;
                }

                return currentValue;
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "IOException reading {FilePath}", filePath);
                return currentValue;
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "JSON parsing error in {FilePath}", filePath);
                return currentValue;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error reading {FilePath}", filePath);
                return currentValue;
            }
        }

        private bool LoadLoadoutData()
        {
            var newLoadout = DeserializeJsonFile<LoadoutJson>(Path.Combine(gamePath, "ModulesInfo.json"));

            if (newLoadout == null)
            {
                Log.Warning("?? ModulesInfo.json was null on startup");
                return false;
            }

            Log.Information("? Loaded ModulesInfo.json with {Count} modules", newLoadout.Modules?.Count ?? 0);

            if (CurrentLoadout == null || !JsonEquals(CurrentLoadout, newLoadout))
            {
                Log.Information("? Setting CurrentLoadout...");
                CurrentLoadout = newLoadout;
                LoadoutUpdated?.Invoke();
                return true;
            }

            return false;
        }

        // Replace the LoadCargoData method in GameStateService.cs with this improved version:
        private bool LoadMaterialsData()
        {
            var oldMaterials = CurrentMaterials;
            CurrentMaterials = LoadJsonFile<FCMaterialsJson>("FCMaterials.json", CurrentMaterials, MaterialsEqual);

            return !ReferenceEquals(oldMaterials, CurrentMaterials);
        }

        private bool LoadNavRouteData()
        {
            var oldRoute = CurrentRoute;
            CurrentRoute = LoadJsonFile<NavRouteJson>("NavRoute.json", CurrentRoute, (r1, r2) =>
            {
                // Consider routes equal if they have the same systems in the same order
                if (r1?.Route == null && r2?.Route == null) return true;
                if (r1?.Route == null || r2?.Route == null) return false;
                if (r1.Route.Count != r2.Route.Count) return false;

                for (int i = 0; i < r1.Route.Count; i++)
                {
                    if (!string.Equals(r1.Route[i].StarSystem, r2.Route[i].StarSystem,
                        StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            });

            // Special handling for empty routes
            if (CurrentRoute?.Route == null || CurrentRoute.Route.Count == 0)
            {
                if (oldRoute != null && (oldRoute.Route?.Count ?? 0) > 0)
                {
                    CurrentRoute = null;
                    RemainingJumps = null;
                    return true;
                }
                return false;
            }

            // Prune jumps already completed
            if (!ReferenceEquals(oldRoute, CurrentRoute))
            {
                PruneCompletedRouteSystems();
                return true;
            }

            return false;
        }

        // LoadPersistedColonizationData moved to GameStateService_Colonization.cs

        // LoadRouteProgress moved to GameStateService_Route.cs

        // LoadStatusData moved to GameStateService_Status.cs

        private bool MaterialsEqual(FCMaterialsJson mat1, FCMaterialsJson mat2)
        {
            if (mat1 == null && mat2 == null) return true;
            if (mat1 == null || mat2 == null) return false;

            return FCMaterialItemsEqual(mat1.Items, mat2.Items);
        }

        private void ProcessDockingEvent(string eventType, JsonElement root)
        {
            var previousDockingState = IsDocking; // Track the previous state

            switch (eventType)
            {
                case "DockingRequested":
                    _currentDockingState = DockingState.DockingRequested;
                    IsDocking = true;
                    Log.Debug("Docking requested - IsDocking set to true");
                    break;

                case "DockingGranted":
                    _currentDockingState = DockingState.DockingGranted;
                    IsDocking = true;
                    Log.Debug("Docking granted - IsDocking set to true");
                    break;

                case "Docked":
                    _currentDockingState = DockingState.Docked;
                    IsDocking = false;
                    Log.Debug("Docked - IsDocking set to false (was: {Previous})", previousDockingState);
                    break;

                case "DockingCancelled":
                case "DockingDenied":
                case "DockingTimeout":
                    _currentDockingState = DockingState.NotDocking;
                    IsDocking = false;
                    Log.Debug("{EventType} - IsDocking set to false", eventType);
                    break;

                case "Undocked":
                    _currentDockingState = DockingState.NotDocking;
                    IsDocking = false;
                    Log.Debug("Undocked - IsDocking set to false");
                    break;
            }

            // Force an immediate MQTT update with explicit docking state
            if (previousDockingState != IsDocking || eventType == "Docked")
            {
                Log.Debug("Docking state changed from {Previous} to {Current} - forcing MQTT update",
                    previousDockingState, IsDocking);
                // Ensure CurrentStatus is up-to-date before publishing
                var status = CurrentStatus;
                if (status != null)
                {
                    Task.Run(async () =>
                    {
                        await MqttService.Instance.PublishFlagStatesAsync(status, IsDocking, forcePublish: true);
                    });
                }
                else
                {
                    Log.Warning("CurrentStatus is null when trying to publish MQTT docking state");
                }
            }
            else
            {
                // Always publish the current state to MQTT for all docking events (retain message)
                var status = CurrentStatus;
                if (status != null)
                {
                    Task.Run(async () =>
                    {
                        await MqttService.Instance.PublishFlagStatesAsync(status, IsDocking, forcePublish: true);
                    });
                }
            }
        }

        // ProcessLegalStateEvent moved to GameStateService_Status.cs

        // PublishStatusToMqtt moved to GameStateService_Status.cs

        // SaveAllColonizationData moved to GameStateService_Colonization.cs

        private void SaveCarrierCargoToDisk()
        {
            try
            {
                _carrierCargoService.Save(_carrierCargo);
                Log.Debug("Saved {Count} carrier cargo items to disk via service", _carrierCargo.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save carrier cargo to disk");
            }
        }
        // SaveColonizationData moved to GameStateService_Colonization.cs

        // SaveRouteProgress moved to GameStateService_Route.cs

        // ScanJournalForPendingCarrierJump moved to GameStateService_Journal.cs

        /// <summary>
        /// Sends all pending property change notifications at once
        /// </summary>
        private void SendPendingNotifications()
        {
            if (_pendingNotifications.Count == 0)
                return;

            foreach (var prop in _pendingNotifications)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
            }

            _pendingNotifications.Clear();
        }

        // SetupFileWatcher moved to GameStateService_FileWatchers.cs

        // SetupJournalWatcher moved to GameStateService_FileWatchers.cs

        private void UpdateCarrierCargo(JsonElement root)
        {
            if (!_cargoTrackingInitialized) return;
            if (root.TryGetProperty("Commodity", out var commodityProp) &&
                root.TryGetProperty("Count", out var countProp))
            {
                string name = commodityProp.GetString();
                int count = countProp.GetInt32();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    // Only update if this is a full refresh (e.g. CarrierTradeOrder)
                    // Not a transfer
                    if (count > 0)
                    {
                        _carrierCargo[name] = count;
                    }
                    else if (_carrierCargo.ContainsKey(name))
                    {
                        _carrierCargo.Remove(name);
                    }

                    Log.Information("?? UpdateCarrierCargo: {Name} = {Count}", name, count);
                    OnPropertyChanged(nameof(CarrierCargo));
                }
            }
        }

        // In GameStateService.cs - Update UpdateCurrentCarrierCargoFromDictionary
        private void UpdateCurrentCarrierCargoFromDictionary()
        {
            try
            {
                // Standardize the dictionary to use internal names only
                var standardizedCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in CarrierCargo)
                {
                    // Always treat the key as an internal name
                    string internalName = pair.Key;
                    standardizedCargo[internalName] = pair.Value;
                }

                // Update our dictionary to the standardized version (internal names only)
                CarrierCargo = standardizedCargo;

                // Now create the UI list with display names
                var items = new List<CarrierCargoItem>();

                foreach (var pair in CarrierCargo.Where(kv => kv.Value > 0))
                {
                    items.Add(new CarrierCargoItem
                    {
                        Name = CommodityMapper.GetDisplayName(pair.Key), // display name for UI
                        Quantity = pair.Value
                    });
                }

                var sortedItems = items.OrderByDescending(i => i.Quantity).ToList();
                CurrentCarrierCargo = sortedItems;

#if dev
                Log.Debug("UpdateCurrentCarrierCargoFromDictionary: Updated with {Count} items (internal names only)",
                    sortedItems.Count);
#endif
            }
            catch (Exception ex)
            {
#if dev
                Log.Error(ex, "Error updating CurrentCarrierCargo from dictionary");
#endif
            }
        }

        /// <summary>
        /// Called by SummaryViewModel when the carrier jump countdown reaches zero, to trigger overlay display.
        /// </summary>
     
        #endregion Private Methods
    }
}


