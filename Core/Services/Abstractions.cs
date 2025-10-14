namespace EliteInfoPanel.Core.Services
{
    internal interface IGameFilesService
    {
        string PathFor(string fileName);
        T? ReadJson<T>(string fileName) where T : class;
        void WriteJson<T>(string fileName, T value);
        string? LatestJournal();
    }

    internal interface IJournalReader
    {
        // Pull-based API (kept for tests/backfill)
        System.Collections.Generic.IAsyncEnumerable<(string Line, System.Text.Json.JsonElement Root, long Position)> ReadEventsAsync(
            string filePath, long startPosition, System.DateTime appStartTimeUtc, bool isInitialScan,
            System.Threading.CancellationToken cancellationToken = default);

        // Push-based API
        event System.Action<string, System.Text.Json.JsonElement>? JournalEventReceived;

        System.Threading.Tasks.Task StartAsync(
            string filePath,
            long startPosition,
            System.DateTime appStartTimeUtc,
            bool isInitialScan,
            System.Threading.CancellationToken cancellationToken = default);

        void SwitchTo(string newFilePath, bool startAtEnd = true);
        void Stop();
    }

    internal interface IFileWatcherService
    {
        void Watch(string filter, System.Action onChanged, int debounceMs = 100, System.IO.NotifyFilters notify = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size | System.IO.NotifyFilters.CreationTime);
    }

    internal interface ICarrierCargoService
    {
        event System.EventHandler<CargoUpdatedEventArgs>? CargoUpdated;
        System.Collections.Generic.Dictionary<string, int> Load();
        void Save(System.Collections.Generic.Dictionary<string, int> cargo);
        void Initialize(System.Collections.Generic.Dictionary<string, int> cargo);
        void Normalize();
        System.Collections.Generic.Dictionary<string, int> GetState();
        void ApplyEvent(System.Text.Json.JsonElement root);
    }

    internal interface IColonizationService
    {
        event System.EventHandler<ColonizationUpdatedEventArgs>? ColonizationUpdated;
        System.Collections.Generic.Dictionary<long, Core.Models.ColonizationData> LoadActive(string filePath);
        void SaveAllActive(string filePath, System.Collections.Generic.IEnumerable<Core.Models.ColonizationData> activeDepots);
        void SaveSingle(string filePath, Core.Models.ColonizationData data);
    }

    internal interface IRouteProgressService
    {
        event System.EventHandler<RouteUpdatedEventArgs>? RouteUpdated;
        Core.Models.RouteProgressState Load();
        void Save(Core.Models.RouteProgressState state);
    }

    internal interface IStatusService
    {
        global::EliteInfoPanel.Core.StatusJson? LoadStatus(IGameFilesService files);
        LegalStateResult MapLegalState(System.Text.Json.JsonElement root, string eventType);
    }

    internal interface ICarrierJumpManager
    {
        event System.EventHandler<CarrierJumpScheduledEventArgs>? CarrierJumpScheduled;
        event System.EventHandler? CarrierJumpCompleted;
        bool ShouldShowOverlay { get; }
        string DestinationSystem { get; }
        string DestinationBody { get; }
        int CountdownSeconds { get; }
        bool IsJumpScheduled { get; }
        System.DateTime? ScheduledJumpTime { get; }
        bool JumpCompleted { get; }

        void ScheduleJump(System.DateTime departureTimeUtc, string systemName, string bodyName);
        void CancelJump();
        void CompleteJump();
        void ActivateOverlay();
        void Reset();
    }
}
