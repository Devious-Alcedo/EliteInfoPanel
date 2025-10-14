using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class JournalReader : IJournalReader
    {
        private CancellationTokenSource? _cts;
        private Task? _tailTask;
        private string? _currentFile;
        private long _position;
        private DateTime _appStartTimeUtc;
        private bool _isInitialScan;

        public event Action<string, JsonElement>? JournalEventReceived;

        private static readonly HashSet<string> HeavyEvents = new(StringComparer.OrdinalIgnoreCase)
        {
            "CargoTransfer", "CargoDepot", "CarrierTradeOrder", "MarketBuy", "MarketSell"
        };

        private static readonly HashSet<string> InitSkipCarrierEvents = new(StringComparer.OrdinalIgnoreCase)
        {
            "CarrierJumpRequest", "CarrierJump", "CarrierJumpCancelled"
        };

        // Pull-based API kept for tests/backfill
        public async IAsyncEnumerable<(string Line, JsonElement Root, long Position)> ReadEventsAsync(
            string filePath,
            long startPosition,
            DateTime appStartTimeUtc,
            bool isInitialScan,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) yield break;

            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(startPosition, SeekOrigin.Begin);

            using var sr = new StreamReader(fs);

            while (!sr.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string line = await sr.ReadLineAsync().ConfigureAwait(false);
                long pos = fs.Position;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                JsonElement cloned;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("event", out var eventProp))
                        continue;

                    string eventType = eventProp.GetString();

                    // Skip heavy historical events to reduce startup work
                    if (root.TryGetProperty("timestamp", out var tsProp) &&
                        DateTime.TryParse(tsProp.GetString(), out var ts) &&
                        ts < appStartTimeUtc && HeavyEvents.Contains(eventType))
                    {
                        continue;
                    }

                    // During initial scan, skip carrier jump state transitions; recovery happens separately
                    if (isInitialScan && InitSkipCarrierEvents.Contains(eventType))
                    {
                        continue;
                    }

                    cloned = root.Clone();
                }
                catch
                {
                    // Ignore malformed lines
                    continue;
                }

                yield return (line, cloned, pos);
            }
        }

        public Task StartAsync(
            string filePath,
            long startPosition,
            DateTime appStartTimeUtc,
            bool isInitialScan,
            CancellationToken cancellationToken = default)
        {
            Stop();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _currentFile = filePath;
            _position = startPosition;
            _appStartTimeUtc = appStartTimeUtc;
            _isInitialScan = isInitialScan;
            _tailTask = Task.Run(() => TailLoopAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public void SwitchTo(string newFilePath, bool startAtEnd = true)
        {
            _currentFile = newFilePath;
            _position = startAtEnd && File.Exists(newFilePath) ? new FileInfo(newFilePath).Length : 0;
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }
        }

        private async Task TailLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (string.IsNullOrEmpty(_currentFile) || !File.Exists(_currentFile))
                    {
                        await Task.Delay(250, token);
                        continue;
                    }

                    using var fs = new FileStream(_currentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (_position < 0) _position = 0;
                    if (_position > fs.Length) _position = fs.Length;
                    fs.Seek(_position, SeekOrigin.Begin);

                    using var sr = new StreamReader(fs);
                    while (!sr.EndOfStream && !token.IsCancellationRequested)
                    {
                        string? line = await sr.ReadLineAsync().ConfigureAwait(false);
                        _position = fs.Position;
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            if (!root.TryGetProperty("event", out var eventProp))
                                continue;
                            var eventType = eventProp.GetString();

                            if (root.TryGetProperty("timestamp", out var tsProp) &&
                                DateTime.TryParse(tsProp.GetString(), out var ts) &&
                                ts < _appStartTimeUtc && HeavyEvents.Contains(eventType))
                            {
                                continue;
                            }
                            if (_isInitialScan && InitSkipCarrierEvents.Contains(eventType))
                            {
                                continue;
                            }

                            JournalEventReceived?.Invoke(eventType, root.Clone());
                        }
                        catch
                        {
                            // ignore bad lines
                        }
                    }

                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500, token);
                }
            }
        }
    }
}
