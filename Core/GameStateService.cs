using EliteInfoPanel.Core.EliteInfoPanel.Core;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Windows.Media;
using EliteInfoPanel.Util;

namespace EliteInfoPanel.Core
{
    public class GameStateService
    {
        #region Private Fields
        private static readonly SolidColorBrush CountdownRedBrush = new SolidColorBrush(Colors.Red);
        private static readonly SolidColorBrush CountdownGoldBrush = new SolidColorBrush(Colors.Gold);
        private static readonly SolidColorBrush CountdownGreenBrush = new SolidColorBrush(Colors.Green);
        private bool _isInitializing = true;
        public string CurrentStationName { get; private set; }
        private string gamePath;
        private long lastJournalPosition = 0;
        private string latestJournalPath;
        private FileSystemWatcher watcher;
        private bool _firstLoadCompleted = false;
        public bool FirstLoadCompleted => _firstLoadCompleted;

        public DateTime? CarrierJumpScheduledTime { get; private set; }
        public bool FleetCarrierJumpInProgress { get; private set; }

        #endregion

        #region Public Constructors

        public GameStateService(string path)
        {
            gamePath = path;

            // Watcher specifically for Status.json
            var watcher = new FileSystemWatcher(gamePath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                Filter = "*.*", // watch all files in the folder
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            watcher.Changed += (s, e) =>
            {
                // Normalize file name (some systems include full path)
                string fileName = Path.GetFileName(e.Name);

              //  Log.Debug("FileSystemWatcher triggered for: {File}", fileName);

                switch (fileName)
                {
                    case "Status.json":
                        LoadStatusData();
                        break;
                    case "NavRoute.json":
                        LoadNavRouteData();
                        break;
                    case "Cargo.json":
                        LoadCargoData();
                        break;
                    case "Backpack.json":
                        LoadBackpackData();
                        break;
                    case "FCMaterials.json":
                        LoadMaterialsData();
                        break;
                    default:
                        return; // Unwatched file
                }

                DataUpdated?.Invoke();
            };
            // Initial load
            LoadAllData();
            

            // Background refresh loop (optional safety measure)
            Task.Run(async () =>
            {
                while (true)
                {
                    LoadAllData();
                    await ProcessJournalAsync();
                  
                    DataUpdated?.Invoke();
                    await Task.Delay(4000);
                }
            });
            ScanJournalForPendingCarrierJump();
        }


        #endregion

        #region Public Events

        public event Action DataUpdated;

        #endregion

        #region Public Properties
        public string? HyperspaceDestination { get; private set; }
        public string? HyperspaceStarClass { get; private set; }
        public bool IsHyperspaceJumping { get; private set; }
        public long? Balance => CurrentStatus?.Balance;
        public string? CarrierJumpDestinationBody { get; private set; }
        public string? CarrierJumpDestinationSystem { get; private set; }
        public string CommanderName { get; private set; }
        public BackpackJson CurrentBackpack { get; private set; }
        public CargoJson CurrentCargo { get; private set; }
        public LoadoutJson CurrentLoadout { get; internal set; }
        public FCMaterialsJson CurrentMaterials { get; private set; }
        public NavRouteJson CurrentRoute { get; private set; }
        public StatusJson CurrentStatus { get; private set; }
        public string CurrentSystem { get; private set; }
        public DateTime? FleetCarrierJumpTime { get; private set; }
        public bool IsDocking { get; private set; }
        public TimeSpan? JumpCountdown => FleetCarrierJumpTime.HasValue ? FleetCarrierJumpTime.Value.ToLocalTime() - DateTime.Now : null;
        public string ShipLocalised { get; private set; }
        public string ShipName { get; private set; }
        public string SquadronName { get; private set; }
        public string UserShipId { get; set; }
        public string UserShipName { get; set; }
        public bool FleetCarrierJumpArrived { get; private set; }
        public bool RouteCompleted => CurrentRoute?.Route?.Count == 0;
        private bool routeWasActive = false;
        public event Action<bool, string>? HyperspaceJumping;
        private bool isInHyperspace = false;
        public bool IsInHyperspace => isInHyperspace;

       
        public bool RouteWasActive => routeWasActive;

        public int? RemainingJumps { get; private set; }
        public string LastFsdTargetSystem { get; private set; }


        #endregion

        #region Public Methods

        // In GameStateService.cs
        private void RaiseDataUpdated()
        {

            if (DataUpdated == null) return;

            if (System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
            {
                DataUpdated.Invoke();
            }
            else
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.DataBind,
                    new Action(() => DataUpdated.Invoke()));
            }
        }
        public void ResetFleetCarrierJumpFlag()
        {
            FleetCarrierJumpArrived = false;
        }
        public void SetDockingStatus()
        {
           // Log.Debug("GameStateService: SetDockingStatus triggered");
            IsDocking = true;
            RaiseDataUpdated();

            Task.Run(async () =>
            {
                await Task.Delay(10000); // 10 seconds
                IsDocking = false;
                RaiseDataUpdated();
            });
        }
        public void UpdateLoadout(LoadoutJson loadout)
        {
            CurrentLoadout = loadout;
        }

        #endregion
     
        #region Private Methods
        private void PruneCompletedRouteSystems()
        {
            if (CurrentRoute?.Route?.Count == 0)
            {
                routeWasActive = false;
            }

            if (CurrentRoute?.Route == null || string.IsNullOrWhiteSpace(CurrentSystem))
                return;

            int index = CurrentRoute.Route.FindIndex(r =>
                string.Equals(r.StarSystem, CurrentSystem, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
              //  Log.Debug("Pruning route up to and including current system: {System}", CurrentSystem);
                CurrentRoute.Route = CurrentRoute.Route.Skip(index + 1).ToList();
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
                    Debug.WriteLine($"Failed to deserialize {filePath}: {ex}");
                    return null;
                }
            }

            return null;
        }
        public void ResetRouteActivity()
        {
            routeWasActive = false;
        }

        private void LoadStatusData()
        {
            CurrentStatus = DeserializeJsonFile<StatusJson>(Path.Combine(gamePath, "Status.json"));
        }

        private void LoadNavRouteData()
        {
            CurrentRoute = DeserializeJsonFile<NavRouteJson>(Path.Combine(gamePath, "NavRoute.json"));
            if (CurrentRoute?.Route == null || CurrentRoute.Route.Count == 0)
            {
                CurrentRoute = null;
                RemainingJumps = null;
            }
        }

        private void LoadCargoData()
        {
            CurrentCargo = DeserializeJsonFile<CargoJson>(Path.Combine(gamePath, "Cargo.json"));
        }

        private void LoadBackpackData()
        {
            CurrentBackpack = DeserializeJsonFile<BackpackJson>(Path.Combine(gamePath, "Backpack.json"));
        }

        private void LoadMaterialsData()
        {
            CurrentMaterials = DeserializeJsonFile<FCMaterialsJson>(Path.Combine(gamePath, "FCMaterials.json"));
        }

        private void LoadLoadoutData()
        {
            // Typically loaded from journal event; include here only if you have a Loadout.json
            CurrentLoadout = DeserializeJsonFile<LoadoutJson>(Path.Combine(gamePath, "Loadout.json"));
        }

        private void LoadAllData()
        {
            LoadStatusData();
            LoadNavRouteData();
            LoadCargoData();
            LoadBackpackData();
            LoadMaterialsData();
            // Include additional calls if other JSON files exist
            //  LoadLoadoutData(); // Optional, based on your scenario
            latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
         .OrderByDescending(File.GetLastWriteTime)
         .FirstOrDefault();
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

                    switch (eventType)
                    {
                        case "Commander":
                            var cmdr = JsonSerializer.Deserialize<JournalEntry>(line);
                            CommanderName = cmdr?.Name ?? CommanderName;
                            break;

                        case "LoadGame":
                            var loadGame = JsonSerializer.Deserialize<JournalEntry>(line);
                            ShipLocalised = loadGame?.Ship_Localised ?? ShipLocalised;
                            ShipName = loadGame?.Ship ?? ShipName;
                            break;
                        // In your GameStateService.cs, find the ProcessJournalAsync method
                        // Add a case for "ShipyardSwap" in your switch statement

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
                                LoadAllData();
                            }
                            break;
                        case "SetUserShipName":
                            if (root.TryGetProperty("Ship", out var setShipTypeProperty) &&
                                root.TryGetProperty("ShipID", out var setShipIdProperty))
                            {
                                string shipType = setShipTypeProperty.GetString();
                                int shipId = setShipIdProperty.GetInt32();

                                string? userShipName = root.TryGetProperty("UserShipName", out var nameProp) ? nameProp.GetString() : null;
                                string? userShipId = root.TryGetProperty("UserShipId", out var idProp) ? idProp.GetString() : null;

                                Log.Information("Received ship name info for {Ship}: {UserShipName} [{UserShipId}]", shipType, userShipName, userShipId);

                                ShipName = shipType;
                                UserShipName = userShipName;
                                UserShipId = userShipId;
                            }
                            break;


                        case "Loadout":
                            var loadout = JsonSerializer.Deserialize<LoadoutJson>(line);
                            Log.Debug("Updating fuel: FuelMain={0}, FuelReservoir={1}, Max={2}");
                            if (loadout != null)
                            {
                                CurrentLoadout = loadout;
                                UserShipName = loadout.ShipName;
                                UserShipId = loadout.ShipIdent;
                                Log.Debug("Shipname " + loadout.ShipName);
                                // Log.Debug("Assigned Loadout from journal: {Ship} with {Modules} modules", loadout.Ship, loadout.Modules?.Count ?? 0);
                            }
                            break;

                        case "CarrierCancelJump":
                          //  Log.Debug("Carrier jump was cancelled — clearing jump state");
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
                            if (!suppressUIUpdates)
                                RaiseDataUpdated();
                            // force re-check system after jump
                            if (root.TryGetProperty("StarSystem", out var carrierSystem))
                            {
                                CurrentSystem = carrierSystem.GetString();
                                Log.Debug("Updated CurrentSystem from CarrierLocation: {System}", CurrentSystem);
                                if (!suppressUIUpdates)
                                    RaiseDataUpdated();
                            }
                            break;



                        case "CarrierJumpRequest":
                            if (root.TryGetProperty("DepartureTime", out var departureTimeProp) &&
                                DateTime.TryParse(departureTimeProp.GetString(), out var departureTime))
                            {
                                if (departureTime > DateTime.UtcNow)
                                {
                                    FleetCarrierJumpTime = departureTime;
                                    CarrierJumpScheduledTime = departureTime;
                                    CarrierJumpDestinationSystem = root.TryGetProperty("SystemName", out var sysName) ? sysName.GetString() : null;
                                    CarrierJumpDestinationBody = root.TryGetProperty("Body", out var bodyName) ? bodyName.GetString() : null;
                                  
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
                                if (!suppressUIUpdates)
                                    RaiseDataUpdated();

                            }

                            else
                            {
                              //  Log.Debug("CarrierJump event seen — jump still in progress.");
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
                                    IsHyperspaceJumping = true;

                                    if (root.TryGetProperty("StarSystem", out var starSystem))
                                        HyperspaceDestination = starSystem.GetString();

                                    if (root.TryGetProperty("StarClass", out var starClass))
                                        HyperspaceStarClass = starClass.GetString();

                                    isInHyperspace = true;
                                    HyperspaceJumping?.Invoke(true, HyperspaceDestination); // ✅ fire event
                                }
                                else if (jumpTypeString == "Supercruise")
                                {
                                    Log.Information("Supercruise initiated");
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

                                if (!suppressUIUpdates)
                                    RaiseDataUpdated();
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

                            if (isInHyperspace)
                            {
                                isInHyperspace = false;
                                HyperspaceJumping?.Invoke(false, ""); // ✅ notify view model
                            }
                            break;


                        case "SupercruiseEntry":
                            Log.Information("Entered supercruise");
                            // Ensure we're not in hyperspace jump mode when entering supercruise
                            IsHyperspaceJumping = false;
                            break;

                        case "Location":
                            if (isInHyperspace)
                            {
                                isInHyperspace = false;
                                HyperspaceJumping?.Invoke(false, "");
                            }

                            if (root.TryGetProperty("StarSystem", out JsonElement locationSystemElement))
                            {
                                CurrentSystem = locationSystemElement.GetString();
                                PruneCompletedRouteSystems();
                            }
                            break;

                        case "SupercruiseExit":
                            if (isInHyperspace)
                            {
                                isInHyperspace = false;
                                HyperspaceJumping?.Invoke(false, "");
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
                                   // Log.Debug("JournalWatcher: 'Docking request granted' detected.");
                                    SetDockingStatus();
                                }
                            }
                            break;



                    }
                }
                if (!_firstLoadCompleted)
                {
                    _firstLoadCompleted = true;
                    Log.Information("✅ First journal scan completed, raising final UI update");
                    RaiseDataUpdated();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error processing journal file");
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


        #endregion
    }
}
