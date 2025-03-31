using EliteInfoPanel.Core.EliteInfoPanel.Core;
using Serilog;
using System.Diagnostics;
using System.IO;

using System.Text.Json;


namespace EliteInfoPanel.Core
{
    public class GameStateService
    {
        public StatusJson CurrentStatus { get; private set; }
        public CargoJson CurrentCargo { get; private set; }
        public BackpackJson CurrentBackpack { get; private set; }
        public FCMaterialsJson CurrentMaterials { get; private set; }
        public NavRouteJson CurrentRoute { get; private set; }
        public string CommanderName { get; private set; }
        public string ShipLocalised { get; private set; }
        public string ShipName { get; private set; }
        public string CurrentSystem { get; private set; }
        public long? Balance => CurrentStatus?.Balance;
        public string SquadronName { get; private set; }
        public DateTime? FleetCarrierJumpTime { get; private set; }
        public TimeSpan? JumpCountdown => FleetCarrierJumpTime.HasValue ? FleetCarrierJumpTime.Value - DateTime.UtcNow : null;
        public string UserShipName { get;  set; }
        public string UserShipId { get;  set; }


        // Add this property
        public LoadoutJson CurrentLoadout { get; internal set; }



        public event Action DataUpdated;
       

        private string gamePath;
        private FileSystemWatcher watcher;
        public void RaiseDataUpdated()
        {
            DataUpdated?.Invoke();
        }

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
                DataUpdated?.Invoke();
            };

            LoadData();

            Task.Run(async () =>
            {
                while (true)
                {
                    LoadData();
                    DataUpdated?.Invoke();
                    await Task.Delay(2000);
                }
            });
        }
        public void UpdateLoadout(LoadoutJson loadout)
        {
            CurrentLoadout = loadout;
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
                CurrentMaterials = DeserializeJsonFile<FCMaterialsJson>(materialsPath);
                CurrentRoute = DeserializeJsonFile<NavRouteJson>(routePath);

                Log.Debug("Raw Status.json: {RawStatus}", File.ReadAllText(statusPath));
                Log.Debug("Parsed CurrentStatus.Flags: {Flags}", CurrentStatus?.Flags);

                var latestJournal = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(latestJournal))
                {
                    try
                    {
                        using var fs = new FileStream(latestJournal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs);

                        var lines = new List<string>();
                        while (!sr.EndOfStream)
                            lines.Add(sr.ReadLine());

                        foreach (var line in lines.AsEnumerable().Reverse())
                        {
                            if (line.Contains("\"event\":\"Commander\""))
                            {
                                var entry = JsonSerializer.Deserialize<JournalEntry>(line);
                                CommanderName = entry?.Name ?? CommanderName;
                            }
                            else if (line.Contains("\"event\":\"LoadGame\""))
                            {
                                var entry = JsonSerializer.Deserialize<JournalEntry>(line);
                                ShipLocalised = entry?.Ship_Localised ?? ShipLocalised;
                                ShipName = entry?.Ship ?? ShipName;
                            }
                            else if (line.Contains("\"event\":\"CarrierJumpRequest\""))
                            {
                                using var doc = JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("DepartureTime", out var departureTime))
                                {
                                    if (DateTime.TryParse(departureTime.GetString(), out var dt))
                                        FleetCarrierJumpTime = dt;
                                }
                            }
                            else if (line.Contains("\"event\":\"Location\""))
                            {
                                using var doc = JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("StarSystem", out var sys))
                                    CurrentSystem = sys.GetString();
                            }
                            else if (line.Contains("\"event\":\"SquadronStartup\""))
                            {
                                using var doc = JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("SquadronName", out var squad))
                                    SquadronName = squad.GetString();
                            }
                            //else if (line.Contains("\"event\":\"Loadout\""))
                            //{
                            //    var loadout = JsonSerializer.Deserialize<LoadoutJson>(line);
                            //    CurrentLoadout = loadout;
                            //    UserShipName = loadout?.ShipName;
                            //    UserShipId = loadout?.ShipIdent;
                            //}
                            //Log.Debug("LoadData assigned ship from journal: {Ship}", loadout?.Ship);

                            if (CommanderName != null && ShipLocalised != null && FleetCarrierJumpTime.HasValue)
                                break;
                        }
                    }
                    catch (IOException ex)
                    {
                        Log.Warning("Journal file temporarily locked or unavailable: {Message}", ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error parsing journal file.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in LoadData");
            }
        }


        private T DeserializeJsonFile<T>(string filePath) where T : class
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to deserialize {filePath}: {ex}");
                return null;
            }
        }

       

    }
}



