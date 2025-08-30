using System.IO;
using System.Text.Json;
using Serilog;

namespace EliteInfoPanel.Services;

/// <summary>
/// Robust journal file monitoring service that prevents missing events
/// </summary>
public class JournalService : IDisposable
{
    private readonly string _gameDirectory;
    private readonly Dictionary<string, long> _journalPositions = new();
    private FileSystemWatcher? _journalWatcher;
    private readonly Timer _scanTimer;
    private readonly object _lockObject = new();
    private bool _disposed = false;

    public event Action<string, JsonElement>? JournalEventReceived;

    public JournalService(string gameDirectory)
    {
        _gameDirectory = gameDirectory ?? throw new ArgumentNullException(nameof(gameDirectory));
        
        // Create a timer that scans for new events every 100ms (faster than before)
        _scanTimer = new Timer(ScanJournalFiles, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        
        SetupJournalWatcher();
        InitializeJournalPositions();
        
        Log.Information("📖 JournalService initialized for directory: {Directory}", _gameDirectory);
    }

    private void SetupJournalWatcher()
    {
        try
        {
            _journalWatcher = new FileSystemWatcher(_gameDirectory)
            {
                Filter = "Journal.*.log",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _journalWatcher.Changed += OnJournalFileChanged;
            _journalWatcher.Created += OnJournalFileChanged;
            
            Log.Debug("📖 Journal file watcher enabled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "📖 Failed to setup journal file watcher");
        }
    }

    private void OnJournalFileChanged(object sender, FileSystemEventArgs e)
    {
        // Don't process immediately - let the timer handle it to avoid rapid-fire events
        Log.Debug("📖 Journal file change detected: {File}", Path.GetFileName(e.FullPath));
    }

    private void InitializeJournalPositions()
    {
        lock (_lockObject)
        {
            try
            {
                var journalFiles = Directory.GetFiles(_gameDirectory, "Journal.*.log")
                    .OrderBy(f => Path.GetFileName(f)); // Chronological order

                foreach (var journalFile in journalFiles)
                {
                    var fileName = Path.GetFileName(journalFile);
                    if (!_journalPositions.ContainsKey(fileName))
                    {
                        // For existing files, start from the end to only process new events
                        var fileInfo = new FileInfo(journalFile);
                        _journalPositions[fileName] = fileInfo.Length;
                        Log.Debug("📖 Initialized position for {File}: {Position} bytes", fileName, fileInfo.Length);
                    }
                }

                // For the most recent journal, scan the last 10KB for any critical events we might have missed
                var latestJournal = journalFiles.LastOrDefault();
                if (!string.IsNullOrEmpty(latestJournal))
                {
                    ScanRecentEventsInLatestJournal(latestJournal);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "📖 Error initializing journal positions");
            }
        }
    }

    private void ScanRecentEventsInLatestJournal(string journalPath)
    {
        try
        {
            var fileInfo = new FileInfo(journalPath);
            var fileName = Path.GetFileName(journalPath);
            
            using var fs = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            // Read last 10KB to catch any recent critical events
            long startPos = Math.Max(0, fileInfo.Length - 10240);
            fs.Seek(startPos, SeekOrigin.Begin);
            
            using var reader = new StreamReader(fs);
            var criticalEvents = new List<(string EventType, JsonElement Data, DateTime Timestamp)>();
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    
                    if (!root.TryGetProperty("event", out var eventProp)) continue;
                    var eventType = eventProp.GetString();
                    
                    // Look for critical events we might have missed
                    if (IsCriticalEvent(eventType))
                    {
                        if (root.TryGetProperty("timestamp", out var timestampProp) &&
                            DateTime.TryParse(timestampProp.GetString(), out var timestamp))
                        {
                            // Only process events from the last 5 minutes
                            if (DateTime.UtcNow - timestamp < TimeSpan.FromMinutes(5))
                            {
                                criticalEvents.Add((eventType, root.Clone(), timestamp));
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Log.Debug("📖 Failed to parse journal line: {Error}", ex.Message);
                }
            }

            // Process critical events in chronological order
            foreach (var (eventType, data, timestamp) in criticalEvents.OrderBy(e => e.Timestamp))
            {
                Log.Information("📖 🚨 Processing recent critical event: {Event} from {Timestamp}", eventType, timestamp);
                JournalEventReceived?.Invoke(eventType, data);
            }

            if (criticalEvents.Any())
            {
                Log.Information("📖 Processed {Count} recent critical events from latest journal", criticalEvents.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "📖 Error scanning recent events in latest journal");
        }
    }

    private bool IsCriticalEvent(string? eventType)
    {
        return eventType switch
        {
            "CarrierJumpRequest" => true,
            "CarrierJump" => true,
            "CarrierJumpCancelled" => true,
            "FSDJump" => true,
            "StartJump" => true,
            "SupercruiseEntry" => true,
            "SupercruiseExit" => true,
            "Docked" => true,
            "Undocked" => true,
            "Location" => true,
            _ => false
        };
    }

    private void ScanJournalFiles(object? state)
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            try
            {
                var journalFiles = Directory.GetFiles(_gameDirectory, "Journal.*.log")
                    .OrderBy(f => Path.GetFileName(f));

                foreach (var journalFile in journalFiles)
                {
                    ProcessJournalFile(journalFile);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "📖 Error during journal scan");
            }
        }
    }

    private void ProcessJournalFile(string journalPath)
    {
        try
        {
            var fileName = Path.GetFileName(journalPath);
            var fileInfo = new FileInfo(journalPath);
            
            if (!fileInfo.Exists) return;

            // Initialize position if we haven't seen this file before
            if (!_journalPositions.ContainsKey(fileName))
            {
                _journalPositions[fileName] = 0; // Start from beginning for new files
                Log.Information("📖 New journal file detected: {File}", fileName);
            }

            var lastPosition = _journalPositions[fileName];
            var currentSize = fileInfo.Length;

            // If file hasn't grown, nothing to do
            if (currentSize <= lastPosition) return;

            // If file has shrunk (shouldn't happen but just in case), reset position
            if (currentSize < lastPosition)
            {
                Log.Warning("📖 Journal file appears to have shrunk: {File} (was {OldSize}, now {NewSize})", 
                    fileName, lastPosition, currentSize);
                lastPosition = 0;
            }

            using var fs = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(lastPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(fs);
            var linesProcessed = 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("event", out var eventProp))
                    {
                        var eventType = eventProp.GetString();
                        if (!string.IsNullOrEmpty(eventType))
                        {
                            JournalEventReceived?.Invoke(eventType, root.Clone());
                            linesProcessed++;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Log.Debug("📖 Failed to parse journal line: {Error}", ex.Message);
                }
            }

            // Update position
            _journalPositions[fileName] = fs.Position;

            if (linesProcessed > 0)
            {
                Log.Debug("📖 Processed {Count} events from {File} (position: {Position})", 
                    linesProcessed, fileName, _journalPositions[fileName]);
            }
        }
        catch (IOException ex)
        {
            // File might be locked by Elite, this is normal
            Log.Debug("📖 IO error reading journal file (file locked): {Error}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "📖 Error processing journal file: {File}", journalPath);
        }
    }

    public void ForceRescan()
    {
        Log.Information("📖 Force rescanning all journal files...");
        
        lock (_lockObject)
        {
            // Reset all positions to rescan files
            var keys = _journalPositions.Keys.ToList();
            foreach (var key in keys)
            {
                _journalPositions[key] = 0;
            }
            
            ScanJournalFiles(null);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        _journalWatcher?.Dispose();
        _scanTimer?.Dispose();
        
        Log.Information("📖 JournalService disposed");
    }
}
