using EliteInfoPanel.Core.EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Interop;

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
        public event Action DockingGranted;

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

                        while (true)
                        {
                            string? line = await reader.ReadLineAsync();

                            if (line == null)
                            {
                                await Task.Delay(500); // Wait for new data
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                try
                                {
                                    using JsonDocument doc = JsonDocument.Parse(line);
                                    string eventType = doc.RootElement.GetProperty("event").GetString();

                                    if (eventType == "ReceiveText")
                                    {
                                        if (doc.RootElement.TryGetProperty("Message_Localised", out var msgProp))
                                        {
                                            string msg = msgProp.GetString();
                                            Log.Debug("JournalWatcher: ReceiveText message: {Message}", msg);

                                            if (msg?.Contains("Docking request granted", StringComparison.OrdinalIgnoreCase) == true)
                                            {
                                                Log.Debug("JournalWatcher: 'Docking request granted' detected.");
                                                DockingGranted?.Invoke();
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "Failed to parse journal line");
                                }
                            }
                        }


                        lastPosition = stream.Position;
                    }
                    catch (IOException ex)
                    {
                        Log.Warning("Journal file temporarily locked or unavailable: {Message}", ex.Message);
                        Console.WriteLine($"IOException in JournalWatcher stream: {ex.Message}");
                        await Task.Delay(500); // back off a little longer if file is locked
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unexpected error in JournalWatcher");
                        Console.WriteLine($"Unexpected error in JournalWatcher: {ex.Message}");
                    }

                    await Task.Delay(1000);
                }
            });
        }



    }
}
