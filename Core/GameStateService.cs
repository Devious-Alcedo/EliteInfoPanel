using EliteInfoPanel.Core.EliteInfoPanel.Core;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;

namespace EliteInfoPanel.Core
{
    public class GameStateService
    {
        #region Private Fields

        private string gamePath;
        private long lastJournalPosition = 0;
        private string latestJournalPath;
        private FileSystemWatcher watcher;
        #endregion

        #region Public Constructors

        public GameStateService(string path)
        {
            gamePath = path;

            // Watcher specifically for Status.json
            var statusWatcher = new FileSystemWatcher(gamePath, "Status.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            statusWatcher.Changed += (s, e) =>
            {
                LoadStatusData();
                DataUpdated?.Invoke();
            };

            // Watcher specifically for NavRoute.json
            var navRouteWatcher = new FileSystemWatcher(gamePath, "NavRoute.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            navRouteWatcher.Changed += (s, e) =>
            {
                LoadNavRouteData();
                DataUpdated?.Invoke();
            };

            // Watcher specifically for Cargo.json
            var cargoWatcher = new FileSystemWatcher(gamePath, "Cargo.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            cargoWatcher.Changed += (s, e) =>
            {
                LoadCargoData();
                DataUpdated?.Invoke();
            };

            // Watcher specifically for Backpack.json
            var backpackWatcher = new FileSystemWatcher(gamePath, "Backpack.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            backpackWatcher.Changed += (s, e) =>
            {
                LoadBackpackData();
                DataUpdated?.Invoke();
            };

            // Watcher specifically for FCMaterials.json (if needed)
            var materialsWatcher = new FileSystemWatcher(gamePath, "FCMaterials.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            materialsWatcher.Changed += (s, e) =>
            {
                LoadMaterialsData();
                DataUpdated?.Invoke();
            };

            // General watcher for Journal logs
            watcher = new FileSystemWatcher(gamePath, "Journal.*.log")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            watcher.Changed += (s, e) =>
            {
                Task.Run(async () =>
                {
                    await ProcessJournalAsync();
                    DataUpdated?.Invoke();
                });
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
                    await Task.Delay(5000);
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
        public TimeSpan? JumpCountdown => FleetCarrierJumpTime.HasValue ? FleetCarrierJumpTime.Value - DateTime.UtcNow : null;
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

        public void RaiseDataUpdated()
        {
            DataUpdated?.Invoke();
        }
        public void ResetFleetCarrierJumpFlag()
        {
            FleetCarrierJumpArrived = false;
        }
        public void SetDockingStatus()
        {
            Log.Debug("GameStateService: SetDockingStatus triggered");
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
                Log.Debug("Pruning route up to and including current system: {System}", CurrentSystem);
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

                        case "Loadout":
                            var loadout = JsonSerializer.Deserialize<LoadoutJson>(line);
                            if (loadout != null)
                            {
                                CurrentLoadout = loadout;
                                UserShipName = loadout.ShipName;
                                UserShipId = loadout.ShipIdent;
                                Log.Debug("Assigned Loadout from journal: {Ship} with {Modules} modules", loadout.Ship, loadout.Modules?.Count ?? 0);
                            }
                            break;

                        case "CarrierLocation":
                            if (FleetCarrierJumpTime == null || FleetCarrierJumpTime <= DateTime.UtcNow)
                            {
                                Log.Debug("CarrierLocation received – jump has completed");
                                FleetCarrierJumpTime = null;
                                CarrierJumpDestinationSystem = null;
                                CarrierJumpDestinationBody = null;
                                FleetCarrierJumpArrived = true;
                            }
                            else
                            {
                                Log.Debug("CarrierLocation seen, but jump still pending (FleetCarrierJumpTime = {Time})", FleetCarrierJumpTime);
                            }
                            break;



                        case "CarrierJumpRequest":
                            if (root.TryGetProperty("DepartureTime", out var dtProp) &&
                                DateTime.TryParse(dtProp.GetString(), out var dt))
                                FleetCarrierJumpTime = dt;

                            if (root.TryGetProperty("SystemName", out var sysName))
                                CarrierJumpDestinationSystem = sysName.GetString();

                            if (root.TryGetProperty("Body", out var bodyName))
                                CarrierJumpDestinationBody = bodyName.GetString();
                            break;

                        case "CarrierJump":
                            // Determine if this is the arrival confirmation (it includes "Docked", "StationName", etc.)
                            if (root.TryGetProperty("Docked", out var dockedProp) &&
                                dockedProp.GetBoolean() == true &&
                                root.TryGetProperty("StationName", out var stationProp) &&
                                stationProp.GetString()?.Length > 0)
                            {
                                Log.Debug("CarrierJump completion confirmed via station docking.");
                                FleetCarrierJumpTime = null;
                                CarrierJumpDestinationSystem = null;
                                CarrierJumpDestinationBody = null;
                                FleetCarrierJumpArrived = true;
                            }
                            else
                            {
                                // This is the jump initiation (could be older format or mid-jump)
                                Log.Debug("CarrierJump event seen without docking info — jump may still be in progress.");
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

                                    // Get the destination system from the StartJump event
                                    if (root.TryGetProperty("StarSystem", out var starSystem))
                                        HyperspaceDestination = starSystem.GetString();

                                    if (root.TryGetProperty("StarClass", out var starClass))
                                        HyperspaceStarClass = starClass.GetString();
                                }
                                else if (jumpTypeString == "Supercruise")
                                {
                                    Log.Information("Supercruise initiated");
                                    // Do not set IsHyperspaceJumping for supercruise
                                }
                            }
                            break;

                        case "FSDJump":
                            // Reset hyperspace jumping flag when the jump is complete
                            Log.Information("Hyperspace jump completed");
                            IsHyperspaceJumping = false;
                            HyperspaceDestination = null;
                            HyperspaceStarClass = null;
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
                                    Log.Debug("JournalWatcher: 'Docking request granted' detected.");
                                    SetDockingStatus();
                                }
                            }
                            break;



                    }
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
