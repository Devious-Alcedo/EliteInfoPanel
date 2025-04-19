using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            Log.Information("🚨 SetProperty fired for: {Property}", propertyName); // 👈 TEMP LOG

            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        #region Private Fields
        private static readonly SolidColorBrush CountdownRedBrush = new SolidColorBrush(Colors.Red);
        private static readonly SolidColorBrush CountdownGoldBrush = new SolidColorBrush(Colors.Gold);
        private static readonly SolidColorBrush CountdownGreenBrush = new SolidColorBrush(Colors.Green);

        private string _lastVisitedSystem;
        private const string RouteProgressFile = "RouteProgress.json";
        private RouteProgressState _routeProgress = new();
        private bool _isRouteLoaded = false;
        private bool _isInitializing = true;
        private string _currentStationName;
        private string gamePath;
        private long lastJournalPosition = 0;
        private string latestJournalPath;
        private List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private bool _firstLoadCompleted = false;
        private (double X, double Y, double Z)? _currentSystemCoordinates;
        private double _maxJumpRange;
        private DateTime? _carrierJumpScheduledTime;
        private bool _fleetCarrierJumpInProgress;
        private StatusJson _currentStatus;
        private string _commanderName;
        private BackpackJson _currentBackpack;
        private CargoJson _currentCargo;
        private LoadoutJson _currentLoadout;
        private FCMaterialsJson _currentMaterials;
        private NavRouteJson _currentRoute;
        private string _currentSystem;
        private DateTime? _fleetCarrierJumpTime;
        private bool _isDocking;
        private string _shipLocalised;
        private string _shipName;
        private string _squadronName;
        private string _userShipId;
        private string _userShipName;
        private bool _fleetCarrierJumpArrived;
        private bool _routeWasActive = false;
        private bool _isInHyperspace = false;
        private int? _remainingJumps;
        private string _lastFsdTargetSystem;
        private string _hyperspaceDestination;
        private string _hyperspaceStarClass;
        private bool _isHyperspaceJumping;
        private string _carrierJumpDestinationBody;
        private string _carrierJumpDestinationSystem;
        #endregion

        #region Public Properties
        public (double X, double Y, double Z)? CurrentSystemCoordinates
        {
            get => _currentSystemCoordinates;
            set => SetProperty(ref _currentSystemCoordinates, value);
        }

        public bool FirstLoadCompleted => _firstLoadCompleted;

        public double MaxJumpRange
        {
            get => _maxJumpRange;
            private set => SetProperty(ref _maxJumpRange, value);
        }

        public DateTime? CarrierJumpScheduledTime
        {
            get => _carrierJumpScheduledTime;
            private set => SetProperty(ref _carrierJumpScheduledTime, value);
        }

        public bool FleetCarrierJumpInProgress
        {
            get => _fleetCarrierJumpInProgress;
            private set => SetProperty(ref _fleetCarrierJumpInProgress, value);
        }

        public string LastVisitedSystem
        {
            get => _lastVisitedSystem;
            private set => SetProperty(ref _lastVisitedSystem, value);
        }

        public string CurrentStationName
        {
            get => _currentStationName;
            private set => SetProperty(ref _currentStationName, value);
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

        public bool IsHyperspaceJumping
        {
            get => _isHyperspaceJumping;
            private set
            {
                if (SetProperty(ref _isHyperspaceJumping, value))
                {
                    HyperspaceJumping?.Invoke(value, HyperspaceDestination);
                }
            }
        }

        public long? Balance => CurrentStatus?.Balance;

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
            internal set => SetProperty(ref _currentLoadout, value);
        }

        public FCMaterialsJson CurrentMaterials
        {
            get => _currentMaterials;
            private set => SetProperty(ref _currentMaterials, value);
        }

        public NavRouteJson CurrentRoute
        {
            get => _currentRoute;
            private set => SetProperty(ref _currentRoute, value);
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

        public DateTime? FleetCarrierJumpTime
        {
            get => _fleetCarrierJumpTime;
            private set => SetProperty(ref _fleetCarrierJumpTime, value);
        }

        public bool IsDocking
        {
            get => _isDocking;
            private set => SetProperty(ref _isDocking, value);
        }

        public TimeSpan? JumpCountdown => FleetCarrierJumpTime.HasValue ?
            FleetCarrierJumpTime.Value.ToLocalTime() - DateTime.Now : null;

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

        public string SquadronName
        {
            get => _squadronName;
            private set => SetProperty(ref _squadronName, value);
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

        public bool FleetCarrierJumpArrived
        {
            get => _fleetCarrierJumpArrived;
            private set => SetProperty(ref _fleetCarrierJumpArrived, value);
        }

        public bool RouteCompleted => CurrentRoute?.Route?.Count == 0;

        public bool RouteWasActive => _routeWasActive;

        public bool IsInHyperspace => _isInHyperspace;

        public int? RemainingJumps
        {
            get => _remainingJumps;
            private set => SetProperty(ref _remainingJumps, value);
        }

        public string LastFsdTargetSystem
        {
            get => _lastFsdTargetSystem;
            private set => SetProperty(ref _lastFsdTargetSystem, value);
        }
        #endregion

        #region Events
        // Event for hyperspace jump notification
        public event Action<bool, string> HyperspaceJumping;
        public event Action LoadoutUpdated;


        #endregion

        #region Constructor
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
        #endregion

        #region Public Methods
        public void ResetFleetCarrierJumpFlag()
        {
            FleetCarrierJumpArrived = false;
        }

        public void SetDockingStatus()
        {
            IsDocking = true;

            Task.Run(async () =>
            {
                await Task.Delay(10000); // 10 seconds
                IsDocking = false;
            });
        }

        public void UpdateLoadout(LoadoutJson loadout)
        {
            CurrentLoadout = loadout;
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
            }
        }

        public void ResetRouteActivity()
        {
            _routeWasActive = false;
        }
        #endregion

        #region Private Methods
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

        private bool LoadStatusData()
        {
            var newStatus = DeserializeJsonFile<StatusJson>(Path.Combine(gamePath, "Status.json"));
            if (newStatus == null) return false;

            if (CurrentStatus == null || !JsonEquals(CurrentStatus, newStatus))
            {
                CurrentStatus = newStatus;
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

        private bool LoadCargoData()
        {
            var newCargo = DeserializeJsonFile<CargoJson>(Path.Combine(gamePath, "Cargo.json"));
            if (newCargo == null) return false;

            if (CurrentCargo == null || !InventoriesEqual(CurrentCargo.Inventory, newCargo.Inventory))
            {
                CurrentCargo = newCargo;
                return true;
            }

            return false;
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
                return true;
            }

            return false;
        }



        private void LoadAllData()
        {
            LoadStatusData();
            LoadNavRouteData();
            LoadCargoData();
            LoadBackpackData();
            LoadMaterialsData();
            LoadLoadoutData();

            latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();

            LoadRouteProgress();
            OnPropertyChanged(nameof(CurrentLoadout));
            OnPropertyChanged(nameof(CurrentStatus));
            OnPropertyChanged(nameof(CurrentCargo));
            OnPropertyChanged(nameof(CurrentRoute));

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
                                UserShipName = loadout.ShipName;
                                UserShipId = loadout.ShipIdent;
                                LoadoutUpdated?.Invoke();
                            }
                            break;

                        case "CarrierCancelJump":
                            FleetCarrierJumpTime = null;
                            CarrierJumpScheduledTime = null;
                            CarrierJumpDestinationSystem = null;
                            CarrierJumpDestinationBody = null;
                            FleetCarrierJumpInProgress = false;
                            break;

                        case "CarrierLocation":
                            Log.Debug("CarrierLocation seen — clearing any jump state");

                            FleetCarrierJumpTime = null;
                            CarrierJumpScheduledTime = null;
                            CarrierJumpDestinationSystem = null;
                            CarrierJumpDestinationBody = null;

                            FleetCarrierJumpArrived = true;
                            FleetCarrierJumpInProgress = false;

                            bool isOnCarrier = root.TryGetProperty("StationType", out var stationTypeProp) &&
                                               stationTypeProp.GetString() == "FleetCarrier";

                            if (isOnCarrier && root.TryGetProperty("StarSystem", out var carrierSystem))
                            {
                                CurrentSystem = carrierSystem.GetString();
                                Log.Debug("✅ Updated CurrentSystem from CarrierLocation: {System}", CurrentSystem);
                            }
                            break;

                        case "DockingGranted":
                            Log.Information("Docking granted by station — setting IsDocking = true");
                            SetDockingStatus();
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
                            // Only clear state if we see arrival confirmation
                            if (root.TryGetProperty("Docked", out var dockedProp) && dockedProp.GetBoolean())
                            {
                                FleetCarrierJumpTime = null;
                                CarrierJumpDestinationSystem = null;
                                CarrierJumpDestinationBody = null;
                                FleetCarrierJumpArrived = true;
                                FleetCarrierJumpInProgress = false;
                            }
                            break;

                        case "FSDTarget":
                            if (root.TryGetProperty("RemainingJumpsInRoute", out var jumpsProp))
                                RemainingJumps = jumpsProp.GetInt32();

                            if (root.TryGetProperty("Name", out var fsdNameProp))
                                LastFsdTargetSystem = fsdNameProp.GetString();
                            break;

                        case "StartJump":
                            if (root.TryGetProperty("JumpType", out var jumpType))
                            {
                                string jumpTypeString = jumpType.GetString();
                                if (jumpTypeString == "Hyperspace")
                                {
                                    Log.Information("Hyperspace jump initiated");

                                    if (root.TryGetProperty("StarSystem", out var starSystem))
                                        HyperspaceDestination = starSystem.GetString();

                                    if (root.TryGetProperty("StarClass", out var starClass))
                                        HyperspaceStarClass = starClass.GetString();

                                    _isInHyperspace = true;
                                    IsHyperspaceJumping = true;
                                }
                                else if (jumpTypeString == "Supercruise")
                                {
                                    Log.Debug("Supercruise initiated");
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
                                Log.Debug("Docked at station: {Station}", CurrentStationName);
                            }
                            else
                            {
                                CurrentStationName = null;
                            }
                            break;

                        case "Undocked":
                            CurrentStationName = null;
                            break;

                        case "FSDJump":
                            Log.Information("Hyperspace jump completed");
                            IsHyperspaceJumping = false;
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

                                // Track and persist progress
                                if (!_routeProgress.CompletedSystems.Contains(CurrentSystem))
                                {
                                    _routeProgress.CompletedSystems.Add(CurrentSystem);
                                    _routeProgress.LastKnownSystem = CurrentSystem;
                                    SaveRouteProgress();
                                }

                                PruneCompletedRouteSystems();
                            }

                            _isInHyperspace = false;
                            break;

                        case "SupercruiseEntry":
                            Log.Debug("Entered supercruise");
                            // Ensure we're not in hyperspace jump mode when entering supercruise
                            IsHyperspaceJumping = false;
                            break;

                        case "Location":
                            if (_isInHyperspace)
                            {
                                _isInHyperspace = false;
                                IsHyperspaceJumping = false;
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
                            if (_isInHyperspace)
                            {
                                _isInHyperspace = false;
                                IsHyperspaceJumping = false;
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
                            if (root.TryGetProperty("Message_Localised", out var msgProp))
                            {
                                string msg = msgProp.GetString();
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

                    Log.Information("Recovered scheduled CarrierJump to {System}, {Body} at {Time}",
                        system, body, latestDeparture);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan journal for CarrierJumpRequest on startup");
            }
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

        private bool BackpacksEqual(BackpackJson bp1, BackpackJson bp2)
        {
            if (bp1 == null && bp2 == null) return true;
            if (bp1 == null || bp2 == null) return false;

            return ItemListsEqual(bp1.Items, bp2.Items) &&
                   ItemListsEqual(bp1.Components, bp2.Components) &&
                   ItemListsEqual(bp1.Consumables, bp2.Consumables) &&
                   ItemListsEqual(bp1.Data, bp2.Data);
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

        private bool MaterialsEqual(FCMaterialsJson mat1, FCMaterialsJson mat2)
        {
            if (mat1 == null && mat2 == null) return true;
            if (mat1 == null || mat2 == null) return false;

            return FCMaterialItemsEqual(mat1.Items, mat2.Items);
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
        #endregion
    }
}