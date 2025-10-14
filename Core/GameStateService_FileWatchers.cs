using System;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Threading;
using Serilog;
using EliteInfoPanel.Util;
using EliteInfoPanel.Core.Services;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        private void SetupFileWatcher(string fileName, Func<bool> loadMethod)
        {
            try
            {
                if (SettingsManager.Load().DevelopmentMode)
                {
                    string filePath = Path.Combine(gamePath, fileName);
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllText(filePath, "{}");
                        Log.Debug("Created empty development file: {File}", filePath);
                    }
                }
                // Use shared FileWatcherService with built-in debounce (injected)
                _fileWatcherService.Watch(fileName, () =>
                {
                    try
                    {
                        loadMethod();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error loading {File}", fileName);
                    }
                }, debounceMs: 100);
                Log.Debug("Set up file system watcher for {File}", fileName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error setting up watcher for {fileName}");
            }
        }

        private void SetupJournalWatcher()
        {
            try
            {
                latestJournalPath = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(latestJournalPath))
                {
                    Log.Warning("?? No journal files found in {Path}", gamePath);
                    return;
                }

                var fileInfo = new FileInfo(latestJournalPath);
                Log.Information("?? Monitoring journal: {Journal} (will scan for initialization, then monitor from end)",
                    Path.GetFileName(latestJournalPath));

                lastJournalPosition = 0;

                // Push model is active; no polling timer needed

                // Start push-based journal tail
                _journalReader.JournalEventReceived += (eventType, data) =>
                {
                    try
                    {
                        // Marshal to UI thread and process
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await ProcessJournalEventAsync(eventType, data, initialScan: !_firstLoadCompleted);
                        }, DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error handling journal event {EventType}", eventType);
                    }
                };
                _ = _journalReader.StartAsync(latestJournalPath, lastJournalPosition, _appStartTimeUtc, !_firstLoadCompleted);

                // Use injected watcher to monitor journal files
                _fileWatcherService.Watch("Journal.*.log", () =>
                {
                    try
                    {
                        var newest = Directory.GetFiles(gamePath, "Journal.*.log")
                            .OrderByDescending(File.GetLastWriteTime)
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(newest) &&
                            !string.Equals(newest, latestJournalPath, StringComparison.OrdinalIgnoreCase))
                        {
                            latestJournalPath = newest;
                            var newFileInfo = new FileInfo(latestJournalPath);
                            lastJournalPosition = newFileInfo.Length; // start at end of new file
                            _journalReader.SwitchTo(latestJournalPath, startAtEnd: true);
                            Log.Information("?? Switched to new journal: {Journal} (starting from end at position {Position})",
                                Path.GetFileName(latestJournalPath), lastJournalPosition);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error handling journal watcher callback");
                    }
                }, debounceMs: 150, notify: NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime);

                Log.Information("?? Journal monitoring active");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "?? Failed to setup journal watcher");
            }
        }
    }
}
