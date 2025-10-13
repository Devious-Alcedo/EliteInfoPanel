using System;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Threading;
using Serilog;
using EliteInfoPanel.Util;

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
                var watcher = new FileSystemWatcher(gamePath)
                {
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                var debounceTimer = new System.Timers.Timer(100) { AutoReset = false };
                bool pendingUpdate = false;

                debounceTimer.Elapsed += (s, e) =>
                {
                    if (pendingUpdate)
                    {
                        pendingUpdate = false;
                        try
                        {
                            loadMethod();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Error loading {fileName}");
                        }
                    }
                };

                watcher.Changed += (s, e) =>
                {
                    pendingUpdate = true;
                    debounceTimer.Stop();
                    debounceTimer.Start();
                };

                watcher.Created += (s, e) =>
                {
                    pendingUpdate = true;
                    debounceTimer.Stop();
                    debounceTimer.Start();
                };

                _watchers.Add(watcher);
                Log.Debug($"Set up file system watcher for {fileName}");
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

                var dirWatcher = new FileSystemWatcher(gamePath)
                {
                    Filter = "Journal.*.log",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                var journalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };

                bool pendingUpdate = false;
                DateTime lastUpdate = DateTime.MinValue;

                journalTimer.Tick += async (s, e) =>
                {
                    if (pendingUpdate && DateTime.UtcNow - lastUpdate > TimeSpan.FromMilliseconds(100))
                    {
                        pendingUpdate = false;
                        lastUpdate = DateTime.UtcNow;

                        try
                        {
                            await ProcessJournalAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "?? Error in journal timer");
                        }
                    }
                };

                journalTimer.Start();

                dirWatcher.Changed += (s, e) =>
                {
                    if (Path.GetFileName(e.FullPath) == Path.GetFileName(latestJournalPath))
                    {
                        pendingUpdate = true;
                    }
                };

                dirWatcher.Created += (s, e) =>
                {
                    if (Path.GetFileName(e.FullPath).StartsWith("Journal.") &&
                        File.GetLastWriteTime(e.FullPath) > File.GetLastWriteTime(latestJournalPath))
                    {
                        latestJournalPath = e.FullPath;
                        var newFileInfo = new FileInfo(latestJournalPath);
                        lastJournalPosition = newFileInfo.Length;
                        pendingUpdate = true;
                        Log.Information("?? Switched to new journal: {Journal} (starting from end at position {Position})",
                            Path.GetFileName(latestJournalPath), lastJournalPosition);
                    }
                };

                _watchers.Add(dirWatcher);
                Log.Information("?? Journal monitoring active");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "?? Failed to setup journal watcher");
            }
        }
    }
}
