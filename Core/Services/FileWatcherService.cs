using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class FileWatcherService : IFileWatcherService, IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly Dictionary<string, System.Timers.Timer> _debounceTimers = new();
        private readonly string _directory;

        public FileWatcherService(string directory)
        {
            _directory = directory;
        }

        public void Watch(string filter, Action onChanged, int debounceMs = 100, NotifyFilters notify = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime)
        {
            var watcher = new FileSystemWatcher(_directory)
            {
                Filter = filter,
                NotifyFilter = notify,
                EnableRaisingEvents = true
            };

            void schedule()
            {
                if (!_debounceTimers.TryGetValue(filter, out var timer))
                {
                    timer = new System.Timers.Timer(debounceMs) { AutoReset = false };
                    timer.Elapsed += (_, __) => onChanged();
                    _debounceTimers[filter] = timer;
                }
                timer.Stop();
                timer.Start();
            }

            watcher.Changed += (_, __) => schedule();
            watcher.Created += (_, __) => schedule();

            _watchers.Add(watcher);
        }

        public void Dispose()
        {
            foreach (var w in _watchers)
            {
                try { w.Dispose(); } catch { }
            }
            foreach (var t in _debounceTimers.Values)
            {
                try { t.Dispose(); } catch { }
            }
            _watchers.Clear();
            _debounceTimers.Clear();
        }
    }
}
