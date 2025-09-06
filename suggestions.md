# Code Review Suggestions

## General
- Consider adding more XML documentation comments for public methods and classes to improve maintainability.
- Review thread safety for all shared state, especially in GameStateService and MqttService. Some fields are protected by locks, but not all.
- Ensure all IDisposable resources (FileSystemWatcher, Timers, CancellationTokenSource, etc.) are properly disposed. GameStateService does not currently implement IDisposable.
- Consider using async/await consistently for file I/O and network operations to avoid blocking threads.

## GameStateService
- The use of List<FileSystemWatcher> may lead to resource leaks if watchers are not disposed. Consider tracking and disposing them on shutdown.
- The batch update system (_isUpdating, UpdateScope) is useful, but queued property notifications (_pendingNotifications) could grow unbounded if not flushed. Consider a maximum size or periodic flush.
- Some methods (e.g., LoadAllData) use Task.WaitAll, which can block the UI thread if called from the main thread. Prefer async/await for better responsiveness.
- Exception handling is present, but some catch blocks rethrow or swallow exceptions. Review for consistent error reporting and recovery.
- Manual cargo change expiry is hardcoded to 30 minutes. Consider making this configurable.
- Some file paths (ManualCarrierCargoFilePath, CarrierCargoFilePath) are hardcoded to AppData/Local or AppData/Roaming. Consider centralizing path management.
- The method UpdateCurrentCarrierCargoFromDictionary uses #if dev for logging, which may be missed in production builds. Consider a more consistent logging strategy.
- The method SetupFileWatcher creates a System.Timers.Timer for debouncing, but does not dispose it. This could lead to timer leaks.
- The method SetupJournalWatcher uses DispatcherTimer, which should be stopped and disposed on shutdown.
- The method BeginUpdate returns an IDisposable, but the UpdateScope class is private and not reusable elsewhere. Consider extracting to a utility class if needed in other services.

## CarrierCargoTracker
- The normalization of cargo keys relies on CommodityMapper.GetDisplayName, which may not be unique or stable. Consider validating display names for uniqueness.
- The FindExistingCargoKey method does a linear search. If cargo grows large, consider a more efficient lookup or normalization strategy.
- Logging is wrapped in #if dev, which may hide important diagnostics in production. Consider using log levels instead.
- The Process method only handles a fixed set of event types. If new event types are added in the game, this may need updating.

## MqttService
- The singleton pattern (Lazy<MqttService>) is thread-safe, but Dispose is not called automatically. Ensure Dispose is called on application exit.
- The reconnect logic uses a Timer and Task.Run, which may result in multiple concurrent reconnect attempts if the connection is unstable. Consider throttling or serializing reconnect attempts.
- The PublishAsync method does not retry on transient network errors. Consider adding retry logic for robustness.
- The _haConfigSent cache is cleared in some methods, but not always repopulated. Ensure Home Assistant configs are always resent when needed.
- The method PublishFlagStatesAsync uses rate limiting and change detection, but forcePublish overrides these. Review for possible message flooding if forcePublish is used frequently.
- The method PublishGameEventAsync serializes eventData as an object. If eventData is a complex type, ensure it serializes correctly for MQTT consumers.
- The method Dispose calls DisconnectAsync synchronously with .Wait(5000), which may block the thread. Prefer async disposal or fire-and-forget.

## Other
- Consider adding unit tests for critical logic (cargo tracking, MQTT publishing, file watching) to catch regressions.
- Review all usages of Task.Run for fire-and-forget async work. Ensure exceptions are logged and do not crash the process.
- Consider using a centralized error reporting/logging strategy for all background tasks.
- Review all file I/O for possible race conditions if files are updated by the game and the app simultaneously.

---

This file documents code review suggestions only. No changes have been made to the codebase.
