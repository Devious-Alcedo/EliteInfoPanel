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
                        }
                    }

                    lastPosition = stream.Position;
                    await Task.Delay(1000);
                }
            });
        }
    }
}
