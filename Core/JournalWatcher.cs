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
                            var entry = JsonSerializer.Deserialize<JournalEntry>(line, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            NewEntry?.Invoke(entry);
                            if (entry.@event == "LoadGame" && !string.IsNullOrEmpty(entry.Name))
                            {
                                CommanderName = entry.Name;
                                ShipLocalised = entry.Ship_Localised;
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
