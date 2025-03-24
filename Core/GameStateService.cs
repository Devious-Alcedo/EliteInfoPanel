using System;
using System.Collections.Generic;
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

        private string gamePath;
        private FileSystemWatcher watcher;

        public event Action DataUpdated;

        public GameStateService(string path)
        {
            gamePath = path;
            watcher = new FileSystemWatcher(gamePath, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += (s, e) =>
            {
                LoadData();
                DataUpdated?.Invoke();
            };

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var statusPath = Path.Combine(gamePath, "Status.json");
                var cargoPath = Path.Combine(gamePath, "Cargo.json");

                if (File.Exists(statusPath))
                    CurrentStatus = JsonSerializer.Deserialize<StatusJson>(File.ReadAllText(statusPath));

                if (File.Exists(cargoPath))
                    CurrentCargo = JsonSerializer.Deserialize<CargoJson>(File.ReadAllText(cargoPath));
            }
            catch { /* Ignore parse errors for now */ }
        }
    }
}
