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
        private FileSystemWatcher watcher;
        private long lastJournalPosition = 0;
        private string latestJournalPath;

        #endregion

        #region Public Constructors

        public GameStateService(string path)
        {
            gamePath = path;

            watcher = new FileSystemWatcher(gamePath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            watcher.Changed += (s, e) =>
            {
                LoadData();
                if (CurrentStatus != null)
                    DataUpdated?.Invoke();
            };

            LoadData();

            Task.Run(async () =>
            {
                while (true)
                {
                    LoadData();
                    await ProcessJournalAsync();

                    if (CurrentStatus != null)
                        DataUpdated?.Invoke();

                    await Task.Delay(2000);
                }
            });
        }

        #endregion

        #region Public Events

        public event Action DataUpdated;

        #endregion

        #region Public Properties

        public long? Balance => CurrentStatus?.Balance;
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

        #endregion

        #region Public Methods

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

        public void RaiseDataUpdated()
        {
            DataUpdated?.Invoke();
        }

        public void UpdateLoadout(LoadoutJson loadout)
        {
            CurrentLoadout = loadout;
        }

        #endregion

        #region Private Methods

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

        private void LoadData()
        {
            try
            {
                var statusPath = Path.Combine(gamePath, "Status.json");
                var cargoPath = Path.Combine(gamePath, "Cargo.json");
                var backpackPath = Path.Combine(gamePath, "Backpack.json");
                var materialsPath = Path.Combine(gamePath, "FCMaterials.json");
                var routePath = Path.Combine(gamePath, "NavRoute.json");

                CurrentStatus = DeserializeJsonFile<StatusJson>(statusPath);
                CurrentCargo = DeserializeJsonFile<CargoJson>(cargoPath);
                CurrentBackpack = DeserializeJsonFile<BackpackJson>(backpackPath);
                // CurrentMaterials = DeserializeJsonFile<FCMaterialsJson>(materialsPath);
                CurrentRoute = DeserializeJsonFile<NavRouteJson>(routePath);

                if (CurrentStatus != null)
                {
                    Log.Debug("Raw Status.json: {RawStatus}", File.ReadAllText(statusPath));
                    Log.Debug("Parsed CurrentStatus.Flags: {Flags}", CurrentStatus?.Flags);
                }

                latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in LoadData");
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

                        case "CarrierJumpRequest":
                            if (root.TryGetProperty("DepartureTime", out var dtProp) &&
                                DateTime.TryParse(dtProp.GetString(), out var dt))
                                FleetCarrierJumpTime = dt;
                            break;

                        case "Location":
                            if (root.TryGetProperty("StarSystem", out var system))
                                CurrentSystem = system.GetString();
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

        #endregion
    }
}
