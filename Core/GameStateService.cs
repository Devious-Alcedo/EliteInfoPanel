using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
        public string UserShipName { get; private set; }
        public string UserShipId { get; private set; }

        public event Action DataUpdated;

        private string gamePath;
        private FileSystemWatcher watcher;

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

        private void LoadData()
        {
            try
            {
                var statusPath = Path.Combine(gamePath, "Status.json");
                var cargoPath = Path.Combine(gamePath, "Cargo.json");
                var backpackPath = Path.Combine(gamePath, "Backpack.json");
                var materialsPath = Path.Combine(gamePath, "FCMaterials.json");
                var routePath = Path.Combine(gamePath, "NavRoute.json");

                if (File.Exists(statusPath))
                    CurrentStatus = JsonSerializer.Deserialize<StatusJson>(File.ReadAllText(statusPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(cargoPath))
                    CurrentCargo = JsonSerializer.Deserialize<CargoJson>(File.ReadAllText(cargoPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(backpackPath))
                    CurrentBackpack = JsonSerializer.Deserialize<BackpackJson>(File.ReadAllText(backpackPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(materialsPath))
                    CurrentMaterials = JsonSerializer.Deserialize<FCMaterialsJson>(File.ReadAllText(materialsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(routePath))
                    CurrentRoute = JsonSerializer.Deserialize<NavRouteJson>(File.ReadAllText(routePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var latestJournal = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(latestJournal))
                {
                    using var fs = new FileStream(latestJournal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    var lines = new List<string>();

                    while (!sr.EndOfStream)
                    {
                        lines.Add(sr.ReadLine());
                    }

                    foreach (var line in lines.AsEnumerable().Reverse())
                    {
                        if (line.Contains("\"event\":\"Commander\""))
                        {
                            var entry = JsonSerializer.Deserialize<JournalEntry>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (entry?.@event == "Commander")
                            {
                                CommanderName = entry.Name;
                            }
                        }
                        else if (line.Contains("\"event\":\"LoadGame\""))
                        {
                            var entry = JsonSerializer.Deserialize<JournalEntry>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (entry?.@event == "LoadGame")
                            {
                                if (!string.IsNullOrWhiteSpace(entry.Ship_Localised))
                                {
                                    ShipLocalised = entry.Ship_Localised;
                                }
                                ShipName = entry.Ship;
                            }
                        }
                        else if (line.Contains("\"event\":\"CarrierJumpRequest\""))
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("DepartureTime", out var departureTime))
                            {
                                if (DateTime.TryParse(departureTime.GetString(), out var dt))
                                {
                                    FleetCarrierJumpTime = dt;
                                }
                            }
                        }
                        else if (line.Contains("\"event\":\"Location\""))
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("StarSystem", out var sys))
                            {
                                CurrentSystem = sys.GetString();
                            }
                        }

                        else if (line.Contains("\"event\":\"SquadronStartup\""))
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("SquadronName", out var squad))
                            {
                                SquadronName = squad.GetString();
                            }
                        }

                        else if (line.Contains("\"event\":\"Loadout\""))
                        {
                            var json = JsonDocument.Parse(line);
                            var root = json.RootElement;
                            UserShipName = root.GetProperty("ShipName").GetString();
                            UserShipId = root.GetProperty("ShipIdent").GetString();
                        }
                        



                        if (CommanderName != null && ShipLocalised != null && FleetCarrierJumpTime.HasValue)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in LoadData: " + ex);
            }
        }
    }
}



