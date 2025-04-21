using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EliteInfoPanel.Core.EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.Core
{
    public class GameStateService : INotifyPropertyChanged
    {

        #region Private Fields

        private const string RouteProgressFile = "RouteProgress.json";

        private static readonly SolidColorBrush CountdownGoldBrush = new SolidColorBrush(Colors.Gold);

        private static readonly SolidColorBrush CountdownGreenBrush = new SolidColorBrush(Colors.Green);

        private static readonly SolidColorBrush CountdownRedBrush = new SolidColorBrush(Colors.Red);

        private string _carrierJumpDestinationBody;
        private bool _isCarrierJumping = false;
        private string _carrierJumpDestinationSystem;

        private DateTime? _carrierJumpScheduledTime;

        private string _commanderName;

        private BackpackJson _currentBackpack;

        private CargoJson _currentCargo;

        private LoadoutJson _currentLoadout;

        private FCMaterialsJson _currentMaterials;

        private NavRouteJson _currentRoute;

        private string _currentStationName;

        private StatusJson _currentStatus;

        private string _currentSystem;

        private (double X, double Y, double Z)? _currentSystemCoordinates;

        private CancellationTokenSource _dockingCts = new CancellationTokenSource();

        private bool _firstLoadCompleted = false;

        private bool _fleetCarrierJumpArrived;

        private bool _fleetCarrierJumpInProgress;

        private DateTime? _fleetCarrierJumpTime;

        private string _hyperspaceDestination;

        private string _hyperspaceStarClass;

        private CancellationTokenSource _hyperspaceTimeoutCts;

        private bool _isDocking;

        private bool _isHyperspaceJumping;

        private bool _isInHyperspace = false;

        private bool _isInitializing = true;

        private bool _isOnFleetCarrier;

        private bool _isRouteLoaded = false;

        private bool _jumpArrived;

        // Add this field to track changes
        private int _lastCarrierJumpCountdown = -1;

        private string _lastFsdTargetSystem;

        private string _lastVisitedSystem;

        private string _legalState = "Clean";

        private double _maxJumpRange;

        private int? _remainingJumps;

        private RouteProgressState _routeProgress = new();

        private bool _routeWasActive = false;

        private string _shipLocalised;

        private string _shipName;

        private string _squadronName;

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

            // Scan journal for pending carrier jump
            ScanJournalForPendingCarrierJump();
        }

        #endregion Public Constructors

        #region Public Events

        public event Action FirstLoadCompletedEvent;

        // Event for hyperspace jump notification
        public event Action<bool, string> HyperspaceJumping;

        public event Action LoadoutUpdated;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

        public long? Balance => CurrentStatus?.Balance;

        public int CarrierJumpCountdownSeconds
        {
            get
            {
                if (CarrierJumpScheduledTime.HasValue)
                {
                    var timeLeft = CarrierJumpScheduledTime.Value.ToLocalTime() - DateTime.Now;
                    int result = (int)Math.Max(0, timeLeft.TotalSeconds);

                    // Explicitly set JumpArrived to false when countdown starts
                    if (_lastCarrierJumpCountdown > 0 && result == _lastCarrierJumpCountdown)
                    {
                        JumpArrived = false;
                    }

                    // Store the previous value to detect changes
                    int previousValue = _lastCarrierJumpCountdown;
                    _lastCarrierJumpCountdown = result;

                    // If countdown reached zero, notify ShowCarrierJumpOverlay
                    if (previousValue > 0 && result == 0 && FleetCarrierJumpInProgress && IsOnFleetCarrier)
                    {
                        Log.Information("Carrier jump countdown reached zero - triggering overlay notification");
                        OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                    }

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

        public string CommanderName
        {
            get => _commanderName;
            private set => SetProperty(ref _commanderName, value);
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

        public bool FirstLoadCompleted => _firstLoadCompleted;

        public bool FleetCarrierJumpArrived
        {
            get => _fleetCarrierJumpArrived;
            private set => SetProperty(ref _fleetCarrierJumpArrived, value);
        }

        public bool FleetCarrierJumpInProgress
        {
            get => _fleetCarrierJumpInProgress;
            private set
            {
                Log.Information("📡 FleetCarrierJumpInProgress changed to {0}", value);
                if (SetProperty(ref _fleetCarrierJumpInProgress, value))
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // 👈 notify
            }
        }

        public DateTime? FleetCarrierJumpTime
        {
            get => _fleetCarrierJumpTime;
            private set => SetProperty(ref _fleetCarrierJumpTime, value);
        }

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
            set => SetProperty(ref _jumpArrived, value);
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

        public int? RemainingJumps
        {
            get => _remainingJumps;
            private set => SetProperty(ref _remainingJumps, value);
        }

        public bool RouteCompleted => CurrentRoute?.Route?.Count == 0;

        public bool RouteWasActive => _routeWasActive;

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
        public bool ShowCarrierJumpOverlay
        {
            get
            {
                bool result = FleetCarrierJumpInProgress && IsOnFleetCarrier && CarrierJumpCountdownSeconds <= 0 && !JumpArrived;
                Log.Information("ShowCarrierJumpOverlay: FleetCarrierJumpInProgress={0}, IsOnFleetCarrier={1}, CountdownSeconds={2}, JumpArrived={3}, Result={4}",
                    FleetCarrierJumpInProgress, IsOnFleetCarrier, CarrierJumpCountdownSeconds, JumpArrived, result);
                return result;
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

        public void ResetFleetCarrierJumpFlag()
        {
            FleetCarrierJumpArrived = false;
        }

        public void ResetRouteActivity()
        {
            _routeWasActive = false;
        }

        public void SetDockingStatus()
        {
            // Cancel any existing docking timer
            _dockingCts.Cancel();
            _dockingCts.Dispose();
            _dockingCts = new CancellationTokenSource();

            IsDocking = true;

            var token = _dockingCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(10000, token); // Wait 10 seconds or until cancelled
                    if (!token.IsCancellationRequested)
                    {
                        IsDocking = false;
                    }
                }
                catch (TaskCanceledException)
                {
                    // Ignored—this is expected behavior if canceled+
                    Debug.WriteLine("Docking task was canceled.");
                }
            }, token);
        }

        public void UpdateLoadout(LoadoutJson loadout)
        {
            CurrentLoadout = loadout;
        }

        #endregion Public Methods

        #region Protected Methods

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            Task statusTask = Task.Run(() => LoadStatusData());
            Task routeTask = Task.Run(() => LoadNavRouteData());
            Task cargoTask = Task.Run(() => LoadCargoData());
            Task backpackTask = Task.Run(() => LoadBackpackData());
            Task materialsTask = Task.Run(() => LoadMaterialsData());
            Task loadoutTask = Task.Run(() => LoadLoadoutData());

            Task.WaitAll(statusTask, routeTask, cargoTask, backpackTask, materialsTask, loadoutTask);

            latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
            _firstLoadCompleted = true;
            Log.Information("✅ First journal scan completed");

            FirstLoadCompletedEvent?.Invoke(); // 🔔 raise the event here


            LoadRouteProgress();

            Task.Run(async () => await ProcessJournalAsync()).Wait();
            OnPropertyChanged(nameof(CurrentLoadout));
            OnPropertyChanged(nameof(CurrentCargo));
            OnPropertyChanged(nameof(CurrentRoute));
            OnPropertyChanged(nameof(CommanderName));
            OnPropertyChanged(nameof(ShipName));
            OnPropertyChanged(nameof(CurrentSystem));
            OnPropertyChanged(nameof(Balance));
            OnPropertyChanged(nameof(CurrentStatus));
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // ensure overlay check runs


        }

        private bool LoadBackpackData()
        {
            var newBackpack = DeserializeJsonFile<BackpackJson>(Path.Combine(gamePath, "Backpack.json"));
            if (newBackpack == null) return false;

            if (CurrentBackpack == null || !BackpacksEqual(CurrentBackpack, newBackpack))
            {
                CurrentBackpack = newBackpack;
                return true;
            }

            return false;
        }

        private bool LoadCargoData()
        {
            try
            {
                Log.Debug("GameStateService: Loading cargo data");
                var newCargo = DeserializeJsonFile<CargoJson>(Path.Combine(gamePath, "Cargo.json"));

                if (newCargo == null)
                {
                    Log.Warning("GameStateService: Failed to load Cargo.json or file not found");
                    return false;
                }

                // Check if the inventory is null before proceeding
                if (newCargo.Inventory == null)
                {
                    Log.Warning("GameStateService: Cargo.json loaded but Inventory is null");
                    newCargo.Inventory = new List<CargoJson.CargoItem>(); // Initialize with empty list
                }

                bool hasChanged = false;

                // First check - CurrentCargo might be null (first load)
                if (CurrentCargo == null)
                {
                    Log.Information("GameStateService: First cargo data load - {Count} items",
                        newCargo.Inventory.Count);
                    CurrentCargo = newCargo;
                    hasChanged = true;
                }
                // Second check - compare inventories
                else if (!InventoriesEqual(CurrentCargo.Inventory, newCargo.Inventory))
                {
                    Log.Information("GameStateService: Cargo inventory changed - now {Count} items",
                        newCargo.Inventory.Count);
                    CurrentCargo = newCargo;
                    hasChanged = true;
                }

                // Log specific details about the change if it happened
                if (hasChanged)
                {
                    Log.Debug("GameStateService: Cargo details - {Count} items",
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
            var newMaterials = DeserializeJsonFile<FCMaterialsJson>(Path.Combine(gamePath, "FCMaterials.json"));
            if (newMaterials == null) return false;

            if (CurrentMaterials == null || !MaterialsEqual(CurrentMaterials, newMaterials))
            {
                CurrentMaterials = newMaterials;
                return true;
            }

            return false;
        }

        private bool LoadNavRouteData()
        {
            var loadedRoute = DeserializeJsonFile<NavRouteJson>(Path.Combine(gamePath, "NavRoute.json"));

            if (loadedRoute?.Route == null || loadedRoute.Route.Count == 0)
            {
                if (CurrentRoute != null && (CurrentRoute.Route?.Count ?? 0) > 0)
                {
                    CurrentRoute = null;
                    RemainingJumps = null;
                    return true;
                }
                return false;
            }

            // Check if routes are equivalent
            if (CurrentRoute != null &&
                CurrentRoute.Route.Select(r => r.StarSystem)
                    .SequenceEqual(loadedRoute.Route.Select(r => r.StarSystem), StringComparer.OrdinalIgnoreCase))
            {
                return false; // No change
            }

            CurrentRoute = loadedRoute;
            // Immediately prune jumps already completed
            PruneCompletedRouteSystems();

            return true;
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
            var newStatus = DeserializeJsonFile<StatusJson>(Path.Combine(gamePath, "Status.json"));
            if (newStatus == null) return false;
       //     IsHyperspaceJumping = newStatus.Flags.HasFlag(Flag.FsdJump);

            if (CurrentStatus == null || !JsonEquals(CurrentStatus, newStatus))
            {
                CurrentStatus = newStatus;
                return true;
            }

            return false;
        }

        private bool MaterialsEqual(FCMaterialsJson mat1, FCMaterialsJson mat2)
        {
            if (mat1 == null && mat2 == null) return true;
            if (mat1 == null || mat2 == null) return false;

            return FCMaterialItemsEqual(mat1.Items, mat2.Items);
        }

        private async Task ProcessJournalAsync()
        {
            if (string.IsNullOrEmpty(latestJournalPath))
                return;

            try
            {
                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(lastJournalPosition, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                bool suppressUIUpdates = !_firstLoadCompleted; // true if this is the first pass

                while (!sr.EndOfStream)
                {
                    string line = await sr.ReadLineAsync();
                    lastJournalPosition = fs.Position;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("event", out var eventProp))
                        continue;

                    string eventType = eventProp.GetString();
                    Log.Debug("Processing journal event: {Event}", eventType);

                    switch (eventType)
                    {
                        case "Commander":
                            if (root.TryGetProperty("Name", out var nameProperty))
                            {
                                CommanderName = nameProperty.GetString();
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
                            break;

                        case "ShipyardSwap":
                            if (root.TryGetProperty("ShipType", out var shipTypeProperty))
                            {
                                string shipType = shipTypeProperty.GetString();
                                string shipTypeName = root.TryGetProperty("ShipType_Localised", out var localisedProp) && !string.IsNullOrWhiteSpace(localisedProp.GetString())
                                    ? localisedProp.GetString()
                                    : ShipNameHelper.GetLocalisedName(shipType); // fallback if null or missing

                                ShipName = shipType;
                                ShipLocalised = shipTypeName;

                                Log.Information("Ship changed to: {Type} ({Localised})", shipType, shipTypeName);

                                // Clear current loadout
                                CurrentLoadout = null;
                                LoadLoadoutData();
                            }
                            break;

                        case "SetUserShipName":
                            if (root.TryGetProperty("Ship", out var setShipTypeProperty) &&
                                root.TryGetProperty("ShipID", out var setShipIdProperty))
                            {
                                string shipType = setShipTypeProperty.GetString();
                                int shipId = setShipIdProperty.GetInt32();

                                string userShipName = root.TryGetProperty("UserShipName", out var nameProp) ? nameProp.GetString() : null;
                                string userShipId = root.TryGetProperty("UserShipId", out var idProp) ? idProp.GetString() : null;

                                Log.Information("Received ship name info for {Ship}: {UserShipName} [{UserShipId}]", shipType, userShipName, userShipId);

                                ShipName = shipType;
                                UserShipName = userShipName;
                                UserShipId = userShipId;
                            }
                            break;
                        case "ShipLocker":
                            // Check if the carrier jump was initiated and then completed
                            if (_isCarrierJumping)
                            {
                                Log.Information("Carrier jump completed - carrier has arrived");
                                CarrierJumpDestinationSystem = null;
                                // Reset the flag and trigger overlay logic
                                _isCarrierJumping = false;
                                _jumpArrived = true;
                                // Here you can trigger the carrier jump overlay completion
                                OnCarrierJumpComplete();
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

                                CurrentLoadout = loadout;

                                // explicitly notify UI
                                OnPropertyChanged(nameof(CurrentLoadout));
                                OnPropertyChanged(nameof(CurrentStatus)); // fuel level from status might need updating
                                LoadoutUpdated?.Invoke(); // explicitly notify subscribers
                            }
                            break;

                        case "Status":
                            // Process legal state from Status events
                            ProcessLegalStateEvent(root, "Status");
                            break;

                        case "CarrierCancelJump":
                            FleetCarrierJumpTime = null;
                            CarrierJumpScheduledTime = null;
                            CarrierJumpDestinationSystem = null;
                            CarrierJumpDestinationBody = null;
                            FleetCarrierJumpInProgress = false;
                            OnPropertyChanged(nameof(CarrierJumpDestinationSystem));
                            OnPropertyChanged(nameof(FleetCarrierJumpTime));
                            break;

                        case "CarrierLocation":
                            Log.Debug("CarrierLocation seen — clearing any jump state");

                           

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
                            break;


                        case "DockingGranted":
                            Log.Information("Docking granted by station — setting IsDocking = true");
                            SetDockingStatus();

                            SetProperty(ref _isDocking, true, nameof(IsDocking));
                            break;

                        case "CarrierJumpRequest":
                            if (root.TryGetProperty("DepartureTime", out var departureTimeProp) &&
                                DateTime.TryParse(departureTimeProp.GetString(), out var departureTime))
                            {
                                if (departureTime > DateTime.UtcNow)
                                {
                                    FleetCarrierJumpTime = departureTime;
                                    CarrierJumpScheduledTime = departureTime;

                                    if (root.TryGetProperty("SystemName", out var sysName))
                                        CarrierJumpDestinationSystem = sysName.GetString();

                                    if (root.TryGetProperty("Body", out var bodyName))
                                        CarrierJumpDestinationBody = bodyName.GetString();

                                    FleetCarrierJumpArrived = false;
                                    FleetCarrierJumpInProgress = true;
                                    Log.Debug($"Carrier jump scheduled for {departureTime:u}");
                                }
                                else
                                {
                                    Log.Debug("CarrierJumpRequest ignored — departure time is in the past");
                                }
                            }
                            break;

                        case "CarrierJump":
                            if (root.TryGetProperty("Docked", out var dockedProperty) && dockedProperty.GetBoolean())
                            {
                                // Carrier jump completed, the player has arrived
                                Log.Information("Carrier jump completed, player docked at fleet carrier.");

                                // Mark the jump as completed
                                _isCarrierJumping = true;
                                Log.Information("Carrier jump started");

                            }
                            break;


                        case "FSDTarget":
                            if (root.TryGetProperty("RemainingJumpsInRoute", out var jumpsProp))
                                RemainingJumps = jumpsProp.GetInt32();

                            if (root.TryGetProperty("Name", out var fsdNameProp))
                                LastFsdTargetSystem = fsdNameProp.GetString();

                            break;
                        case "StartJump":
                            if (root.TryGetProperty("JumpType", out var jumpTypeProp))
                            {
                                string jumpType = jumpTypeProp.GetString();

                                if (jumpType == "Hyperspace")
                                {
                                    IsHyperspaceJumping = true;
                                    _isInHyperspace = true;

                                    // ✅ Add this:
                                    if (root.TryGetProperty("StarClass", out var starClassProp))
                                    {
                                        HyperspaceStarClass = starClassProp.GetString();
                                    }
                                    else
                                    {
                                        HyperspaceStarClass = null;
                                    }

                                    EnsureHyperspaceTimeout(); // optional fail-safe
                                }
                                else
                                {
                                    IsHyperspaceJumping = false;
                                    _isInHyperspace = false;
                                }
                            }
                            break;




                        case "CarrierJumpCancelled":
                            // Only process this if a jump was actually scheduled
                            if (FleetCarrierJumpTime != null || CarrierJumpScheduledTime != null)
                            {
                                Log.Information("Carrier jump was cancelled ... clearing jump state");
                                FleetCarrierJumpTime = null;
                                CarrierJumpScheduledTime = null;
                                CarrierJumpDestinationSystem = null;
                                CarrierJumpDestinationBody = null;
                                FleetCarrierJumpInProgress = false;
                            }
                            else
                            {
                                Log.Debug("Ignoring CarrierJumpCancelled as no jump was active.");
                            }
                            break;

                        case "Docked":
                            if (root.TryGetProperty("StationName", out var stationProp))
                            {
                                CurrentStationName = stationProp.GetString();
                                IsOnFleetCarrier = root.TryGetProperty("StationType", out stationTypeProp) &&
                                   stationTypeProp.GetString() == "FleetCarrier";

                                Log.Debug("Docked at station: {Station}", CurrentStationName);
                            }
                            else
                            {
                                CurrentStationName = null;
                            }
                            ProcessLegalStateEvent(root, "Docked");
                            IsDocking = false; // Immediately clear docking state
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
                        // Optional, if you ever see "DockingCancelled"
                        case "DockingCancelled":
                            IsDocking = false; // Immediately clear docking state
                            Log.Debug("Docking cancelled explicitly");
                            break;

                        case "Undocked":
                            CurrentStationName = null;
                            IsOnFleetCarrier = false; // Reset carrier flag when undocking
                            break;

                        case "FSDJump":
                            Log.Information("✅ Hyperspace jump completed");

                            IsHyperspaceJumping = false;
                            _isInHyperspace = false;
                            HyperspaceDestination = null;
                            HyperspaceStarClass = null;

                            if (root.TryGetProperty("StarSystem", out JsonElement systemElement))
                            {
                                string currentSystem = systemElement.GetString();

                                if (!string.Equals(LastVisitedSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
                                {
                                    LastVisitedSystem = currentSystem;
                                }

                                CurrentSystem = currentSystem;

                                // Route tracking
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
                            // For any of these events, we should not be in hyperspace
                            if (IsHyperspaceJumping || _isInHyperspace)
                            {
                                Log.Warning("⚠️ Hyperspace state was still active during {Event} - resetting", eventType);

                                HyperspaceDestination = null;
                                HyperspaceStarClass = null;
                            }
                            break;

                        case "Location":
                            // For any of these events, we should not be in hyperspace
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
                            break;

                        case "SupercruiseExit":
                            // For any of these events, we should not be in hyperspace
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

                        case "SquadronStartup":
                            if (root.TryGetProperty("SquadronName", out var squadron))
                                SquadronName = squadron.GetString();
                            break;

                        case "ReceiveText":
                            string msg = null;

                            if (root.TryGetProperty("Message_Localised", out var msgProp))
                            {
                                msg = msgProp.GetString();
                                if (msg?.Contains("Docking request granted", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    SetDockingStatus();
                                }
                            }


                            break;

                    }
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
        private void OnCarrierJumpComplete()
        {
            // Set the flag to indicate the jump has completed
            FleetCarrierJumpInProgress = false;
            JumpArrived = true;

            // Ensure the overlay is triggered
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // This will re-evaluate the ShowCarrierJumpOverlay property
            Log.Information("Carrier jump completed, overlay should be shown now.");
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

                                Log.Information("Docked at station: {Station}, StationType: {Type}, IsCarrier: {IsCarrier}",
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

                        if (eventProp.GetString() == "CarrierJumpRequest")
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
                    }

                    if (latestDeparture != null) break; // found in newest journal
                }

                if (latestDeparture != null)
                {
                    FleetCarrierJumpTime = latestDeparture;
                    CarrierJumpDestinationSystem = system;
                    CarrierJumpDestinationBody = body;
                    FleetCarrierJumpInProgress = true;
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay)); // ✅ force re-eval

                    Log.Information("Recovered scheduled CarrierJump to {System}, {Body} at {Time}",
                        system, body, latestDeparture);
                }

            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan journal for CarrierJumpRequest on startup");
            }
        }

        private void SetupFileWatcher(string fileName, Func<bool> loadMethod)
        {
            try
            {
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
                // First find the latest journal file
                latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(latestJournalPath))
                {
                    Log.Warning("No journal files found in {Path}", gamePath);
                    return;
                }

                // Set up a watcher for the directory to catch new journal files
                var dirWatcher = new FileSystemWatcher(gamePath)
                {
                    Filter = "Journal.*.log",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                // Use a throttling approach to avoid excessive processing
                DispatcherTimer journalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };

                bool pendingJournalUpdate = false;

                journalTimer.Tick += async (s, e) =>
                {
                    if (pendingJournalUpdate)
                    {
                        pendingJournalUpdate = false;
                        journalTimer.Stop();

                        try
                        {
                            await ProcessJournalAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error processing journal");
                        }

                        journalTimer.Start();
                    }
                };

                journalTimer.Start();

                // Handle changes to the latest journal file
                dirWatcher.Changed += (s, e) =>
                {
                    // Check if this is for our current journal file
                    string changedFile = Path.GetFileName(e.FullPath);
                    string currentFile = Path.GetFileName(latestJournalPath);

                    if (changedFile == currentFile)
                    {
                        pendingJournalUpdate = true;
                    }
                };

                // Handle creation of new journal files
                dirWatcher.Created += (s, e) =>
                {
                    if (Path.GetFileName(e.FullPath).StartsWith("Journal."))
                    {
                        // Check if this is newer than our current journal
                        DateTime newFileTime = File.GetLastWriteTime(e.FullPath);
                        DateTime currentFileTime = File.GetLastWriteTime(latestJournalPath);

                        if (newFileTime > currentFileTime)
                        {
                            latestJournalPath = e.FullPath;
                            lastJournalPosition = 0; // Reset position for new file
                            pendingJournalUpdate = true;
                        }
                    }
                };

                _watchers.Add(dirWatcher);
                Log.Debug("Set up journal watcher for {Path}", gamePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting up journal watcher");
            }
        }

        #endregion Private Methods
    }
}