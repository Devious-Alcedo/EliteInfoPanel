# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EliteInfoPanel is a WPF .NET 9 application that provides a real-time information overlay for Elite Dangerous players. It monitors the game's journal files and displays dynamic information cards showing cargo, ship status, navigation routes, colonization tracking, and fleet carrier management.

## Build and Development Commands

### Building the Application
```bash
# Build the solution
dotnet build EliteInfoPanel.sln

# Build in Release mode
dotnet build EliteInfoPanel.sln -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

### Running the Application
```bash
# Run from the project directory
dotnet run --project EliteInfoPanel.csproj

# Run in Release mode
dotnet run --project EliteInfoPanel.csproj -c Release
```

### Testing
There is currently no test project. When adding tests:
```bash
# Run all tests (when test project exists)
dotnet test

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Architecture Overview

### Technology Stack
- .NET 9 with WPF
- MVVM architecture pattern
- MaterialDesignThemes v5.2.1 for UI
- MQTTnet v5.0.1 for Home Assistant integration
- Serilog v4.3.0 for structured logging
- WpfScreenHelper v2.1.1 for multi-monitor support

### Key Architectural Patterns

**MVVM Structure:**
```
MainWindow → MainViewModel → CardViewModels → GameStateService
```
- All ViewModels inherit from `ViewModelBase` (implements `INotifyPropertyChanged`)
- CardViewModels subscribe to `GameStateService.PropertyChanged` for reactive updates
- GameStateService is the central singleton holding all game state

**GameStateService Partial Class Architecture (Phase 1 Refactor Complete):**

The GameStateService was recently refactored from a monolithic class into focused partial classes:

| Partial File | Responsibility |
|-------------|----------------|
| `GameStateService.cs` | Core state properties, initialization |
| `GameStateService_FileWatchers.cs` | File system watchers for Status.json, Cargo.json, NavRoute.json, Backpack.json, FCMaterials.json, Journal.log |
| `GameStateService_Journal.cs` | Journal event parsing, historical filtering |
| `GameStateService_Cargo.cs` | Carrier cargo tracking, ship cargo sync |
| `GameStateService_CarrierJump.cs` | Fleet carrier jump scheduling and overlays |
| `GameStateService_Colonization.cs` | Colonization depot tracking |
| `GameStateService_Status.cs` | Status.json loading |
| `GameStateService_Route.cs` | Navigation route tracking |
| `GameStateService_Mqtt.cs` | MQTT publishing |
| `GameStateService_Utils.cs` | Utility methods |
| `GameStateService.UpdateScope.cs` | Batch update mechanism |

**Batch Update Pattern:**
```csharp
using (BeginUpdate()) // Suspends PropertyChanged notifications
{
    // Multiple property assignments
} // EndUpdate() fires single PropertyChanged
```
This prevents excessive UI re-renders during complex state changes.

**Journal Processing Flow:**
1. FileSystemWatcher detects Journal file changes (100ms debounce)
2. `ProcessJournalAsync()` reads new lines from tracked file position
3. Events are parsed as JSON and dispatched by type
4. **Critical Filter**: Skips historical cargo events during initial load and events predating app startup (`_appStartTimeUtc`)
5. Updates GameStateService properties which trigger PropertyChanged
6. ViewModels react to property changes and update UI

**Carrier Cargo Tracking:**
- `CarrierCargoTracker` processes: `CarrierTradeOrder`, `CargoDepot`, `MarketBuy`, `MarketSell`, `CargoTransfer`
- Normalizes commodity names via `CommodityMapper`
- Tracks bidirectional transfers (tocarrier vs fromcarrier)
- Persists to `carrier_cargo_state.json` in AppData
- Supports manual adjustments via UI dialog

**MQTT Integration (Home Assistant):**
- `MqttService.Instance` singleton manages connection
- Publishes game state flags as binary sensors with auto-discovery
- Topics: `homeassistant/binary_sensor/eliteinfopanel_*/config` and `*/state`
- Tracks sent configs to avoid duplicates (`_haConfigSent`)

### Important Code Conventions

**ViewModelBase Property Pattern:**
```csharp
private int _combatRank;
public int CombatRank
{
    get => _combatRank;
    private set => SetProperty(ref _combatRank, value);
}
```

**Event Aggregator Pattern:**
```csharp
EventAggregator.Instance.Subscribe<CardVisibilityChangedEvent>(OnCardVisibilityChanged);
EventAggregator.Instance.Publish(new LayoutRefreshRequestEvent { ForceRebuild = true });
```

**Historical Event Filtering:**
```csharp
// GameStateService tracks app start time
private readonly DateTime _appStartTimeUtc = DateTime.UtcNow;

// Skip stale events
if (eventTimestamp < _appStartTimeUtc) continue;
```

**File Watcher Debounce Pattern:**
```csharp
var debounceTimer = new Timer(100) { AutoReset = false };
watcher.Changed += (s, e) => {
    debounceTimer.Stop();
    debounceTimer.Start();
};
```

## Key Directories

- **Core/** - GameStateService partial classes, JSON models, domain models
- **ViewModels/** - All ViewModels inherit from ViewModelBase
- **Controls/** - XAML cards and UI controls
- **Dialogs/** - Settings and selection dialogs
- **Services/** - MqttService, JournalService (note: JournalService may be underutilized)
- **Converters/** - WPF value converters for data binding
- **Util/** - CardLayoutManager, EventAggregator, SettingsManager, mappers
- **Assets/** - Rank icons, star type icons

## File Locations and Persistence

### Elite Dangerous Game Files
The application monitors journal files in:
- `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\`

### Application Data Files
Persisted state is stored in:
- `%APPDATA%\EliteInfoPanel\`
  - `carrier_cargo_state.json` - Carrier cargo tracking
  - `ColonizationData.json` - Colonization depot progress
  - `RouteProgress.json` - Navigation route state

### Manual Cargo Integration
- `%LOCALAPPDATA%\EliteCompanion\ManualCarrierCargo.json` - Manual cargo adjustments

## Ongoing Refactoring (See refactor.md)

**Phase 1 (Complete):** Partials created to group responsibilities
**Phase 2 (Next):** Extract internal services (JournalReader, CarrierJumpManager, etc.)
**Phase 3 (Future):** Introduce interfaces and dependency injection
**Phase 4-8:** Event model, threading, persistence normalization, API boundaries

**Key Principle:** GameStateService remains the public-facing singleton facade. All ViewModels continue to work without changes. Services are UI-agnostic; only GameStateService marshals to UI thread.

## Important Considerations When Making Changes

### When Modifying GameStateService
- Identify which partial class handles the concern
- Use `BeginUpdate()/EndUpdate()` for batch property changes
- All property setters must use `SetProperty()` for INPC
- Log state transitions for debugging
- Respect the historical event filter (`_appStartTimeUtc`)

### When Adding New Cards
1. Create ViewModel inheriting from `CardViewModel`
2. Subscribe to relevant GameStateService properties in constructor
3. Create XAML + code-behind in Controls/
4. Add to MainViewModel card creation
5. Update CardLayoutManager if custom layout needed

### When Adding Journal Event Handling
1. Add case in `GameStateService_Journal.ProcessJournalAsync()`
2. Parse JSON event with appropriate model
3. Update relevant properties within `BeginUpdate()` scope
4. Consider timestamp filtering for cargo-related events

### When Modifying Cargo Tracking
- All carrier cargo updates go through `CarrierCargoTracker`
- Always normalize commodity names via `CommodityMapper.Instance`
- Persist after changes to ensure state recovery
- Handle duplicate keys via `CleanupDuplicateCargoEntries()`

### Threading Considerations
- GameStateService runs on UI thread (Dispatcher)
- File watchers trigger on background threads
- Always marshal PropertyChanged to Dispatcher
- Use `Application.Current.Dispatcher.Invoke()` when updating from background

## MaterialDesign Theming

The application uses MaterialDesignThemes with custom Elite Dangerous HUD color extraction:
- `EliteThemeManager.Instance` manages theme colors
- Colors extracted from `GraphicsConfiguration.xml` (EDHM format)
- Supports dynamic color updates

## Known Issues and Technical Debt

1. **GameStateService Decomposition**: Phase 1 partials complete; continue with Phase 2 service extraction (see refactor.md)
2. **JournalService Redundancy**: Appears underutilized; GameStateService handles journal processing
3. **No Test Coverage**: No test project exists; recommend xUnit + Moq when adding tests
4. **Service Locator Pattern**: Singletons accessed via `.Instance`; DI planned for Phase 3

## Resources

- **ARCHITECTURE.md**: Comprehensive architectural documentation with diagrams
- **refactor.md**: Detailed refactoring plan and phase breakdown
- **README.md**: User-facing documentation, features, and keyboard shortcuts
