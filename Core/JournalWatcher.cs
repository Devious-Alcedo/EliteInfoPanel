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
                    try
                    {
                        using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        stream.Seek(lastPosition, SeekOrigin.Begin);
                        using var reader = new StreamReader(stream);

                        while (!reader.EndOfStream)
                        {
                            try
                            {
                                string line = await reader.ReadLineAsync();
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    using JsonDocument doc = JsonDocument.Parse(line);
                                    string eventType = doc.RootElement.GetProperty("event").GetString();

                                    // your event handling here...
                                }
                            }
                            catch (IOException ioEx)
                            {
                                Console.WriteLine($"IO error while reading journal line: {ioEx.Message}");
                                break; // abort this read loop, allow outer loop to retry
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing journal line: {ex.Message}");
                            }
                        }

                        lastPosition = stream.Position;
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"IOException in JournalWatcher stream: {ex.Message}");
                        await Task.Delay(500); // back off a little longer if file is locked
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected error in JournalWatcher: {ex.Message}");
                    }

                    await Task.Delay(1000);
                }
            });
        }



    }
}
