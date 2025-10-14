Goals
•	Keep GameStateService as the state store/facade (INPC + public properties).
•	Move IO, timers, and business logic into focused services.
•	Make services UI-agnostic; only GameStateService marshals to UI thread.
Phase 1: Stabilize with partials (low risk)
•	Expand partials to group responsibilities (no public API changes):
•	GameStateService_Journal.cs → journal reading/dispatch
•	GameStateService_FileWatchers.cs → watchers/timers bootstrap
•	GameStateService_Cargo.cs → carrier cargo logic and persistence
•	GameStateService_Colonization.cs → colonization parsing/persist
•	GameStateService_Status.cs → Status.json + legal state mapping
•	GameStateService_Route.cs → route progress/jump pruning
•	GameStateService_Mqtt.cs → MQTT publishing
•	GameStateService_CarrierJump.cs (already exists)
Phase 2: Extract internal helpers (still internal to Core, minimal churn)
•	Create internal services (classes) and inject into GameStateService via constructor (or create inside until DI is added):
•	JournalReader (reads journal, raises typed events)
•	GameFilesService (paths, read/write JSON, dev files)
•	FileWatcherService (encapsulates watchers + debounce)
•	CarrierJumpManager (uses CarrierJumpState, timers, overlay rules)
•	CarrierCargoService (wraps CarrierCargoTracker, normalization, save/load)
•	ColonizationService (state + save/load, filtering)
•	RouteProgressService (state + save/load)
•	GameStateService subscribes to these services’ events and updates its properties (and raises INPC).
Phase 3: Introduce interfaces and DI (clean MVVM seams)
•	Define interfaces for testability:
•	IJournalReader, IGameFilesService, IFileWatcherService
•	ICarrierJumpManager, ICarrierCargoService, IColonizationService, IRouteProgressService, IMqttService (already exists)
•	Register with Microsoft.Extensions.DependencyInjection in App.xaml.cs and inject into GameStateService.
•	Keep GameStateService lifetime as singleton; others as singletons too.
Phase 4: Event model and threading
•	Services publish lightweight domain events (C# events) with DTOs:
•	JournalEventReceived, CarrierJumpScheduled, CarrierJumpCompleted, CargoUpdated, StatusUpdated, RouteUpdated, ColonizationUpdated
•	GameStateService:
•	Subscribes to events, updates its state, and raises INPC on the Dispatcher.
•	Owns all public properties consumed by ViewModels (no change to VMs).
Phase 5: Persistence normalization
•	Move all JSON read/write and path decisions to GameFilesService.
•	ColonizationService and CarrierCargoService call IGameFilesService for persistence.
•	All app settings and persisted feature state now live under AppData/Roaming/EliteInfoPanel using IGameFilesService:
    - CarrierCargo.json (replaces legacy carrier_cargo_state.json)
    - ManualCarrierCargo.json (replaces legacy LocalAppData EliteCompanion path)
    - RouteProgress.json
    - ColonizationData.json
•	Migrations implemented:
    - On missing CarrierCargo.json, load legacy carrier_cargo_state.json (AppData/Roaming/EliteInfoPanel), save to new path, then delete legacy file.
    - On missing ManualCarrierCargo.json, load legacy LocalAppData/EliteCompanion/ManualCarrierCargo.json, save to new path, then delete legacy file.
•	Reads use FileShare.ReadWrite with empty/whitespace checks to avoid file contention with the game.
Phase 6: API boundaries per feature (mapping from current methods)
•	Journal
•	Move: ProcessJournalAsync, SetupJournalWatcher, scanning logic → JournalReader
•	Expose typed events per journal event type
•	Carrier Jump
•	Keep CarrierJumpState
•	Move: timers and show/hide logic → CarrierJumpManager
•	GameStateService only updates IsOnFleetCarrier and exposes overlay properties from manager
•	Cargo
•	Move: EnsureCarrierCargoTrackingInitialized, ProcessCarrierCargoGameUpdate, RefreshCargoStates, FixCarrierCargoItem, CleanupDuplicateCargoEntries, save/load → CarrierCargoService
•	Colonization
•	Move: processing of ColonisationConstructionDepot, depot dict + save/load/filter → ColonizationService
•	Status/Legal
•	Move: LoadStatusData, ProcessLegalStateEvent → StatusService (or keep in GameFilesService + mapper)
•	Route
•	Move: PruneCompletedRouteSystems, SaveRouteProgress, load → RouteProgressService
•	File watchers
•	Move: SetupFileWatcher and debouncing → FileWatcherService
•	MQTT
•	Keep MqttService as-is; ensure only one publisher (from GameStateService or a thin MqttPublisher adapter)
Phase 7: Migration order (safe iterations)
1.	Group code into partials (Phase 1).
2.	Create CarrierJumpManager and switch GameStateService_CarrierJump to call into it.
3.	Extract CarrierCargoService and ColonizationService (update calls).
4.	Extract JournalReader and event-based dispatch (replace direct parsing).
5.	Extract GameFilesService and FileWatcherService.
6.	Add interfaces + DI.
7.	Remove deprecated internal methods from GameStateService once compiled and tested.
Phase 8: Acceptance criteria
•	No public property changes in GameStateService used by ViewModels.
•	All existing ViewModels continue to work (no XAML changes).
•	Unit tests for:
•	JournalReader parsing (sample lines)
•	CarrierJumpManager overlay activation rules
•	Colonization completion from sample JSON
•	Cargo normalization and persistence
•	Manual test:
•	Start after jump scheduled: countdown shows; overlay behavior per rules.
•	Colonization completion moves to 100% and persists.
•	Summary card shows commander/ship/fuel status.
Notes
•	Keep all services UI-agnostic; only GameStateService handles Dispatcher marshalling.
•	Log within services; GameStateService logs state transitions and INPC triggers.
•	Start with internal classes; introduce interfaces and DI last to keep early PRs small.