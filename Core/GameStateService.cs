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
        public string CommanderName { get; private set; }
        public string ShipLocalised { get; private set; }

        private string gamePath;
        private FileSystemWatcher watcher;

        public event Action DataUpdated;

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

            // Add periodic polling fallback to ensure updates
            Task.Run(async () =>
            {
                while (true)
                {
                    LoadData();
                    DataUpdated?.Invoke();
                    await Task.Delay(2000); // Refresh every 2 seconds
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

                if (File.Exists(statusPath))
                    CurrentStatus = JsonSerializer.Deserialize<StatusJson>(File.ReadAllText(statusPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(cargoPath))
                    CurrentCargo = JsonSerializer.Deserialize<CargoJson>(File.ReadAllText(cargoPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(backpackPath))
                    CurrentBackpack = JsonSerializer.Deserialize<BackpackJson>(File.ReadAllText(backpackPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (File.Exists(materialsPath))
                    CurrentMaterials = JsonSerializer.Deserialize<FCMaterialsJson>(File.ReadAllText(materialsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var latestJournal = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(latestJournal))
                {
                    var lines = File.ReadAllLines(latestJournal).Reverse();
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"event\":\"Commander\""))
                        {
                            var entry = JsonSerializer.Deserialize<JournalEntry>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (!string.IsNullOrWhiteSpace(entry?.Commander))
                            {
                                CommanderName = entry.Commander;
                            }
                        }
                        else if (line.Contains("\"event\":\"LoadGame\""))
                        {
                            var entry = JsonSerializer.Deserialize<JournalEntry>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (!string.IsNullOrWhiteSpace(entry?.Ship_Localised))
                            {
                                ShipLocalised = entry.Ship_Localised;
                            }
                        }

                        if (CommanderName != null && ShipLocalised != null)
                            break;
                    }
                }
            }
            catch
            {
                // Handle or log parse errors if needed
            }
        }
    }

}
