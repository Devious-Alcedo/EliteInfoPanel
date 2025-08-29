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

namespace EliteInfoPanel.Core
{
    public class GameStateService : INotifyPropertyChanged
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
        private string _carrierJumpDestinationBody;
        private string _carrierJumpDestinationSystem;
        private DateTime? _carrierJumpScheduledTime;
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
        private bool _fleetCarrierJumpInProgress;
        private DateTime? _fleetCarrierJumpTime;
        private string _hyperspaceDestination;
        private string _hyperspaceStarClass;
        private CancellationTokenSource _hyperspaceTimeoutCts;
        private bool _isCarrierJumping = false;
        private bool _isDocking;
        private bool _isHyperspaceJumping;
        private bool _isInHyperspace = false;
        private bool _isInitializing = true;
        private bool _isOnFleetCarrier;
        private bool _isRouteLoaded = false;
        private bool _isUpdating = false;
        private bool _jumpArrived;
        // Add this field to track changes
        private int _lastCarrierJumpCountdown = -1;
        private readonly Dictionary<string, ManualCargoChange> _manualCarrierCargoChanges = new(StringComparer.OrdinalIgnoreCase);
        private string ManualCarrierCargoFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EliteCompanion", "ManualCarrierCargo.json");
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
        private List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private string gamePath;
        private long lastJournalPosition = 0;
        private string latestJournalPath;

        #endregion Private Fields

        #region Public Constructors

        public GameStateService(string path)
        {
            var settings = SettingsManager.Load();
            if (settings.DevelopmentMode)
            {
                Log.Information("🔧 DEVELOPMENT MODE ENABLED - Using simulated journal entries");
                // Get the development path (dev folder in the same location as real game files)
                gamePath = EliteDangerousPaths.GetSavedGamesPath(true);

                // Ensure crucial files exist in dev folder
                EnsureDevelopmentFilesExist(gamePath);

                Log.Information("Using development journal path: {Path}", gamePath);
            }
            else
            {
                gamePath = path;
            }

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

            // Scan journal for pending carrier jump
            ScanJournalForPendingCarrierJump();
            Task.Run(InitializeMqttAsync);
            if (CurrentStatus != null)
            {
                Task.Run(() => PublishStatusToMqtt(CurrentStatus));
                Log.Information("✅ Initial flag state pushed to MQTT on startup");
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
                OnPropertyChanged(nameof(CarrierCargo)); // ← ADD THIS
            }
        }

        // Add this field to track the special condition
        public int CarrierJumpCountdownSeconds
        {
            get
            {
                if (CarrierJumpScheduledTime.HasValue)
                {
                    var timeLeft = CarrierJumpScheduledTime.Value.ToLocalTime() - DateTime.Now;
                    int result = (int)Math.Max(0, timeLeft.TotalSeconds);
                    return result;
                }
                return 0;
            }
        }

        public string CarrierJumpDestinationBody
        {
            get => _carrierJumpDestinationBody;
            private set => SetProperty(ref _carrierJumpDestinationBody, value);
        }

        public string CarrierJumpDestinationSystem
        {
            get => _carrierJumpDestinationSystem;
            private set => SetProperty(ref _carrierJumpDestinationSystem, value);
        }

        public DateTime? CarrierJumpScheduledTime
        {
            get => _carrierJumpScheduledTime;
            private set => SetProperty(ref _carrierJumpScheduledTime, value);
        }

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
        public bool FleetCarrierJumpInProgress
        {
            get => _fleetCarrierJumpInProgress;
            private set
            {
                Log.Information("📡 FleetCarrierJumpInProgress changed to {0} - Stack trace: {1}",
                    value, Environment.StackTrace);
                if (SetProperty(ref _fleetCarrierJumpInProgress, value))
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // notify
            }
        }

        public DateTime? FleetCarrierJumpTime
        {
            get => _fleetCarrierJumpTime;
            private set => SetProperty(ref _fleetCarrierJumpTime, value);
        }

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

        public bool JumpArrived
        {
            get => _jumpArrived;
            set
            {
                if (SetProperty(ref _jumpArrived, value))
                {
                    Log.Debug("JumpArrived changed to {0}, notifying ShowCarrierJumpOverlay", value);
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                }
            }
        }

        public TimeSpan? JumpCountdown => FleetCarrierJumpTime.HasValue ?
                    FleetCarrierJumpTime.Value.ToLocalTime() - DateTime.Now : null;

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

        // Add this to GameStateService.cs
        // In ShowCarrierJumpOverlay
        public bool ShowCarrierJumpOverlay
        {
            get
            {
                bool onCarrier = IsOnFleetCarrier;
                bool jumpInProgress = FleetCarrierJumpInProgress;
                int countdown = CarrierJumpCountdownSeconds;
                bool jumpArrived = JumpArrived;

                bool shouldShow = onCarrier && jumpInProgress && countdown <= 0 && !jumpArrived;

                // Only log when there's ACTUAL carrier jump activity (not just being on carrier)
                if (jumpInProgress || (jumpArrived && CarrierJumpScheduledTime.HasValue))
                {
                    Log.Debug("🚀 ShowCarrierJumpOverlay: OnCarrier={OnCarrier}, JumpInProgress={InProgress}, Countdown={Countdown}, JumpArrived={Arrived} → {Result}",
                        onCarrier, jumpInProgress, countdown, jumpArrived, shouldShow);
                }

                return shouldShow;
            }
        }

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

        public void BatchUpdate(Action updateAction)
        {
            using (BeginUpdate())
            {
                updateAction?.Invoke();
            }
        }

        public void DebugCargoTransferState()
        {
            try
            {
                Log.Information("=== CARGO TRANSFER DEBUG INFO ===");
                Log.Information("Cargo tracking initialized: {Initialized}", _cargoTrackingInitialized);
                Log.Information("Current carrier cargo count: {Count}", _carrierCargo.Count);
                Log.Information("CarrierCargoTracker cargo count: {TrackerCount}", _carrierCargoTracker.Cargo.Count);
                Log.Information("Latest journal path: {Path}", latestJournalPath);
                Log.Information("Journal position: {Position}", lastJournalPosition);
                
                // Show current cargo state
                Log.Information("Current carrier cargo contents:");
                foreach (var item in _carrierCargo)
                {
                    Log.Information("  GameState: {Name}: {Quantity}", item.Key, item.Value);
                }
                
                Log.Information("CarrierCargoTracker contents:");
                foreach (var item in _carrierCargoTracker.Cargo)
                {
                    Log.Information("  Tracker: {Name}: {Quantity}", item.Key, item.Value);
                }
                
                // Check for differences
                var differences = new List<string>();
                var allKeys = _carrierCargo.Keys.Union(_carrierCargoTracker.Cargo.Keys).ToList();
                foreach (var key in allKeys)
                {
                int gameStateQty = _carrierCargo.TryGetValue(key, out int gsQty) ? gsQty : 0;
                int trackerQty = _carrierCargoTracker.Cargo.TryGetValue(key, out int tQty) ? tQty : 0;
                
                if (gameStateQty != trackerQty)
                {
                differences.Add($"{key}: GameState={gameStateQty}, Tracker={trackerQty}");
                }
                }
                
                if (differences.Any())
                {
                Log.Warning("FOUND CARGO DISCREPANCIES:");
                foreach (var diff in differences)
                {
                Log.Warning("  {Difference}", diff);
                }
                    
                    Log.Information("Attempting to fix discrepancies...");
                    SynchronizeCarrierCargoState();
                }
                else
                    {
                        Log.Information("✅ GameState and CarrierCargoTracker are in sync");
                    }
                
                // Check recent journal entries for CargoTransfer
                if (!string.IsNullOrEmpty(latestJournalPath) && File.Exists(latestJournalPath))
                {
                    var fileInfo = new FileInfo(latestJournalPath);
                    using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    
                    // Read the last 5KB to look for recent CargoTransfer events
                    long startPos = Math.Max(0, fileInfo.Length - 5120);
                    fs.Seek(startPos, SeekOrigin.Begin);
                    
                    using var sr = new StreamReader(fs);
                    var cargoTransferEvents = new List<string>();
                    
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line) && line.Contains("CargoTransfer"))
                        {
                            cargoTransferEvents.Add(line);
                        }
                    }
                    
                    Log.Information("Found {Count} recent CargoTransfer events:", cargoTransferEvents.Count);
                    foreach (var evt in cargoTransferEvents.TakeLast(3))
                    {
                        Log.Information("  {Event}", evt);
                    }
                }
                
                Log.Information("=== END CARGO TRANSFER DEBUG ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in cargo transfer debug");
            }
        }

        public void DebugJournalPosition()
        {
            try
            {
                if (string.IsNullOrEmpty(latestJournalPath) || !File.Exists(latestJournalPath))
                {
                    Log.Error("📖 DEBUG: No valid journal file");
                    return;
                }

                var fileInfo = new FileInfo(latestJournalPath);
                Log.Information("📖 DEBUG Journal State:");
                Log.Information("  File: {File}", Path.GetFileName(latestJournalPath));
                Log.Information("  File Size: {Size} bytes", fileInfo.Length);
                Log.Information("  Current Position: {Position} bytes", lastJournalPosition);
                Log.Information("  Last Modified: {LastWrite}", fileInfo.LastWriteTime);
                Log.Information("  Bytes Remaining: {Remaining}", fileInfo.Length - lastJournalPosition);

                // Check for recent CarrierJump events
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Read the last 10KB of the file to check for recent events
                long startPos = Math.Max(0, fileInfo.Length - 10240);
                fs.Seek(startPos, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                var recentLines = new List<string>();
                var carrierEvents = new List<string>();

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        recentLines.Add(line);
                        if (line.Contains("CarrierJump") || line.Contains("Carrier"))
                        {
                            carrierEvents.Add(line);
                        }
                    }
                }

                Log.Information("📖 DEBUG: Last {LineCount} lines in journal, {CarrierCount} carrier-related events",
                    recentLines.Count, carrierEvents.Count);

                foreach (var carrierEvent in carrierEvents.TakeLast(3))
                {
                    Log.Information("📖 CARRIER EVENT: {Event}", carrierEvent);
                }

                // Check if we missed the CarrierJump event
                bool missedCarrierJump = carrierEvents.Any(e => e.Contains("\"event\":\"CarrierJump\""));
                if (missedCarrierJump)
                {
                    Log.Warning("📖 ⚠️  FOUND UNPROCESSED CarrierJump EVENT - journal position may be incorrect!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "📖 DEBUG: Error checking journal position");
            }
        }

        public void ForceProcessRecentCargoEvents()
        {
            try
            {
                Log.Information("🔄 Force processing recent cargo events");
                
                if (string.IsNullOrEmpty(latestJournalPath) || !File.Exists(latestJournalPath))
                {
                    Log.Warning("No journal file available for processing");
                    return;
                }
                
                // Ensure cargo tracking is initialized
                if (!_cargoTrackingInitialized)
                {
                    Log.Information("Initializing cargo tracking before processing events");
                    _cargoTrackingInitialized = true;
                }
                
                var fileInfo = new FileInfo(latestJournalPath);
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                
                // Read the last 10KB to look for recent cargo events
                long startPos = Math.Max(0, fileInfo.Length - 10240);
                fs.Seek(startPos, SeekOrigin.Begin);
                
                using var sr = new StreamReader(fs);
                var cargoEvents = new List<string>();
                
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line) && 
                        (line.Contains("CargoTransfer") || line.Contains("CargoDepot") || 
                         line.Contains("MarketBuy") || line.Contains("MarketSell") || 
                         line.Contains("CarrierTradeOrder")))
                    {
                        cargoEvents.Add(line);
                    }
                }
                
                Log.Information("Found {Count} recent cargo events to process", cargoEvents.Count);
                
                // Process each event
                int processedCount = 0;
                foreach (var eventLine in cargoEvents)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(eventLine);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("event", out var eventProp))
                        {
                            string eventType = eventProp.GetString();
                            bool shouldProcess = false;
                            
                            switch (eventType)
                            {
                                case "CargoTransfer":
                                case "CargoDepot":
                                case "CarrierTradeOrder":
                                    shouldProcess = true;
                                    break;
                                    
                                case "MarketBuy":
                                    // Only process if buying FROM carrier
                                    shouldProcess = root.TryGetProperty("BuyFromFleetCarrier", out var boughtFromCarrierProp) && 
                                                  boughtFromCarrierProp.GetBoolean();
                                    break;
                                    
                                case "MarketSell":
                                    // Only process if selling TO carrier
                                    shouldProcess = root.TryGetProperty("SellToFleetCarrier", out var soldToCarrierProp) && 
                                                  soldToCarrierProp.GetBoolean();
                                    break;
                            }
                            
                            if (shouldProcess)
                            {
                                Log.Information("Force processing: {Event}", eventType);
                                _carrierCargoTracker.Process(root);
                                processedCount++;
                            }
                            else
                            {
                                Log.Debug("Skipping {Event} - not carrier-related", eventType);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error processing cargo event: {Event}", eventLine);
                    }
                }
                
                if (processedCount > 0)
                {
                    // Update our cargo state
                    using (BeginUpdate())
                    {
                        _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                        UpdateCurrentCarrierCargoFromDictionary();
                        SaveCarrierCargoToDisk();
                    }
                    
                    Log.Information("Force processed {Count} cargo events, carrier now has {Items} items",
                        processedCount, _carrierCargo.Count);
                    
                    // Log updated quantities
                    foreach (var item in _carrierCargo.Take(10))
                    {
                        Log.Information("  {Name}: {Quantity}", item.Key, item.Value);
                    }
                }
                else
                {
                    Log.Information("No cargo events needed processing");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error force processing recent cargo events");
            }
        }

        public void ForceRefreshColonizationData()
        {
            Log.Information("🔄 Force refreshing colonization data from journal");

            Task.Run(async () =>
            {
                try
                {
                    await ProcessJournalAsync();
                    Log.Information("🔄 Force refresh completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "🔄 Error during force refresh");
                }
            });
        }

        public List<ColonizationData> GetActiveColonizationDepots()
        {
            return _colonizationDepots.Values
                .Where(d => !d.ConstructionComplete && !d.ConstructionFailed)
                .OrderBy(d => d.MarketID)
                .ToList();
        }

        public void InitializeCargoFromSavedData(Dictionary<string, int> savedCargo)
        {
            using (BeginUpdate())
            {
                // Clear any current cargo data
                _carrierCargo.Clear();

                // Copy the saved cargo data
                foreach (var item in savedCargo)
                {
                    _carrierCargo[item.Key] = item.Value;
                }

                // Initialize the tracker with our saved state
                _carrierCargoTracker.Initialize(savedCargo);
                
                // Normalize cargo keys to ensure consistency
                _carrierCargoTracker.NormalizeCargoKeys();
                
                // Update our cargo from the normalized tracker state
                _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);

                // Update the UI-friendly list
                UpdateCurrentCarrierCargoFromDictionary();

                // NOW enable tracking of new cargo events
                _cargoTrackingInitialized = true;

                // Notify of change
                OnPropertyChanged(nameof(CarrierCargo));
                OnPropertyChanged(nameof(CurrentCarrierCargo));

                Log.Information("Carrier cargo initialized from saved data with {Count} items", _carrierCargo.Count);
            }
        }

        public async Task ProcessJournalAsync()
        {
            if (string.IsNullOrEmpty(latestJournalPath))
                return;

            try
            {
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(lastJournalPosition, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                bool suppressUIUpdates = !_firstLoadCompleted; // true if this is the first pass

                // Use the batch update system to reduce property change notifications
                using (BeginUpdate())
                {
                    while (!sr.EndOfStream)
                    {
                        string line = await sr.ReadLineAsync();
                        lastJournalPosition = fs.Position;

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;

                            if (!root.TryGetProperty("event", out var eventProp))
                                continue;

                            string eventType = eventProp.GetString();
                            Log.Debug("Processing journal event: {Event}", eventType);

                            // All the existing switch cases and handling logic remains the same
                            // Full expanded and grouped switch statement with all original logic
                            switch (eventType)
                            {
                                case "Commander":
                                    if (root.TryGetProperty("Name", out var nameProperty))
                                    {
                                        CommanderName = nameProperty.GetString();
                                    }
                                    break;

                                case "Rank":
                                    if (root.TryGetProperty("Combat", out var combatProp))
                                        CombatRank = combatProp.GetInt32();

                                    if (root.TryGetProperty("Trade", out var tradeProp))
                                        TradeRank = tradeProp.GetInt32();

                                    if (root.TryGetProperty("Explore", out var exploreProp))
                                        ExplorationRank = exploreProp.GetInt32();

                                    if (root.TryGetProperty("CQC", out var cqcProp))
                                        CqcRank = cqcProp.GetInt32();

                                    if (root.TryGetProperty("Exobiologist", out var exobioProp))
                                        ExobiologistRank = exobioProp.GetInt32();

                                    if (root.TryGetProperty("Mercenary", out var mercProp))
                                        MercenaryRank = mercProp.GetInt32();

                                    break;

                                case "Promotion":
                                    if (root.TryGetProperty("Combat", out var combatPromotionProp))
                                        CombatRank = combatPromotionProp.GetInt32();

                                    if (root.TryGetProperty("Trade", out var tradePromotionProp))
                                        TradeRank = tradePromotionProp.GetInt32();

                                    if (root.TryGetProperty("Explore", out var explorePromotionProp))
                                        ExplorationRank = explorePromotionProp.GetInt32();

                                    if (root.TryGetProperty("CQC", out var cqcPromotionProp))
                                        CqcRank = cqcPromotionProp.GetInt32();

                                    if (root.TryGetProperty("Exobiologist", out var exobioPromotionProp))
                                        ExobiologistRank = exobioPromotionProp.GetInt32();

                                    if (root.TryGetProperty("Mercenary", out var mercPromotionProp))
                                        MercenaryRank = mercPromotionProp.GetInt32();

                                    break;

                                case "SetUserShipName":
                                    if (root.TryGetProperty("Ship", out var setShipTypeProperty) &&
                                        root.TryGetProperty("ShipID", out var setShipIdProperty))
                                    {
                                        string shipType = setShipTypeProperty.GetString();
                                        int shipId = setShipIdProperty.GetInt32();

                                        // Ensure we're using GetString() not just checking existence
                                        string userShipName = root.TryGetProperty("UserShipName", out var nameProp) ?
                                            nameProp.GetString() : null;

                                        string userShipId = root.TryGetProperty("UserShipId", out var idProp) ?
                                            idProp.GetString() : null;

                                        Log.Debug("Received ship name info for {Ship}: {UserShipName} [{UserShipId}]",
                                            shipType, userShipName, userShipId);

                                        ShipName = shipType;
                                        UserShipName = userShipName;
                                        UserShipId = userShipId;
                                    }
                                    break;

                                case "LoadGame":
                                    if (root.TryGetProperty("Ship", out var shipProperty))
                                    {
                                        ShipName = shipProperty.GetString();
                                    }

                                    if (root.TryGetProperty("Ship_Localised", out var shipLocalisedProperty))
                                    {
                                        ShipLocalised = shipLocalisedProperty.GetString();
                                    }

                                    // Add these lines to also load ship name and ID during LoadGame
                                    if (root.TryGetProperty("ShipName", out var shipNameProperty))
                                    {
                                        UserShipName = shipNameProperty.GetString();
                                        Log.Debug("Loaded ShipName during LoadGame: {ShipName}", UserShipName);
                                    }

                                    if (root.TryGetProperty("ShipIdent", out var shipIdentProperty))
                                    {
                                        UserShipId = shipIdentProperty.GetString();
                                        Log.Debug("Loaded ShipIdent during LoadGame: {ShipIdent}", UserShipId);
                                    }
                                    break;

                                case "ShipyardSwap":
                                    if (root.TryGetProperty("ShipType", out var shipTypeProperty))
                                    {
                                        string shipType = shipTypeProperty.GetString();
                                        string shipTypeName = root.TryGetProperty("ShipType_Localised", out var localisedProp) && !string.IsNullOrWhiteSpace(localisedProp.GetString())
                                            ? localisedProp.GetString()
                                            : ShipNameHelper.GetLocalisedName(shipType);

                                        ShipName = shipType;
                                        ShipLocalised = shipTypeName;

                                        Log.Debug("Ship changed to: {Type} ({Localised})", shipType, shipTypeName);

                                        CurrentLoadout = null;
                                        LoadLoadoutData();
                                    }
                                    break;

                                case "Loadout":
                                    var loadout = JsonSerializer.Deserialize<LoadoutJson>(line);
                                    if (loadout != null)
                                    {
                                        foreach (var module in loadout.Modules)
                                        {
                                            if (module.Class == 0 || string.IsNullOrEmpty(module.Rating))
                                            {
                                                InferClassAndRatingFromItem(module);
                                            }
                                        }
                                        if (!string.IsNullOrEmpty(loadout.ShipName))
                                        {
                                            UserShipName = loadout.ShipName;
                                            Log.Debug("Updated UserShipName from Loadout: {ShipName}", UserShipName);
                                        }

                                        if (!string.IsNullOrEmpty(loadout.ShipIdent))
                                        {
                                            UserShipId = loadout.ShipIdent;
                                            Log.Debug("Updated UserShipId from Loadout: {ShipIdent}", UserShipId);
                                        }
                                        CurrentLoadout = loadout;

                                        OnPropertyChanged(nameof(CurrentLoadout));
                                        OnPropertyChanged(nameof(CurrentStatus));
                                        LoadoutUpdated?.Invoke();
                                    }
                                    break;

                                case "Undocked":
                                    _currentDockingState = DockingState.NotDocking;
                                    IsDocking = false;
                                    CurrentStationName = null;
                                    IsOnFleetCarrier = false;
                                    
                                    // Clean up carrier jump state when undocking
                                    ResetFleetCarrierJumpState();
                                    break;

                                case "Docked":
                                    // Process the docking event first
                                    ProcessDockingEvent(eventType, root);

                                    // Then handle the rest of the Docked event
                                    if (root.TryGetProperty("Wanted", out var wantedProp) && wantedProp.GetBoolean())
                                    {
                                        LegalState = "Wanted";
                                    }
                                    else
                                    {
                                        LegalState = "Clean";
                                    }

                                    if (root.TryGetProperty("StationName", out var stationProp))
                                    {
                                        CurrentStationName = stationProp.GetString();
                                        bool isCarrier = false;
                                        if (root.TryGetProperty("StationType", out var dockStationTypeProp))
                                        {
                                            string stationType = dockStationTypeProp.GetString();
                                            isCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);
                                            Log.Debug("Docked at station: {Station}, StationType: {Type}, IsCarrier: {IsCarrier}",
                                                CurrentStationName, stationType, isCarrier);
                                        }
                                        if (isCarrier || dockStationTypeProp.ValueKind != JsonValueKind.Undefined)
                                        {
                                            IsOnFleetCarrier = isCarrier;
                                        }
                                    }
                                    break;

                                case "DockingCancelled":
                                    ProcessDockingEvent(eventType, root);
                                    Log.Debug("Docking cancelled explicitly");
                                    break;

                                case "DockingDenied":
                                    ProcessDockingEvent(eventType, root);
                                    break;

                                case "DockingTimeout":
                                    ProcessDockingEvent(eventType, root);
                                    break;

                                case "DockingGranted":
                                    Log.Debug("Docking granted by station — setting IsDocking = true");
                                    ProcessDockingEvent(eventType, root);
                                    break;

                                // In GameStateService.cs - Update the StartJump case with debug logging
                                // In GameStateService.cs - Corrected StartJump case
                                case "StartJump":
                                    if (root.TryGetProperty("JumpType", out var jumpTypeProp))
                                    {
                                        string jumpType = jumpTypeProp.GetString();
                                        Log.Information("StartJump event received - JumpType: {JumpType}", jumpType);

                                        if (jumpType == "Hyperspace")
                                        {
                                            Log.Information("Setting hyperspace jump state to TRUE");
                                            IsHyperspaceJumping = true;
                                            _isInHyperspace = true;

                                            if (root.TryGetProperty("StarClass", out var starClassProp))
                                            {
                                                HyperspaceStarClass = starClassProp.GetString();
                                            }
                                            else
                                            {
                                                HyperspaceStarClass = null;
                                            }

                                            EnsureHyperspaceTimeout();
                                        }
                                        else
                                        {
                                            Log.Information("Setting hyperspace jump state to FALSE (JumpType: {JumpType})", jumpType);
                                            IsHyperspaceJumping = false;
                                            _isInHyperspace = false;

                                            // Make sure to clear hyperspace destination and star class for supercruise jumps
                                            HyperspaceDestination = null;
                                            HyperspaceStarClass = null;
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("StartJump event received but no JumpType property found");
                                    }

                                    // Log the final state
                                    Log.Information("After StartJump: IsHyperspaceJumping={IsHyperspace}, _isInHyperspace={InHyperspace}",
                                        IsHyperspaceJumping, _isInHyperspace);
                                    break;

                                case "FSDTarget":
                                    if (root.TryGetProperty("RemainingJumpsInRoute", out var jumpsProp))
                                        RemainingJumps = jumpsProp.GetInt32();

                                    if (root.TryGetProperty("Name", out var fsdNameProp))
                                        LastFsdTargetSystem = fsdNameProp.GetString();

                                    break;

                                case "FSDJump":
                                    Log.Information("✅ Hyperspace jump completed");
                                    bool wasBatchMode = _isUpdating;
                                    if (wasBatchMode)
                                    {
                                        _isUpdating = false;
                                    }
                                    IsHyperspaceJumping = false;
                                    _isInHyperspace = false;
                                    HyperspaceDestination = null;
                                    HyperspaceStarClass = null;
                                    if (wasBatchMode)
                                    {
                                        _isUpdating = true;
                                    }
                                    if (root.TryGetProperty("StarSystem", out JsonElement systemElement))
                                    {
                                        string currentSystem = systemElement.GetString();

                                        if (!string.Equals(LastVisitedSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
                                        {
                                            LastVisitedSystem = currentSystem;
                                        }

                                        CurrentSystem = currentSystem;

                                        if (!_routeProgress.CompletedSystems.Contains(CurrentSystem))
                                        {
                                            _routeProgress.CompletedSystems.Add(CurrentSystem);
                                            _routeProgress.LastKnownSystem = CurrentSystem;
                                            SaveRouteProgress();
                                        }

                                        PruneCompletedRouteSystems();
                                    }
                                    break;

                                case "SupercruiseEntry":
                                    Log.Debug("Entered supercruise");
                                    HyperspaceDestination = null;
                                    IsHyperspaceJumping = false;
                                    HyperspaceStarClass = null;

                                    break;

                                case "Location":
                                    if (IsHyperspaceJumping || _isInHyperspace)
                                    {
                                        Log.Warning("⚠️ Hyperspace state was still active during {Event} - resetting", eventType);

                                        HyperspaceDestination = null;
                                        HyperspaceStarClass = null;
                                    }

                                    if (root.TryGetProperty("StarSystem", out JsonElement locationElement))
                                    {
                                        string currentSystem = locationElement.GetString();

                                        if (!string.Equals(LastVisitedSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
                                        {
                                            LastVisitedSystem = currentSystem;
                                        }

                                        CurrentSystem = currentSystem;
                                        PruneCompletedRouteSystems();
                                    }
                                    
                                    // Clean up carrier jump state when we get location updates
                                    ResetFleetCarrierJumpState();
                                    break;

                                case "SupercruiseExit":
                                    if (IsHyperspaceJumping || _isInHyperspace)
                                    {
                                        Log.Warning("⚠️ Hyperspace state was still active during {Event} - resetting", eventType);

                                        HyperspaceDestination = null;
                                        HyperspaceStarClass = null;
                                    }

                                    if (root.TryGetProperty("StarSystem", out JsonElement exitSystemElement))
                                    {
                                        CurrentSystem = exitSystemElement.GetString();
                                        PruneCompletedRouteSystems();
                                    }
                                    break;

                                case "CargoDepot":
                                case "CarrierTradeOrder":
                                    if (!_cargoTrackingInitialized)
                                    {
                                        Log.Debug("Skipping historical cargo event {EventType} during initialization", eventType);
                                        continue;
                                    }
                                    
                                    Log.Information("Processing {EventType} cargo event using CarrierCargoTracker", eventType);
                                    
                                    // Use the CarrierCargoTracker for consistent processing
                                    _carrierCargoTracker.Process(root);
                                    
                                    // Update our local cargo state from the tracker
                                    using (BeginUpdate())
                                    {
                                        _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                                        UpdateCurrentCarrierCargoFromDictionary();
                                        SaveCarrierCargoToDisk();
                                    }
                                    
                                    Log.Information("{EventType} processed: {Count} items in carrier cargo",
                                        eventType, _carrierCargo.Count);
                                    break;

                                case "MarketBuy":
                                    if (!_cargoTrackingInitialized)
                                    {
                                        Log.Debug("Skipping historical cargo event {EventType} during initialization", eventType);
                                        continue;
                                    }
                                    
                                    // Only process if buying FROM carrier
                                    if (root.TryGetProperty("BuyFromFleetCarrier", out var boughtFromCarrierProp) && boughtFromCarrierProp.GetBoolean())
                                    {
                                        Log.Information("Processing MarketBuy FROM carrier using CarrierCargoTracker");
                                        
                                        // Use the CarrierCargoTracker for consistent processing
                                        _carrierCargoTracker.Process(root);
                                        
                                        // Update our local cargo state from the tracker
                                        using (BeginUpdate())
                                        {
                                            _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                                            UpdateCurrentCarrierCargoFromDictionary();
                                            SaveCarrierCargoToDisk();
                                        }
                                        
                                        Log.Information("MarketBuy FROM carrier processed: {Count} items in carrier cargo", _carrierCargo.Count);
                                    }
                                    else
                                    {
                                        Log.Debug("MarketBuy event ignored - not from carrier (goes to ship cargo)");
                                    }
                                    break;

                                case "MarketSell":
                                    if (!_cargoTrackingInitialized)
                                    {
                                        Log.Debug("Skipping historical cargo event {EventType} during initialization", eventType);
                                        continue;
                                    }
                                    
                                    // Only process if selling TO carrier
                                    if (root.TryGetProperty("SellToFleetCarrier", out var soldToCarrierProp) && soldToCarrierProp.GetBoolean())
                                    {
                                        Log.Information("Processing MarketSell TO carrier using CarrierCargoTracker");
                                        
                                        // Use the CarrierCargoTracker for consistent processing
                                        _carrierCargoTracker.Process(root);
                                        
                                        // Update our local cargo state from the tracker
                                        using (BeginUpdate())
                                        {
                                            _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                                            UpdateCurrentCarrierCargoFromDictionary();
                                            SaveCarrierCargoToDisk();
                                        }
                                        
                                        Log.Information("MarketSell TO carrier processed: {Count} items in carrier cargo", _carrierCargo.Count);
                                    }
                                    else
                                    {
                                        Log.Debug("MarketSell event ignored - not to carrier (comes from ship cargo)");
                                    }
                                    break;

                                case "CargoTransfer":
                                    if (!_cargoTrackingInitialized)
                                    {
                                        Log.Debug("Skipping historical cargo event {EventType} during initialization", eventType);
                                        continue;
                                    }
                                    
                                    Log.Information("Processing CargoTransfer event using CarrierCargoTracker");
                                    
                                    // CRITICAL: Update ship cargo FIRST to maintain consistency
                                    UpdateShipCargoFromTransfers(root);
                                    
                                    // Use the CarrierCargoTracker for consistent processing
                                    _carrierCargoTracker.Process(root);
                                    
                                    // Update our local cargo state from the tracker
                                    using (BeginUpdate())
                                    {
                                        _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                                        UpdateCurrentCarrierCargoFromDictionary();
                                        SaveCarrierCargoToDisk();
                                    }
                                    
                                    Log.Information("CargoTransfer processed: {Count} items in carrier cargo",
                                        _carrierCargo.Count);
                                    
                                    // Log the updated quantities for debugging
                                    foreach (var item in _carrierCargo.Take(10))
                                    {
                                        Log.Information("  Carrier: {Name}: {Quantity}", item.Key, item.Value);
                                    }
                                    
                                    // Log current ship cargo for debugging
                                    if (CurrentCargo?.Inventory != null)
                                    {
                                        Log.Information("Ship cargo now contains {Count} different items", CurrentCargo.Inventory.Count);
                                        foreach (var item in CurrentCargo.Inventory.Take(10))
                                        {
                                            Log.Information("  Ship: {Name}: {Quantity}", 
                                                CommodityMapper.GetDisplayName(item.Name), item.Count);
                                        }
                                    }
                                    break;

                                case "CarrierJumpRequest":
                                    Log.Information("🚀 📜 PROCESSING CarrierJumpRequest event");
                                    Log.Information("   - Full event: {Event}", line);
                                    
                                    if (root.TryGetProperty("DepartureTime", out var departureTimeProp) &&
                                        DateTime.TryParse(departureTimeProp.GetString(), out var departureTime))
                                    {
                                        Log.Information("🚀 DepartureTime parsed: {DepartureTime} (UTC: {UtcTime})", 
                                            departureTime, departureTime.ToUniversalTime());
                                        
                                        if (departureTime > DateTime.UtcNow)
                                        {
                                            Log.Information("✅ Departure time is in the future - processing jump request");
                                            
                                            // CRITICAL: Process carrier jump outside of batch update to ensure immediate UI response
                                            bool wasBatchMode2 = _isUpdating;
                                            if (wasBatchMode2)
                                            {
                                                Log.Information("🚀 Temporarily disabling batch mode for carrier jump request");
                                                _isUpdating = false;
                                            }

                                            FleetCarrierJumpTime = departureTime;
                                            CarrierJumpScheduledTime = departureTime;
                                            Log.Information("✅ Set FleetCarrierJumpTime = {Time}", departureTime);

                                            if (root.TryGetProperty("SystemName", out var sysName))
                                            {
                                                string systemName = sysName.GetString();
                                                CarrierJumpDestinationSystem = systemName;
                                                Log.Information("✅ Set CarrierJumpDestinationSystem = {SystemName}", systemName);
                                            }
                                            else
                                            {
                                                Log.Warning("❌ No SystemName property found in CarrierJumpRequest!");
                                            }

                                            if (root.TryGetProperty("Body", out var bodyName))
                                            {
                                                string bodyNameStr = bodyName.GetString();
                                                CarrierJumpDestinationBody = bodyNameStr;
                                                Log.Information("✅ Set CarrierJumpDestinationBody = {Body}", bodyNameStr);
                                            }

                                            JumpArrived = false;
                                            FleetCarrierJumpInProgress = true;
                                            Log.Information("✅ Set JumpArrived = false, FleetCarrierJumpInProgress = true");

                                            // Verify the properties were actually set
                                            Log.Information("🔍 VERIFICATION after setting properties:");
                                            Log.Information("   - FleetCarrierJumpInProgress: {InProgress}", FleetCarrierJumpInProgress);
                                            Log.Information("   - CarrierJumpDestinationSystem: {Destination}", CarrierJumpDestinationSystem);
                                            Log.Information("   - CarrierJumpScheduledTime: {ScheduledTime}", CarrierJumpScheduledTime);
                                            Log.Information("   - JumpArrived: {JumpArrived}", JumpArrived);
                                            Log.Information("   - ShowCarrierJumpOverlay: {ShowOverlay}", ShowCarrierJumpOverlay);

                                            Log.Information("🚀 Carrier jump scheduled for {Time}", departureTime);

                                            // Restore batch mode if it was active
                                            if (wasBatchMode2)
                                            {
                                                _isUpdating = true;
                                                Log.Information("🚀 Restored batch mode");
                                            }
                                        }
                                        else
                                        {
                                            Log.Warning("❌ CarrierJumpRequest ignored — departure time {DepartureTime} is in the past (current UTC: {CurrentTime})", 
                                                departureTime, DateTime.UtcNow);
                                        }
                                    }
                                    else
                                    {
                                        Log.Error("❌ CarrierJumpRequest: Could not parse DepartureTime property!");
                                        if (root.TryGetProperty("DepartureTime", out var depTimeProp))
                                        {
                                            Log.Error("   - Raw DepartureTime value: {RawValue}", depTimeProp.GetRawText());
                                        }
                                    }
                                    break;

                                case "CarrierJump":
                                    // CRITICAL: Break out of batch mode immediately for this event
                                    bool originalBatchMode = _isUpdating;
                                    _isUpdating = false;

                                    Log.Information("🚀 CarrierJump event detected - hiding overlay");
                                    Log.Information("🚀 Current state before: JumpArrived={JumpArrived}, FleetCarrierJumpInProgress={InProgress}, ShowOverlay={Show}",
                                        JumpArrived, FleetCarrierJumpInProgress, ShowCarrierJumpOverlay);

                                    // Force immediate property updates
                                    JumpArrived = true;
                                    FleetCarrierJumpInProgress = false;
                                    CarrierJumpScheduledTime = null;
                                    CarrierJumpDestinationSystem = null;
                                    CarrierJumpDestinationBody = null;

                                    // Force immediate notification of the overlay property
                                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay));

                                    Log.Information("🚀 Current state after: JumpArrived={JumpArrived}, FleetCarrierJumpInProgress={InProgress}, ShowOverlay={Show}",
                                        JumpArrived, FleetCarrierJumpInProgress, ShowCarrierJumpOverlay);

                                    // Restore batch mode
                                    _isUpdating = originalBatchMode;
                                    break;

                                case "CarrierJumpCancelled":
                                case "CarrierCancelJump":
                                    // CRITICAL: Process cancellation outside of batch update for immediate UI response
                                    bool wasBatchMode4 = _isUpdating;
                                    if (wasBatchMode4)
                                    {
                                        _isUpdating = false;
                                    }

                                    FleetCarrierJumpTime = null;
                                    CarrierJumpScheduledTime = null;
                                    CarrierJumpDestinationSystem = null;
                                    CarrierJumpDestinationBody = null;
                                    FleetCarrierJumpInProgress = false;

                                    if (wasBatchMode4)
                                    {
                                        _isUpdating = true;
                                    }

                                    Log.Information("Carrier jump cancelled");
                                    break;

                                case "CarrierLocation":
                                    Log.Debug("CarrierLocation seen — updating location");
                                    //FleetCarrierJumpInProgress = false;
                                    // JumpArrived = true;

                                    bool isOnCarrier = false;

                                    if (root.TryGetProperty("OnFoot", out var onFootProp) && !onFootProp.GetBoolean() &&
                                        root.TryGetProperty("Docked", out var dockedProp) && dockedProp.GetBoolean() &&
                                        root.TryGetProperty("StationType", out var stationTypeProp))
                                    {
                                        string stationType = stationTypeProp.GetString();
                                        isOnCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);

                                        Log.Information("CarrierLocation: {System}, StationType={Type}, IsOnCarrier={OnCarrier}",
                                            root.TryGetProperty("StarSystem", out var sysProp) ? sysProp.GetString() : "(unknown)",
                                            stationType,
                                            isOnCarrier);

                                        IsOnFleetCarrier = isOnCarrier;
                                    }

                                    if (IsOnFleetCarrier && root.TryGetProperty("StarSystem", out var carrierSystemProp))
                                    {
                                        var carrierSystem = carrierSystemProp.GetString();
                                        CurrentSystem = carrierSystem;
                                        Log.Debug("✅ Updated CurrentSystem from CarrierLocation: {System}", carrierSystem);
                                    }
                                    
                                    // Clean up carrier jump state after location changes
                                    ResetFleetCarrierJumpState();
                                    break;

                                case "ShipLocker":
                                    if (_isCarrierJumping)
                                    {
                                        Log.Information("Carrier jump completed - carrier has arrived");
                                        CarrierJumpDestinationSystem = null;
                                        _isCarrierJumping = false;
                                        _jumpArrived = true;
                                    }
                                    break;

                                case "CommitCrime":
                                    ProcessLegalStateEvent(root, "CommitCrime");
                                    break;

                                case "FactionKillBond":
                                case "Bounty":
                                    ProcessLegalStateEvent(root, eventType);
                                    break;

                                case "FactionAllianceChanged":
                                    ProcessLegalStateEvent(root, "FactionAllianceChanged");
                                    break;

                                case "Status":
                                    ProcessLegalStateEvent(root, "Status");
                                    break;

                                // In GameStateService.cs - Replace the ColonisationConstructionDepot case with this enhanced version

                                case "ColonisationConstructionDepot":
                                    try
                                    {
                                        Log.Information("📋 Processing ColonisationConstructionDepot event");

                                        var colonizationData = new ColonizationData
                                        {
                                            LastUpdated = DateTime.UtcNow
                                        };

                                        if (root.TryGetProperty("MarketID", out var marketIdProp))
                                        {
                                            colonizationData.MarketID = marketIdProp.GetInt64();
                                        }

                                        if (root.TryGetProperty("ConstructionProgress", out var progressProp))
                                        {
                                            colonizationData.ConstructionProgress = progressProp.GetDouble();
                                            Log.Information("📋 Progress updated to: {Progress:P2}", colonizationData.ConstructionProgress);
                                        }

                                        if (root.TryGetProperty("ConstructionComplete", out var completeProp))
                                        {
                                            colonizationData.ConstructionComplete = completeProp.GetBoolean();
                                        }

                                        if (root.TryGetProperty("ConstructionFailed", out var failedProp))
                                        {
                                            colonizationData.ConstructionFailed = failedProp.GetBoolean();
                                        }

                                        colonizationData.ResourcesRequired = new List<ColonizationResource>();

                                        if (root.TryGetProperty("ResourcesRequired", out var resourcesProp) &&
                                            resourcesProp.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var resource in resourcesProp.EnumerateArray())
                                            {
                                                var resourceItem = new ColonizationResource();

                                                if (resource.TryGetProperty("Name", out var nameProp))
                                                    resourceItem.Name = nameProp.GetString();

                                                if (resource.TryGetProperty("Name_Localised", out var nameLocProp))
                                                    resourceItem.Name_Localised = nameLocProp.GetString();

                                                if (resource.TryGetProperty("RequiredAmount", out var reqProp))
                                                    resourceItem.RequiredAmount = reqProp.GetInt32();

                                                if (resource.TryGetProperty("ProvidedAmount", out var provProp))
                                                    resourceItem.ProvidedAmount = provProp.GetInt32();

                                                if (resource.TryGetProperty("Payment", out var payProp))
                                                    resourceItem.Payment = payProp.GetInt32();

                                                colonizationData.ResourcesRequired.Add(resourceItem);
                                            }
                                        }

                                        // CRITICAL FIX: Update colonization data OUTSIDE of batch system to ensure immediate UI update
                                        bool wasBatchMode2 = _isUpdating;
                                        if (wasBatchMode2)
                                        {
                                            Log.Information("📋 Temporarily disabling batch mode for colonization update");
                                            _isUpdating = false;
                                        }

                                        // Add or update depot in dictionary
                                        _colonizationDepots[colonizationData.MarketID] = colonizationData;

                                        // Set as selected if none selected or this is the only one
                                        if (!_selectedDepotMarketId.HasValue || _colonizationDepots.Count == 1)
                                        {
                                            _selectedDepotMarketId = colonizationData.MarketID;
                                        }

                                        // Notify changes
                                        OnPropertyChanged(nameof(ColonizationDepots));
                                        OnPropertyChanged(nameof(SelectedColonizationDepot));

                                        if (wasBatchMode2)
                                        {
                                            _isUpdating = true;
                                        }

                                        // Save all depots
                                        SaveAllColonizationData();

                                        // Publish to MQTT
                                        await MqttService.Instance.PublishColonizationDepotAsync(
                                            colonizationData.MarketID,
                                            colonizationData.ConstructionProgress,
                                            colonizationData.ConstructionComplete,
                                            colonizationData.ConstructionFailed,
                                            colonizationData.ResourcesRequired);

                                        // Also publish aggregated data
                                        await MqttService.Instance.PublishAllColonizationDepotsAsync(GetActiveColonizationDepots());

                                        Log.Information("📋 Colonization depot {MarketID} updated - Progress: {Progress:P2}",
                                            colonizationData.MarketID, colonizationData.ConstructionProgress);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "📋 Error processing ColonisationConstructionDepot event");
                                    }
                                    break;

                                case "ReceiveText":
                                    string msg = null;

                                    //nothing here yet
                                    break;

                                case "SquadronStartup":
                                    if (root.TryGetProperty("SquadronName", out var squadron))
                                        SquadronName = squadron.GetString();
                                    break;

                                case "Music":
                                    if (root.TryGetProperty("MusicTrack", out var musicTrackProp) &&
                                        musicTrackProp.GetString() == "DockingComputer")
                                    {
                                        //do nothing
                                    }
                                    break;
                            }

                            if (!_firstLoadCompleted)
                            {
                                _firstLoadCompleted = true;
                                Log.Information("✅ First journal scan completed");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Error processing journal file");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading journal file");
            }
        }

        public void PruneCompletedRouteSystems()
        {
            if (CurrentRoute?.Route == null || string.IsNullOrWhiteSpace(CurrentSystem))
                return;

            // Try to match the current system (case-insensitive) to a route entry
            int index = CurrentRoute.Route.FindIndex(j =>
                string.Equals(j.StarSystem, CurrentSystem, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                Log.Information("📍 Pruning route - current system is {0}, removing {1} previous entries",
                    CurrentSystem, index);

                var updatedRoute = new NavRouteJson
                {
                    Route = CurrentRoute.Route.Skip(index).ToList()
                };

                CurrentRoute = updatedRoute;
                OnPropertyChanged(nameof(TotalRemainingJumps));
            }
        }

        public async Task PublishCurrentStateToMqtt()
        {
            if (_mqttInitialized && CurrentStatus != null)
            {
                try
                {
                    await MqttService.Instance.PublishFlagStatesAsync(CurrentStatus, IsDocking);
                    Log.Information("Published current state to MQTT");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error publishing current state to MQTT");
                }
            }
        }

        public async Task RefreshMqttSettingsAsync()
        {
            try
            {
                var settings = SettingsManager.Load();
                await MqttService.Instance.InitializeAsync(settings);
                _mqttInitialized = settings.MqttEnabled;
                Log.Information("MQTT settings refreshed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing MQTT settings");
            }
        }

        public void ResetFleetCarrierJumpState()
        {
            // Only log and process if there's actually some jump state to check
            bool hasJumpState = FleetCarrierJumpInProgress || JumpArrived || CarrierJumpScheduledTime.HasValue || !string.IsNullOrEmpty(CarrierJumpDestinationSystem);
            
            if (!hasJumpState)
            {
                // No jump state exists, nothing to reset
                return;
            }
            
            Log.Information("🔄 🚀 RESET CHECK - ResetFleetCarrierJumpState called");
            Log.Information("   - FleetCarrierJumpInProgress: {InProgress}", FleetCarrierJumpInProgress);
            Log.Information("   - IsOnFleetCarrier: {OnCarrier}", IsOnFleetCarrier);
            Log.Information("   - JumpArrived: {JumpArrived}", JumpArrived);
            Log.Information("   - CarrierJumpScheduledTime: {ScheduledTime}", CarrierJumpScheduledTime);
            Log.Information("   - CarrierJumpDestinationSystem: {Destination}", CarrierJumpDestinationSystem);
            
            // If jump has completed and we're still on carrier, clear ALL jump state
            if (JumpArrived && !FleetCarrierJumpInProgress && IsOnFleetCarrier)
            {
                Log.Information("✅ 🚀 Carrier jump completed - clearing ALL jump state");
                JumpArrived = false;
                CarrierJumpScheduledTime = null;
                CarrierJumpDestinationSystem = null;
                CarrierJumpDestinationBody = null;
                _lastCarrierJumpCountdown = -1;
                Log.Information("✅ 🚀 All carrier jump state cleared - no more jump processing until next request");
                return;
            }
            
            // Check if we should reset stale jump-in-progress state
            if (FleetCarrierJumpInProgress &&
                (!IsOnFleetCarrier || JumpArrived || CarrierJumpScheduledTime?.ToLocalTime() < DateTime.Now.AddMinutes(-5)))
            {
                Log.Warning("⚠️ 🚀 RESETTING stale carrier jump state - JumpInProgress={InProgress}, OnCarrier={OnCarrier}, JumpArrived={JumpArrived}",
                    FleetCarrierJumpInProgress, IsOnFleetCarrier, JumpArrived);

                FleetCarrierJumpInProgress = false;
                CarrierJumpScheduledTime = null;
                CarrierJumpDestinationSystem = null;
                CarrierJumpDestinationBody = null;
                _lastCarrierJumpCountdown = -1;
                JumpArrived = false;
                
                Log.Warning("❌ 🚀 RESET COMPLETE - all carrier jump properties cleared");
            }
            else
            {
                Log.Information("✅ 🚀 RESET SKIPPED - carrier jump state is valid, no reset needed");
            }
        }

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
        public void UpdateCarrierCargoItem(string itemName, int quantity, bool isManualChange = true)
        {
            Log.Debug("UpdateCarrierCargoItem: {Item} = {Quantity} (Manual: {Manual})",
                itemName, quantity, isManualChange);

            lock (_cargoLock)
            {
                int oldValue = _carrierCargo.TryGetValue(itemName, out int existing) ? existing : 0;

                using (BeginUpdate())
                {
                    // Update the in-memory cargo dictionary
                    if (quantity > 0)
                    {
                        _carrierCargo[itemName] = quantity;
                    }
                    else
                    {
                        if (_carrierCargo.ContainsKey(itemName))
                        {
                            _carrierCargo.Remove(itemName);
                            Log.Debug("Removed {Item} from carrier cargo tracking dictionary", itemName);
                        }

                        var itemToRemove = _currentCarrierCargo.FirstOrDefault(i =>
                            string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));

                        if (itemToRemove != null)
                        {
                            _currentCarrierCargo.Remove(itemToRemove);
                            Log.Debug("Removed {Item} from CurrentCarrierCargo UI list", itemName);
                        }
                    }

                    // Track manual changes with timestamp and original value
                    if (isManualChange)
                    {
                        var originalGameValue = GetOriginalGameValueForItem(itemName);

                        _manualCarrierCargoChanges[itemName] = new ManualCargoChange
                        {
                            ItemName = itemName,
                            ManualQuantity = quantity,
                            OriginalGameQuantity = originalGameValue,
                            LastModified = DateTime.UtcNow,
                            IsActive = true
                        };

                        SaveManualCarrierCargoChanges();
                        Log.Information("Saved manual change: {Item} {Original} → {Manual}",
                            itemName, originalGameValue, quantity);
                    }
                    else
                    {
                        // For game updates, don't override active manual changes
                        if (_manualCarrierCargoChanges.ContainsKey(itemName))
                        {
                            var manualChange = _manualCarrierCargoChanges[itemName];
                            if (DateTime.UtcNow - manualChange.LastModified < TimeSpan.FromMinutes(30))
                            {
                                Log.Information("Preserving manual change for {Item}: keeping {Manual} instead of game value {Game}",
                                    itemName, manualChange.ManualQuantity, quantity);
                                
                                // Update the original game quantity but keep the manual override
                                manualChange.OriginalGameQuantity = quantity;
                                _carrierCargo[itemName] = manualChange.ManualQuantity;
                                SaveManualCarrierCargoChanges();
                            }
                            else
                            {
                                // Manual change has expired, remove it
                                _manualCarrierCargoChanges.Remove(itemName);
                                SaveManualCarrierCargoChanges();
                                Log.Information("Manual change for {Item} has expired, accepting game value {Quantity}",
                                    itemName, quantity);
                            }
                        }
                    }

                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }

                Log.Information("Carrier cargo updated: {Item} {OldValue} → {NewValue} (Manual: {Manual})",
                    itemName, oldValue, quantity, isManualChange);
            }
        }
        /// <summary>
        /// Gets the original game quantity for an item before manual changes
        /// </summary>
        private int GetOriginalGameValueForItem(string itemName)
        {
            // If we already have a manual change record, use its original value
            if (_manualCarrierCargoChanges.TryGetValue(itemName, out var existingChange))
            {
                return existingChange.OriginalGameQuantity;
            }

            // Otherwise, the current value IS the original game value
            return _carrierCargo.TryGetValue(itemName, out int currentValue) ? currentValue : 0;
        }
        /// <summary>
        /// Synchronizes GameState cargo with CarrierCargoTracker to ensure consistency
        /// </summary>
        public void SynchronizeCarrierCargoState()
        {
            try
            {
                Log.Information("🔄 Synchronizing carrier cargo state between GameState and CarrierCargoTracker");
                
                using (BeginUpdate())
                {
                    _carrierCargo = new Dictionary<string, int>(_carrierCargoTracker.Cargo);
                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }
                
                Log.Information("✅ Carrier cargo state synchronized: {Count} items", _carrierCargo.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error synchronizing carrier cargo state");
            }
        }
        
        /// <summary>
        /// Cleans up duplicate cargo entries caused by case sensitivity issues
        /// </summary>
        public void CleanupDuplicateCargoEntries()
        {
            try
            {
                Log.Information("🧽 Cleaning up duplicate carrier cargo entries");
                
                var duplicateGroups = _carrierCargo.Keys
                    .GroupBy(k => k.ToLowerInvariant())
                    .Where(g => g.Count() > 1)
                    .ToList();
                
                if (!duplicateGroups.Any())
                {
                    Log.Information("✅ No duplicate entries found");
                    return;
                }
                
                using (BeginUpdate())
                {
                    foreach (var group in duplicateGroups)
                    {
                        var items = group.ToList();
                        Log.Information("🔄 Found duplicates: {Items}", string.Join(", ", items));
                        
                        // Find the best key to keep (prefer proper case)
                        string bestKey = items.FirstOrDefault(k => char.IsUpper(k[0])) ?? items.First();
                        int totalQuantity = 0;
                        
                        // Sum up all quantities
                        foreach (var key in items)
                        {
                            totalQuantity += _carrierCargo[key];
                            if (key != bestKey)
                            {
                                _carrierCargo.Remove(key);
                                Log.Information("❌ Removed duplicate: {Key}", key);
                            }
                        }
                        
                        // Update the best key with the total quantity
                        _carrierCargo[bestKey] = totalQuantity;
                        Log.Information("✅ Consolidated to: {Key} = {Quantity}", bestKey, totalQuantity);
                    }
                    
                    UpdateCurrentCarrierCargoFromDictionary();
                    SaveCarrierCargoToDisk();
                }
                
                Log.Information("✅ Duplicate cleanup completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up duplicate cargo entries");
            }
        }
        
        /// <summary>
        /// Manual fix for specific cargo discrepancies - useful for debugging
        /// </summary>
        public void FixCarrierCargoItem(string itemName, int correctQuantity)
        {
            try
            {
                Log.Information("🔧 Manual fix for carrier cargo item: {Item}", itemName);
                
                // Find the correct item name (case-insensitive)
                var actualKey = _carrierCargo.Keys.FirstOrDefault(k => 
                    string.Equals(k, itemName, StringComparison.OrdinalIgnoreCase));
                
                if (actualKey != null)
                {
                    int oldQty = _carrierCargo[actualKey];
                    
                    using (BeginUpdate())
                    {
                        if (correctQuantity > 0)
                        {
                            _carrierCargo[actualKey] = correctQuantity;
                        }
                        else
                        {
                            _carrierCargo.Remove(actualKey);
                        }
                        
                        UpdateCurrentCarrierCargoFromDictionary();
                        SaveCarrierCargoToDisk();
                    }
                    
                    Log.Information("✅ Fixed {Item}: {OldQty} → {NewQty}", actualKey, oldQty, correctQuantity);
                }
                else
                {
                    Log.Warning("❌ Could not find item '{Item}' in carrier cargo", itemName);
                    Log.Information("Available items: {Items}", string.Join(", ", _carrierCargo.Keys));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fixing carrier cargo item {Item}", itemName);
            }
        }
        
        /// <summary>
        /// Forces a refresh of both ship and carrier cargo states
        /// </summary>
        public void RefreshCargoStates()
        {
            try
            {
                Log.Information("🔄 Force refreshing cargo states");
                
                // Reload ship cargo from file
                LoadCargoData();
                
                // Force process recent cargo events
                ForceProcessRecentCargoEvents();
                
                // Clean up any inconsistencies
                _carrierCargoTracker.NormalizeCargoKeys();
                SynchronizeCarrierCargoState();
                
                Log.Information("✅ Cargo states refreshed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing cargo states");
            }
        }
        
        /// <summary>
        /// Analyzes recent cargo transfer events to understand discrepancies
        /// </summary>
        public void AnalyzeRecentCargoTransfers(string commodityName = null)
        {
            try
            {
                Log.Information("🔍 Analyzing recent cargo transfers for: {Commodity}", commodityName ?? "ALL");
                
                if (string.IsNullOrEmpty(latestJournalPath) || !File.Exists(latestJournalPath))
                {
                    Log.Warning("No journal file available for analysis");
                    return;
                }
                
                var fileInfo = new FileInfo(latestJournalPath);
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                
                // Read the last 50KB to get more transfer events
                long startPos = Math.Max(0, fileInfo.Length - 51200);
                fs.Seek(startPos, SeekOrigin.Begin);
                
                using var sr = new StreamReader(fs);
                var transferEvents = new List<(DateTime timestamp, string eventLine)>();
                
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line) && line.Contains("CargoTransfer"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            
                            if (root.TryGetProperty("timestamp", out var timestampProp) &&
                                DateTime.TryParse(timestampProp.GetString(), out var timestamp))
                            {
                                transferEvents.Add((timestamp, line));
                            }
                        }
                        catch
                        {
                            // Skip malformed JSON
                        }
                    }
                }
                
                Log.Information("Found {Count} CargoTransfer events", transferEvents.Count);
                
                // Analyze each transfer event
                int totalAdded = 0, totalRemoved = 0;
                foreach (var (timestamp, eventLine) in transferEvents.OrderBy(e => e.timestamp).TakeLast(10))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(eventLine);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("Transfers", out var transfersProp) &&
                            transfersProp.ValueKind == JsonValueKind.Array)
                        {
                            Log.Information("📅 Transfer at {Timestamp}:", timestamp.ToString("HH:mm:ss"));
                            
                            foreach (var transfer in transfersProp.EnumerateArray())
                            {
                                if (transfer.TryGetProperty("Type", out var typeProp) &&
                                    transfer.TryGetProperty("Count", out var countProp) &&
                                    transfer.TryGetProperty("Direction", out var directionProp))
                                {
                                    string internalName = typeProp.GetString();
                                    int count = countProp.GetInt32();
                                    string direction = directionProp.GetString();
                                    string displayName = CommodityMapper.GetDisplayName(internalName);
                                    
                                    // Filter by commodity if specified
                                    if (!string.IsNullOrEmpty(commodityName) && 
                                        !displayName.Contains(commodityName, StringComparison.OrdinalIgnoreCase) &&
                                        !internalName.Contains(commodityName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                    
                                    Log.Information("  🔄 {InternalName} → {DisplayName}: {Direction} {Count}",
                                        internalName, displayName, direction, count);
                                    
                                    if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                                    {
                                        totalAdded += count;
                                    }
                                    else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                                    {
                                        totalRemoved += count;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error analyzing transfer event");
                    }
                }
                
                Log.Information("📊 Transfer summary: +{Added} to carrier, -{Removed} from carrier", totalAdded, totalRemoved);
                
                if (!string.IsNullOrEmpty(commodityName))
                {
                    var currentQty = _carrierCargo.TryGetValue(commodityName, out int qty) ? qty : 0;
                    Log.Information("📦 Current {Commodity} quantity: {Quantity}", commodityName, currentQty);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error analyzing recent cargo transfers");
            }
        }
        
        /// <summary>
        /// Processes game file updates while preserving manual changes
        /// </summary>
        private void ProcessCarrierCargoGameUpdate(Dictionary<string, int> newGameData)
        {
            if (newGameData == null) return;

            lock (_cargoLock)
            {
                Log.Information("Processing carrier cargo game update with manual change preservation");

                // Load existing manual changes
                LoadManualCarrierCargoChanges();

                using (BeginUpdate())
                {
                    // Process each item from the game data
                    foreach (var gameItem in newGameData)
                    {
                        // Use the UpdateCarrierCargoItem method with isManualChange = false
                        // This will automatically handle preserving manual changes
                        UpdateCarrierCargoItem(gameItem.Key, gameItem.Value, isManualChange: false);
                    }

                    // Handle items that no longer exist in game data but might have manual changes
                    var itemsToCheck = _carrierCargo.Keys.ToList();
                    foreach (var existingItem in itemsToCheck)
                    {
                        if (!newGameData.ContainsKey(existingItem))
                        {
                            // Item doesn't exist in game data anymore
                            if (!_manualCarrierCargoChanges.ContainsKey(existingItem))
                            {
                                // No manual change exists, remove the item
                                UpdateCarrierCargoItem(existingItem, 0, isManualChange: false);
                            }
                            else
                            {
                                // Manual change exists, check if it's still valid
                                var manualChange = _manualCarrierCargoChanges[existingItem];
                                if (DateTime.UtcNow - manualChange.LastModified > TimeSpan.FromMinutes(30))
                                {
                                    // Manual change has expired
                                    _manualCarrierCargoChanges.Remove(existingItem);
                                    UpdateCarrierCargoItem(existingItem, 0, isManualChange: false);
                                    SaveManualCarrierCargoChanges();
                                    Log.Information("Expired manual change removed for non-existent item: {Item}", existingItem);
                                }
                            }
                        }
                    }
                }

                Log.Information("Game cargo update processing complete: {GameItems} game items processed",
                    newGameData.Count);
            }
        }

        /// <summary>
        /// Saves manual changes to persistent storage
        /// </summary>
        private void SaveManualCarrierCargoChanges()
        {
            try
            {
                string directory = Path.GetDirectoryName(ManualCarrierCargoFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var activeChanges = _manualCarrierCargoChanges
                    .Where(kvp => kvp.Value.IsActive)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var json = JsonSerializer.Serialize(activeChanges, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ManualCarrierCargoFilePath, json);
                Log.Debug("Saved {Count} manual carrier cargo changes to disk", activeChanges.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save manual carrier cargo changes to disk");
            }
        }

        /// <summary>
        /// Loads manual changes from persistent storage
        /// </summary>
        private void LoadManualCarrierCargoChanges()
        {
            try
            {
                if (!File.Exists(ManualCarrierCargoFilePath)) return;

                var json = File.ReadAllText(ManualCarrierCargoFilePath);
                var loadedChanges = JsonSerializer.Deserialize<Dictionary<string, ManualCargoChange>>(json);

                if (loadedChanges != null)
                {
                    // Only load changes that are still recent (within 30 minutes)
                    var validChanges = loadedChanges.Where(kvp =>
                        DateTime.UtcNow - kvp.Value.LastModified < TimeSpan.FromMinutes(30))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    _manualCarrierCargoChanges.Clear();
                    foreach (var change in validChanges)
                    {
                        _manualCarrierCargoChanges[change.Key] = change.Value;
                    }

                    Log.Information("Loaded {Count} valid manual carrier cargo changes from disk",
                        _manualCarrierCargoChanges.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load manual carrier cargo changes from disk");
                _manualCarrierCargoChanges.Clear();
            }
        }

        /// <summary>
        /// Clears expired manual changes
        /// </summary>
        public void ClearExpiredManualCargoChanges()
        {
            lock (_cargoLock)
            {
                var expiredKeys = _manualCarrierCargoChanges
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastModified > TimeSpan.FromMinutes(30))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _manualCarrierCargoChanges.Remove(key);
                }

                if (expiredKeys.Any())
                {
                    SaveManualCarrierCargoChanges();
                    Log.Information("Cleared {Count} expired manual cargo changes", expiredKeys.Count);
                }
            }
        }
        public void UpdateColonizationDepot(long marketId, ColonizationData data)
        {
            _colonizationDepots[marketId] = data;

            // If this is the first depot or currently selected depot, select it
            if (!_selectedDepotMarketId.HasValue || _selectedDepotMarketId.Value == marketId)
            {
                _selectedDepotMarketId = marketId;
            }

            OnPropertyChanged(nameof(ColonizationDepots));
            OnPropertyChanged(nameof(CurrentColonization));
            SaveAllColonizationData();
        }
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
                // Send notification immediately
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            Log.Debug("🚨 SetProperty fired for: {Property}", propertyName); // 👈 TEMP LOG

            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion Protected Methods

        #region Private Methods

        private bool BackpacksEqual(BackpackJson bp1, BackpackJson bp2)
        {
            if (bp1 == null && bp2 == null) return true;
            if (bp1 == null || bp2 == null) return false;

            return ItemListsEqual(bp1.Items, bp2.Items) &&
                   ItemListsEqual(bp1.Components, bp2.Components) &&
                   ItemListsEqual(bp1.Consumables, bp2.Consumables) &&
                   ItemListsEqual(bp1.Data, bp2.Data);
        }

        /// <summary>
        /// Begins a batch update operation that defers property change notifications
        /// </summary>
        private IDisposable BeginUpdate()
        {
            _isUpdating = true;
            return new UpdateScope(this);
        }

        private T DeserializeJsonFile<T>(string filePath) where T : class
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (!File.Exists(filePath)) return null;

                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length == 0) return null;

                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(json))
                        return null;

                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (IOException)
                {
                    Thread.Sleep(50); // Retry briefly
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to deserialize {FilePath}: {Error}", filePath, ex.Message);
                    return null;
                }
            }

            return null;
        }

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

        private void EnsureHyperspaceTimeout()
        {
            // Cancel any existing timeout
            if (_hyperspaceTimeoutCts != null)
            {
                _hyperspaceTimeoutCts.Cancel();
                _hyperspaceTimeoutCts.Dispose();
            }

            _hyperspaceTimeoutCts = new CancellationTokenSource();
            var token = _hyperspaceTimeoutCts.Token;

            // Set a 30-second safety timeout - hyperspace jumps should never take longer than this
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000, token); // 30-second timeout

                    // If we reach here, the timeout wasn't cancelled, so force reset the hyperspace flag
                    if (!token.IsCancellationRequested && (IsHyperspaceJumping || _isInHyperspace))
                    {
                        Log.Warning("⚠️ Hyperspace safety timeout reached - forcing reset of hyperspace state");
                        IsHyperspaceJumping = false;
                        _isInHyperspace = false;
                        HyperspaceDestination = null;
                        HyperspaceStarClass = null;
                    }
                }
                catch (TaskCanceledException)
                {
                    // This is expected when the timeout is cancelled normally
                }
            }, token);
        }

        private bool FCMaterialItemsEqual(List<FCMaterialsJson.MaterialItem> list1, List<FCMaterialsJson.MaterialItem> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            var dict1 = list1.ToDictionary(i => i.Name, i => (i.Stock, i.Demand, i.Price));

            foreach (var item in list2)
            {
                if (!dict1.TryGetValue(item.Name, out var values) ||
                    values.Stock != item.Stock ||
                    values.Demand != item.Demand ||
                    values.Price != item.Price)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Updates ship cargo to reflect transfers to/from carrier.
        /// Elite: Dangerous doesn't immediately update Cargo.json during transfers,
        /// so we need to simulate the changes for accurate UI display.
        /// </summary>
        private void UpdateShipCargoFromTransfers(JsonElement root)
        {
            if (!_cargoTrackingInitialized || CurrentCargo?.Inventory == null) return;
            if (!root.TryGetProperty("Transfers", out var transfers)) return;

            var updatedInventory = new List<CargoJson.CargoItem>(CurrentCargo.Inventory);
            bool cargoChanged = false;

            foreach (var transfer in transfers.EnumerateArray())
            {
                if (!transfer.TryGetProperty("Type", out var typeProp) ||
                    !transfer.TryGetProperty("Count", out var countProp) ||
                    !transfer.TryGetProperty("Direction", out var directionProp))
                    continue;

                string internalName = typeProp.GetString();
                int count = countProp.GetInt32();
                string direction = directionProp.GetString();

                if (string.IsNullOrWhiteSpace(internalName)) continue;

                // Find existing inventory item
                var existingItem = updatedInventory.FirstOrDefault(i =>
                    string.Equals(i.Name, internalName, StringComparison.OrdinalIgnoreCase));

                if (string.Equals(direction, "tocarrier", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove from ship cargo (transferred TO carrier)
                    if (existingItem != null)
                    {
                        int newCount = Math.Max(0, existingItem.Count - count);
                        if (newCount > 0)
                        {
                            existingItem.Count = newCount;
                        }
                        else
                        {
                            updatedInventory.Remove(existingItem);
                        }
                        cargoChanged = true;
                        Log.Information("📦 Ship cargo updated: {Item} reduced by {Count} (transferred to carrier)",
                            CommodityMapper.GetDisplayName(internalName), count);
                    }
                }
                else if (string.Equals(direction, "toship", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(direction, "fromcarrier", StringComparison.OrdinalIgnoreCase))
                {
                    // Add to ship cargo (transferred FROM carrier)
                    if (existingItem != null)
                    {
                        existingItem.Count += count;
                    }
                    else
                    {
                        updatedInventory.Add(new CargoJson.CargoItem
                        {
                            Name = internalName,
                            Count = count,
                            Value = 0 // Default value, will be updated by the game later
                        });
                    }
                    cargoChanged = true;
                    Log.Information("📦 Ship cargo updated: {Item} increased by {Count} (transferred from carrier)",
                        CommodityMapper.GetDisplayName(internalName), count);
                }
            }

            // Update CurrentCargo if changes were made
            if (cargoChanged)
            {
                var updatedCargo = new CargoJson
                {
                    Inventory = updatedInventory
                };

                CurrentCargo = updatedCargo;
                Log.Information("✅ Ship cargo synchronized after transfer - Total items: {Count}", 
                    updatedInventory.Sum(i => i.Count));
            }
        }

        private void InferClassAndRatingFromItem(LoadoutModule module)
        {
            if (!string.IsNullOrEmpty(module.Item))
            {
                var match = Regex.Match(module.Item, @"_(size)?(?<class>\d+)_class(?<rating>\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (int.TryParse(match.Groups["class"].Value, out var classNum))
                    {
                        module.Class = classNum;
                    }
                    module.Rating = match.Groups["rating"].Value;
                }
            }
        }

        private async Task InitializeMqttAsync()
        {
            try
            {
                var settings = SettingsManager.Load();
                if (settings.MqttEnabled)
                {
                    await MqttService.Instance.InitializeAsync(settings);
                    _mqttInitialized = true;
                    Log.Information("MQTT service initialized for GameStateService");

                    // ✅ Publish initial state if available
                    if (CurrentStatus != null)
                    {
                        await MqttService.Instance.PublishFlagStatesAsync(CurrentStatus);
                        Log.Information("✅ Initial state published to MQTT after MQTT initialization.");
                    }
                    else
                    {
                        Log.Warning("⚠️ Cannot publish initial state: CurrentStatus is null.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MQTT service in GameStateService");
            }
        }

        private bool InventoriesEqual(List<CargoJson.CargoItem> inventory1, List<CargoJson.CargoItem> inventory2)
        {
            if (inventory1 == null && inventory2 == null) return true;
            if (inventory1 == null || inventory2 == null) return false;
            if (inventory1.Count != inventory2.Count) return false;

            // Create Dictionary for faster lookups
            var dict1 = inventory1.ToDictionary(i => i.Name, i => i.Count);

            foreach (var item in inventory2)
            {
                if (!dict1.TryGetValue(item.Name, out int count) || count != item.Count)
                    return false;
            }

            return true;
        }

        private bool ItemListsEqual(List<BackpackJson.BackpackItem> list1, List<BackpackJson.BackpackItem> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            var dict1 = list1.ToDictionary(i => i.Name, i => i.Count);

            foreach (var item in list2)
            {
                if (!dict1.TryGetValue(item.Name, out int count) || count != item.Count)
                    return false;
            }

            return true;
        }

        // Helper methods for comparing complex objects
        private bool JsonEquals<T>(T obj1, T obj2)
        {
            if (obj1 == null && obj2 == null) return true;
            if (obj1 == null || obj2 == null) return false;

            // Serialize and compare as strings - simple but effective
            var options = new JsonSerializerOptions { WriteIndented = false };
            string json1 = JsonSerializer.Serialize(obj1, options);
            string json2 = JsonSerializer.Serialize(obj2, options);

            return json1 == json2;
        }

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
                LoadPersistedColonizationData();
                latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                LoadRouteProgress();
                LoadCarrierCargoFromDisk(); // ← Add this near LoadRouteProgress();

                Task.Run(async () => await ProcessJournalAsync()).Wait();
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
                Log.Information("✅ GameStateService: First load completed");
                // Check for stale carrier jump states
                ResetFleetCarrierJumpState();

                // Explicitly notify key jump-related properties
                OnPropertyChanged(nameof(FleetCarrierJumpInProgress));
                OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                OnPropertyChanged(nameof(JumpArrived));
                LoadCarrierCargoFromDisk();
                LoadPersistedColonizationData();
                // Set cargo tracking as initialized after loading saved data
                _cargoTrackingInitialized = true;
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
                if (File.Exists(CarrierCargoFilePath))
                {
                    var json = File.ReadAllText(CarrierCargoFilePath);
                    var loadedCargo = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (loadedCargo != null && loadedCargo.Any())
                    {
                        _carrierCargo = loadedCargo;
                        _carrierCargoTracker.Initialize(_carrierCargo);
                        UpdateCurrentCarrierCargoFromDictionary();
                        Log.Information("Loaded {Count} carrier cargo items from disk", _carrierCargo.Count);
                    }
                    else
                    {
                        Log.Warning("Carrier cargo file exists but is empty or invalid");
                        _carrierCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    Log.Information("No carrier cargo file found - starting with empty cargo");
                    _carrierCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }
                
                // Always try to load manual changes
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
                Log.Warning("🚫 ModulesInfo.json was null on startup");
                return false;
            }

            Log.Information("✅ Loaded ModulesInfo.json with {Count} modules", newLoadout.Modules?.Count ?? 0);

            if (CurrentLoadout == null || !JsonEquals(CurrentLoadout, newLoadout))
            {
                Log.Information("⚡ Setting CurrentLoadout...");
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

        private void LoadPersistedColonizationData()
        {
            try
            {
                if (!File.Exists(ColonizationDataFile))
                {
                    Log.Information("No persisted colonization data file found");
                    return;
                }

                string json = File.ReadAllText(ColonizationDataFile);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Warning("Colonization data file is empty");
                    return;
                }

                var depots = JsonSerializer.Deserialize<List<ColonizationData>>(json);
                if (depots == null || !depots.Any())
                {
                    Log.Warning("No colonization depots found in file");
                    return;
                }

                _colonizationDepots.Clear();
                foreach (var depot in depots.Where(d => !d.ConstructionComplete && !d.ConstructionFailed))
                {
                    _colonizationDepots[depot.MarketID] = depot;
                }

                // Select first depot
                _selectedDepotMarketId = _colonizationDepots.Keys.FirstOrDefault();

                Log.Information("Loaded {Count} active colonization depots from file", _colonizationDepots.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading persisted colonization data");
            }
        }

        private void LoadRouteProgress()
        {
            try
            {
                if (File.Exists(RouteProgressFile))
                {
                    string json = File.ReadAllText(RouteProgressFile);
                    _routeProgress = JsonSerializer.Deserialize<RouteProgressState>(json) ?? new RouteProgressState();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load RouteProgress.json");
            }
        }

        private bool LoadStatusData()
        {
            var oldStatus = CurrentStatus;
            CurrentStatus = LoadJsonFile<StatusJson>("Status.json", CurrentStatus);

            bool changed = !ReferenceEquals(oldStatus, CurrentStatus);

            if (changed)
            {
                if (CurrentStatus?.Flags != null)
                {
                    uint rawFlags = (uint)CurrentStatus.Flags;
                    Log.Debug("Raw Status Flags value: 0x{RawFlags:X8} ({RawFlags})",
                        rawFlags, rawFlags);

                    Log.Debug("Active flags: {Flags}",
                        Enum.GetValues(typeof(Flag))
                            .Cast<Flag>()
                            .Where(f => f != Flag.None && CurrentStatus.Flags.HasFlag(f))
                            .Select(f => f.ToString())
                            .ToList());

                    if (CurrentStatus.Flags2 != 0)
                    {
                        Log.Debug("Raw Status Flags2 value: 0x{RawFlags2:X8} ({RawFlags2})",
                            CurrentStatus.Flags2, CurrentStatus.Flags2);
                    }
                }
                else
                {
                    Log.Warning("Status.json loaded but Flags property is null");
                }

                // Notify dependent properties
                OnPropertyChanged(nameof(FuelMain));
                OnPropertyChanged(nameof(CurrentShip));
                OnPropertyChanged(nameof(Balance));
                OnPropertyChanged(nameof(CurrentSystem));
                OnPropertyChanged(nameof(CommanderName));
                OnPropertyChanged(nameof(FuelReserve));
                // Publish to MQTT if enabled
                PublishStatusToMqtt(CurrentStatus);
            }

            return changed;
        }

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
            }

            // Force an immediate MQTT update with explicit docking state
            if (previousDockingState != IsDocking || eventType == "Docked")
            {
                Log.Debug("Docking state changed from {Previous} to {Current} - forcing MQTT update",
                    previousDockingState, IsDocking);
                Task.Run(async () =>
                {
                    await MqttService.Instance.PublishFlagStatesAsync(CurrentStatus, IsDocking, forcePublish: true);
                });
            }
        }

        private void ProcessLegalStateEvent(JsonElement root, string eventType)
        {
            try
            {
                // Different events have different ways to get the legal status
                switch (eventType)
                {
                    case "Status":
                        // Status.json flag for legal status
                        if (root.TryGetProperty("LegalState", out var legalStateProp))
                        {
                            LegalState = legalStateProp.GetString() ?? "Clean";
                            Log.Debug("Legal state from Status.json: {0}", LegalState);
                        }
                        break;

                    case "Docked":
                        // When docked, reset to "Clean" unless explicitly told otherwise
                        if (root.TryGetProperty("Wanted", out var wantedProp) && wantedProp.GetBoolean())
                        {
                            LegalState = "Wanted";
                        }
                        else
                        {
                            LegalState = "Clean";
                        }
                        if (root.TryGetProperty("StationName", out var stationProp))
                        {
                            CurrentStationName = stationProp.GetString();

                            // Check specifically for Fleet Carrier station type
                            bool isCarrier = false;
                            if (root.TryGetProperty("StationType", out var stationTypeProp))
                            {
                                string stationType = stationTypeProp.GetString();
                                isCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);

                                Log.Debug("Docked at station: {Station}, StationType: {Type}, IsCarrier: {IsCarrier}",
                                    CurrentStationName, stationType, isCarrier);
                            }

                            // Only set if true or if we're sure it's not a carrier
                            if (isCarrier || stationTypeProp.ValueKind != JsonValueKind.Undefined)
                            {
                                IsOnFleetCarrier = isCarrier;
                            }
                        }
                        break;

                    case "FactionKillBond":
                    case "Bounty":
                        // These are activities against wanted ships
                        LegalState = "Clean"; // Reaffirm we're clean
                        break;

                    case "CommitCrime":
                        // Process different crime types
                        if (root.TryGetProperty("CrimeType", out var crimeTypeProp))
                        {
                            string crimeType = crimeTypeProp.GetString();
                            switch (crimeType?.ToLower())
                            {
                                case "assault":
                                case "murder":
                                case "piracy":
                                    LegalState = "Wanted";
                                    break;

                                case "speeding":
                                    LegalState = "Speeding";
                                    break;

                                case "illegalcargo":
                                    LegalState = "IllegalCargo";
                                    break;

                                default:
                                    LegalState = "Wanted"; // Default for other crimes
                                    break;
                            }
                            Log.Debug("Legal state changed due to crime: {0}", LegalState);
                        }
                        break;

                    case "FactionAllianceChanged":
                        if (root.TryGetProperty("Status", out var statusProp))
                        {
                            string status = statusProp.GetString();
                            if (status?.ToLower() == "hostile")
                            {
                                LegalState = "Hostile";
                            }
                            else if (status?.ToLower() == "allied")
                            {
                                LegalState = "Allied";
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing legal state from event {0}", eventType);
            }
        }

        private async void PublishStatusToMqtt(StatusJson status)
        {
            if (!_mqttInitialized || status == null)
                return;

            try
            {
                // Force publish with the current docking state
                await MqttService.Instance.PublishFlagStatesAsync(status, IsDocking, forcePublish: true);

                // Also publish commander status if we have the data
                if (!string.IsNullOrEmpty(CommanderName) && !string.IsNullOrEmpty(CurrentSystem))
                {
                    string shipInfo = !string.IsNullOrEmpty(ShipLocalised) ? ShipLocalised :
                                     !string.IsNullOrEmpty(ShipName) ? ShipNameHelper.GetLocalisedName(ShipName) : "Unknown";

                    await MqttService.Instance.PublishCommanderStatusAsync(
                         CommanderName,
                         CurrentSystem,
                         shipInfo,
                         Balance ?? 0,              // Use Balance property for credits
                         FuelMain,
                         FuelReserve);                // Use the new FuelMain property
                                                      // fuel level
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing status to MQTT");
            }
        }

        private void SaveAllColonizationData()
        {
            try
            {
                var activeDepots = GetActiveColonizationDepots();
                if (!activeDepots.Any())
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(ColonizationDataFile));

                string json = JsonSerializer.Serialize(activeDepots, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ColonizationDataFile, json);

                Log.Information("Saved {Count} colonization depots to file", activeDepots.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data");
            }
        }

        private void SaveCarrierCargoToDisk()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(CarrierCargoFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_carrierCargo, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(CarrierCargoFilePath, json);
                Log.Debug("Saved {Count} carrier cargo items to disk", _carrierCargo.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save carrier cargo to disk");
            }
        }
        private void SaveColonizationData()
        {
            try
            {
                if (CurrentColonization == null)
                    return;

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(ColonizationDataFile));

                // Serialize and save
                string json = JsonSerializer.Serialize(CurrentColonization, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ColonizationDataFile, json);

                Log.Information("Saved colonization data to file: Progress={Progress:P2}, Resources={Count}",
                    CurrentColonization.ConstructionProgress,
                    CurrentColonization.ResourcesRequired?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving colonization data");
            }
        }

        private void SaveRouteProgress()
        {
            try
            {
                string json = JsonSerializer.Serialize(_routeProgress, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(RouteProgressFile, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save RouteProgress.json");
            }
        }

        private void ScanJournalForPendingCarrierJump()
        {
            try
            {
                var journalFiles = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime);

                DateTime? latestDeparture = null;
                string system = null;
                string body = null;
                bool jumpCompleted = false;

                foreach (var path in journalFiles)
                {
                    using var sr = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("event", out var eventProp)) continue;
                        string eventType = eventProp.GetString();

                        // Check for carrier jumps
                        if (eventType == "CarrierJumpRequest")
                        {
                            if (root.TryGetProperty("DepartureTime", out var dtProp) &&
                                DateTime.TryParse(dtProp.GetString(), out var dt) &&
                                dt > DateTime.UtcNow) // only care about future jumps
                            {
                                // Always pick the latest valid one
                                if (latestDeparture == null || dt > latestDeparture)
                                {
                                    latestDeparture = dt;

                                    if (root.TryGetProperty("SystemName", out var sysName))
                                        system = sysName.GetString();

                                    if (root.TryGetProperty("Body", out var bodyName))
                                        body = bodyName.GetString();
                                }
                            }
                        }
                        else if (eventType == "CarrierJump" || eventType == "CarrierJumpCancelled")
                        {
                            // If we find a completed jump after our potential jump request
                            if (latestDeparture != null &&
                                root.TryGetProperty("timestamp", out var timestamp) &&
                                DateTime.TryParse(timestamp.GetString(), out var eventTime) &&
                                eventTime > latestDeparture)
                            {
                                jumpCompleted = true;
                                break;
                            }
                        }
                    }

                    if (jumpCompleted || latestDeparture != null) break; // found conclusive evidence
                }

                if (latestDeparture != null && !jumpCompleted)
                {
                    // Only set jump in progress if we found a request without a completion
                    FleetCarrierJumpTime = latestDeparture;
                    CarrierJumpScheduledTime = latestDeparture;
                    CarrierJumpDestinationSystem = system;
                    CarrierJumpDestinationBody = body;
                    FleetCarrierJumpInProgress = true;
                    JumpArrived = false;

                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // Force re-eval
                    Log.Information("Recovered scheduled CarrierJump to {System}, {Body} at {Time}",
                        system, body, latestDeparture);
                }
                else if (jumpCompleted)
                {
                    Log.Information("Found evidence of a completed jump in journal - not setting jump state");
                    FleetCarrierJumpInProgress = false;
                    JumpArrived = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan journal for CarrierJumpRequest on startup");
            }
        }

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

        private void SetupFileWatcher(string fileName, Func<bool> loadMethod)
        {
            try
            {
                if (SettingsManager.Load().DevelopmentMode)
                {
                    string filePath = Path.Combine(gamePath, fileName);
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllText(filePath, "{}");
                        Log.Debug("Created empty development file: {File}", filePath);
                    }
                }
                var watcher = new FileSystemWatcher(gamePath)
                {
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                // Use debounced approach to handle multiple events in quick succession
                var debounceTimer = new System.Timers.Timer(100) { AutoReset = false };
                bool pendingUpdate = false;

                debounceTimer.Elapsed += (s, e) =>
                {
                    if (pendingUpdate)
                    {
                        pendingUpdate = false;
                        try
                        {
                            loadMethod();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Error loading {fileName}");
                        }
                    }
                };

                watcher.Changed += (s, e) =>
                {
                    pendingUpdate = true;
                    debounceTimer.Stop();
                    debounceTimer.Start();
                };

                watcher.Created += (s, e) =>
                {
                    pendingUpdate = true;
                    debounceTimer.Stop();
                    debounceTimer.Start();
                };

                _watchers.Add(watcher);
                Log.Debug($"Set up file system watcher for {fileName}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error setting up watcher for {fileName}");
            }
        }

        private void SetupJournalWatcher()
        {
            try
            {
                latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(latestJournalPath))
                {
                    Log.Warning("📖 No journal files found in {Path}", gamePath);
                    return;
                }

                Log.Information("📖 Monitoring journal: {Journal}", Path.GetFileName(latestJournalPath));

                var dirWatcher = new FileSystemWatcher(gamePath)
                {
                    Filter = "Journal.*.log",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                // Faster timer for real-time colonization updates
                var journalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };

                bool pendingUpdate = false;
                DateTime lastUpdate = DateTime.MinValue;

                journalTimer.Tick += async (s, e) =>
                {
                    if (pendingUpdate && DateTime.UtcNow - lastUpdate > TimeSpan.FromMilliseconds(100))
                    {
                        pendingUpdate = false;
                        lastUpdate = DateTime.UtcNow;

                        try
                        {
                            await ProcessJournalAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "📖 Error in journal timer");
                        }
                    }
                };

                journalTimer.Start();

                dirWatcher.Changed += (s, e) =>
                {
                    if (Path.GetFileName(e.FullPath) == Path.GetFileName(latestJournalPath))
                    {
                        pendingUpdate = true;
                    }
                };

                dirWatcher.Created += (s, e) =>
                {
                    if (Path.GetFileName(e.FullPath).StartsWith("Journal.") &&
                        File.GetLastWriteTime(e.FullPath) > File.GetLastWriteTime(latestJournalPath))
                    {
                        latestJournalPath = e.FullPath;
                        lastJournalPosition = 0;
                        pendingUpdate = true;
                        Log.Information("📖 Switched to new journal: {Journal}", Path.GetFileName(latestJournalPath));
                    }
                };

                _watchers.Add(dirWatcher);
                Log.Information("📖 Journal monitoring active");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "📖 Failed to setup journal watcher");
            }
        }

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

                    Log.Information("📦 UpdateCarrierCargo: {Name} = {Count}", name, count);
                    OnPropertyChanged(nameof(CarrierCargo));
                }
            }
        }

        // In GameStateService.cs - Update UpdateCurrentCarrierCargoFromDictionary
        private void UpdateCurrentCarrierCargoFromDictionary()
        {
            try
            {
                // First, standardize the dictionary to use internal names only
                var standardizedCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in CarrierCargo)
                {
                    // Check if this key is already an internal name or a display name
                    string internalName = pair.Key;

                    // If this looks like a display name, try to find the internal name
                    var possibleInternal = CarrierCargo.Keys.FirstOrDefault(k =>
                        !string.Equals(k, pair.Key, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(CommodityMapper.GetDisplayName(k), pair.Key, StringComparison.OrdinalIgnoreCase));

                    if (possibleInternal != null)
                    {
                        // Found the internal name, use it instead
                        internalName = possibleInternal;
                    }

                    // Add or update with the standardized internal name
                    if (standardizedCargo.TryGetValue(internalName, out int existingQty))
                    {
                        standardizedCargo[internalName] = existingQty + pair.Value;
                        Log.Warning("Merged duplicate entries for {Internal}: {Total}", internalName, existingQty + pair.Value);
                    }
                    else
                    {
                        standardizedCargo[internalName] = pair.Value;
                    }
                }

                // Update our dictionary to the standardized version
                CarrierCargo = standardizedCargo;

                // Now create the UI list with display names
                var items = new List<CarrierCargoItem>();

                foreach (var pair in CarrierCargo.Where(kv => kv.Value > 0))
                {
                    items.Add(new CarrierCargoItem
                    {
                        Name = CommodityMapper.GetDisplayName(pair.Key),
                        Quantity = pair.Value
                    });
                }

                var sortedItems = items.OrderByDescending(i => i.Quantity).ToList();
                CurrentCarrierCargo = sortedItems;

                Log.Information("UpdateCurrentCarrierCargoFromDictionary: Updated with {Count} items",
                    sortedItems.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating CurrentCarrierCargo from dictionary");
            }
        }

        #endregion Private Methods
        public class ManualCargoChange
        {
            public string ItemName { get; set; }
            public int ManualQuantity { get; set; }
            public int OriginalGameQuantity { get; set; }
            public DateTime LastModified { get; set; }
            public bool IsActive { get; set; } = true;
        }
        #region Private Classes

        /// <summary>
        /// Helper class to manage batch update scope
        /// </summary>
        private class UpdateScope : IDisposable
        {
            #region Private Fields

            private readonly GameStateService _service;

            #endregion Private Fields

            #region Public Constructors

            public UpdateScope(GameStateService service)
            {
                _service = service;
            }

            #endregion Public Constructors

            #region Public Methods

            public void Dispose()
            {
                _service._isUpdating = false;
                _service.SendPendingNotifications();
            }

            #endregion Public Methods
        }

        #endregion Private Classes
    }
}