using EliteInfoPanel.Core.EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class JournalWatcher
    {
        private readonly string logFilePath;
        private long lastPosition = 0;
        public string CommanderName { get; private set; }
        public string ShipLocalised { get; private set; }
        public event Action<JournalEntry> NewEntry;
        public event Action<LoadoutJson> LoadoutReceived;
        public event Action<bool> FleetCarrierScreenChanged;

        public JournalWatcher(string filePath)
        {
            logFilePath = filePath;
        }

        public void StartWatching()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stream.Seek(lastPosition, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream);

                    while (!reader.EndOfStream)
                    {
                        string line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            try
                            {
                                using JsonDocument doc = JsonDocument.Parse(line);
                                string eventType = doc.RootElement.GetProperty("event").GetString();

                                if (eventType == "LoadGame")
                                {
                                    var entry = JsonSerializer.Deserialize<JournalEntry>(line);
                                    CommanderName = entry?.Name;
                                    ShipLocalised = entry?.Ship_Localised;
                                    NewEntry?.Invoke(entry);
                                }
                                else if (eventType == "Loadout")
                                {
                                    var loadout = JsonSerializer.Deserialize<LoadoutJson>(line);
                                    if (loadout != null)
                                    {
                                        LoadoutReceived?.Invoke(loadout); // <-- this line is correct
                                                                          // Also optionally update internal fields, e.g.:
                                        var shipName = ShipNameHelper.GetLocalisedName(loadout.Ship);
                                        ShipLocalised = shipName;
                                    }
                                }
                                else
                                {
                                    var entry = JsonSerializer.Deserialize<JournalEntry>(line);
                                    if (entry != null)
                                        NewEntry?.Invoke(entry);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Optional: log error parsing journal line
                                Console.WriteLine($"Error parsing journal line: {ex.Message}");
                            }
                        }
                    }

                    lastPosition = stream.Position;
                    await Task.Delay(1000);
                }
            });
        }
    }
}
