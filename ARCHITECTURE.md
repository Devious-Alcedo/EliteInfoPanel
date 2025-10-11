# EliteInfoPanel - Architectural Overview

## Project Summary
**EliteInfoPanel** is a WPF .NET 9 application that provides a real-time information overlay for Elite Dangerous players. It monitors the game's journal files and displays dynamic information cards about cargo, ship status, navigation routes, and more.

**Technology Stack:**
- .NET 9 / WPF
- MaterialDesignThemes for UI
- MVVM architecture pattern
- Serilog for logging
- MQTTnet for messaging
- File system watchers for real-time monitoring

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ MainWindow   │  │   Dialogs    │  │   Controls   │         │
│  │   (XAML)     │  │   (XAML)     │  │   (Cards)    │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        VIEWMODEL LAYER                           │
│  ┌──────────────────┐    ┌─────────────────────────────┐       │
│  │  MainViewModel   │◄───┤  CardViewModel (Abstract)   │       │
│  │  - Layout Mgmt   │    │  - Base functionality       │       │
│  │  - Card Orchestr │    └─────────────────────────────┘       │
│  └──────────────────┘              ▲                             │
│                          ┌──────────┴───────────────┐           │
│                          │ Concrete ViewModels:     │           │
│                          │ - CargoViewModel         │           │
│                          │ - BackpackViewModel      │           │
│                          │ - RouteViewModel         │           │
│                          │ - ModulesViewModel       │           │
│                          │ - SummaryViewModel       │           │
│                          │ - FlagsViewModel         │           │
│                          │ - ColonizationViewModel  │           │
│                          │ - FleetCarrierViewModel  │           │
│                          └──────────────────────────┘           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    BUSINESS LOGIC / SERVICES                     │
│  ┌──────────────────────────────────────────────────┐           │
│  │            GameStateService (CORE)               │           │
│  │  - Central state management                      │           │
│  │  - File watching (Status, Cargo, Journal, etc.)  │           │
│  │  - Journal event processing                      │           │
│  │  - State calculations & aggregation              │           │
│  │  - PropertyChanged notifications                 │           │
│  └──────────────────────────────────────────────────┘           │
│                                                                   │
│  ┌──────────────────┐    ┌──────────────────┐                  │
│  │  MqttService     │    │ JournalService   │ ⚠️              │
│  │  - Publishing    │    │ (Underutilized)  │                  │
│  └──────────────────┘    └──────────────────┘                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        UTILITIES / HELPERS                       │
│  ┌──────────────────┐  ┌────────────────┐  ┌───────────────┐  │
│  │ CardLayoutMgr    │  │ EventAggregator│  │ SettingsMgr   │  │
│  │ - Dynamic layout │  │ - Event bus    │  │ - Config mgmt │  │
│  └──────────────────┘  └────────────────┘  └───────────────┘  │
│                                                                   │
│  ┌──────────────────┐  ┌────────────────┐  ┌───────────────┐  │
│  │ CommodityMapper  │  │ ModuleNameMap  │  │ EliteThemeMgr │  │
│  │ ShipNameHelper   │  │ FlagMetadata   │  │ StarIconMap   │  │
│  └──────────────────┘  └────────────────┘  └───────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Directory Structure & File Roles

### **Root Directory**
- **MainWindow.xaml/.cs** - Primary application window, window positioning, keyboard shortcuts
- **App.xaml/.cs** - Application entry point, resource initialization
- **AssemblyInfo.cs** - Assembly metadata
- **EliteInfoPanel.csproj** - Project file with dependencies
- **Settings.settings/.Designer.cs** - Application settings schema

### **ViewModels/** - MVVM ViewModels
All ViewModels inherit from `ViewModelBase` which provides `INotifyPropertyChanged` implementation.

- **ViewModelBase.cs** - Abstract base class with property change tracking, batching, UI thread marshalling
- **CardViewModel.cs** - Base for all card ViewModels with visibility logic
- **MainViewModel.cs** - Orchestrates all cards, manages layout, handles game state subscriptions
- **BackpackViewModel.cs** - Displays on-foot inventory
- **CargoViewModel.cs** - Displays ship cargo hold contents
- **FleetCarrierCargoViewModel.cs** - Fleet carrier cargo tracking
- **ColonizationViewModel.cs** - Colonization project progress
- **EliteRankViewModel.cs** - Commander rank display
- **FlagsViewModel.cs** - Game status flags (docked, supercruise, etc.)
- **ModulesViewModel.cs** - Ship module loadout
- **RouteViewModel.cs** - Navigation route progress
- **SummaryViewModel.cs** - Commander summary info
- **SettingsViewModel.cs** - Settings dialog ViewModel
- **RelayCommand.cs** - ICommand implementation for button bindings

### **Controls/** - Reusable UI Controls (XAML + Code-behind)
Each card has a paired XAML view and code-behind file.

- **BackpackCard.xaml/.cs** - Backpack inventory UI
- **CargoCard.xaml/.cs** - Cargo hold UI
- **FleetCarrierCargoCard.xaml/.cs** - Fleet carrier cargo UI
- **ColonizationCard.xaml/.cs** - Colonization project UI
- **FlagsCard.xaml/.cs** - Status flags UI
- **ModulesCard.xaml/.cs** - Ship modules UI
- **RouteCard.xaml/.cs** - Navigation route UI
- **SummaryCard.xaml/.cs** - Commander summary UI
- **HyperspaceOverlay.xaml/.cs** - Fullscreen hyperspace jump animation
- **CarrierJumpOverlay.xaml/.cs** - Fleet carrier jump overlay
- **EditCargoItemDialog.xaml/.cs** - Manual cargo editing dialog
- **OrderableCheckBoxList.xaml/.cs** - Reorderable checkbox list control
- **InverseBooleanConverter.cs** ⚠️ **MISPLACED** - Should be in Converters/

### **Dialogs/** - Application Dialogs
- **OptionsWindow.xaml/.cs** - Settings/preferences dialog
- **SelectScreenDialog.xaml/.cs** - Multi-monitor screen selection

### **Core/** - Business Logic & Domain Models
- **GameStateService.cs** 🔴 **OVERLOADED** - Central service managing ALL game state:
  - File system watching (Status.json, Cargo.json, NavRoute.json, Journal files)
  - Journal event processing
  - State calculations and aggregation
  - Property change notifications
  - Carrier jump logic
  - Colonization tracking
  - Cargo tracking
  
- **CarrierCargoTracker.cs** - Fleet carrier cargo state management
- **JournalEntry.cs** - Journal event models
- **StatusJson.cs** - Status.json file model
- **CargoJson.cs** - Cargo.json file model
- **BackpackJson.cs** - Backpack.json file model
- **LoadoutJson.cs** - Loadout.json file model
- **LoadoutModule.cs** - Module details model
- **FCMaterialsJson.cs** - Fleet carrier materials model
- **NavRoute.cs** - Navigation route model
- **DisplayOptions.cs** - Display settings
- **EliteDangerousPaths.cs** - File path resolution
- **Engineering.cs** - Engineering-related models
- **EdhmColorExtractor.cs** - HUD color extraction
- **EliteHudColorExtractor.cs** - Alternative HUD color extraction

#### **Core/Models/** - Domain Models
- **CarrierCargoItem.cs** - Individual carrier cargo item
- **ColonizationData.cs** - Colonization depot data
- **ManualCargoChange.cs** - Manual cargo adjustment tracking
- **RouteProgressState.cs** - Route completion tracking

### **Services/** - External Services
- **JournalService.cs** ⚠️ **POTENTIALLY REDUNDANT** - Appears to duplicate GameStateService functionality
- **MqttService.cs** - MQTT message publishing

### **Converters/** - WPF Value Converters
- **CountToVisibilityConverter.cs** - Collection count → Visibility
- **EvenIndexConverter.cs** - Index parity checking
- **MultiplyConverter.cs** - Numeric multiplication
- **PercentToGridLengthConverter.cs** - Percentage → GridLength
- **RectConverter.cs** - Rectangle conversions
- **ScaleToFontSizeConverter.cs** - Scale factor → font size
- **TagFilterConverter.cs** - Tag-based filtering

### **Util/** - Utility Classes
- **AppSettings.cs** - Application settings model
- **SettingsManager.cs** - Settings persistence
- **CardLayoutManager.cs** - Dynamic card layout management
- **EventAggregator.cs** - Simple event bus for decoupled messaging
- **EliteThemeManager.cs** - Theme and HUD color management
- **CommodityMapper.cs** - Commodity name normalization
- **ModuleNameMapper.cs** - Module name localization
- **ShipNameHelper.cs** - Ship name localization
- **StarIconMapper.cs** - Star class → icon mapping
- **FlagMetadata.cs** - Status flag definitions
- **FsdJumpRangeCalculator.cs** - Jump range calculations
- **ModuleSlotHelper.cs** - Module slot utilities
- **SyntheticFlags.cs** - Derived game state flags
- **DeferredUpdate.cs** - Deferred update helper
- **LoggingConfig.cs** - Serilog configuration
- **VectorUtil.cs** - Vector mathematics
- **ProgressToWidthConverter.cs** ⚠️ **MISPLACED** - Should be in Converters/

### **Assets/** - Static Resources
- **Ranks/** - Elite rank icons (Combat, Trade, Explorer, etc.)
- **Stars/** - Star type icons (O, B, A, F, G, K, M, etc.)

### **Data Files**
- **commodity_mapping.json** - Commodity name mappings
- **ModuleNameMap.json** - Module name localizations
- **ships.csv** - Ship database
- **outfitting.csv** - Module database

---

## Key Design Patterns

### **MVVM (Model-View-ViewModel)**
- **Models**: Core/\*.cs (GameStateService, JSON models)
- **Views**: MainWindow.xaml, Controls/\*.xaml, Dialogs/\*.xaml
- **ViewModels**: ViewModels/\*.cs
- Clear separation between UI and business logic
- Data binding for automatic UI updates

### **Observer Pattern**
- `INotifyPropertyChanged` in ViewModels for property change notifications
- `EventAggregator` for cross-component messaging
- Event subscriptions in GameStateService

### **File System Watcher Pattern**
- Real-time monitoring of Elite Dangerous journal files
- Automatic updates when game state changes

### **Command Pattern**
- `RelayCommand` for button click handling
- Decouples UI actions from business logic

### **Singleton Pattern**
- `EventAggregator.Instance`
- `EliteThemeManager.Instance`
- `MqttService.Instance`

---

## Identified Issues & Technical Debt

### 🔴 **Critical Issues**

1. **GameStateService is a God Object**
   - **Problem**: Handles file watching, journal parsing, state management, notifications, MQTT publishing
   - **Impact**: >2000 lines, difficult to test, maintain, and extend
   - **Recommended Fix**: Split into:
     ```
     GameStateService (state only)
       ↓
     JournalMonitorService (file watching + parsing)
       ↓
     CargoTrackingService (carrier cargo logic)
       ↓
     ColonizationService (colonization tracking)
     ```

2. **MainWindow.xaml.cs has excessive code-behind**
   - **Problem**: Contains window positioning, settings management, overlay logic
   - **Impact**: Violates MVVM, hard to test
   - **Recommended Fix**: Move logic to:
     - `WindowManager` service for positioning
     - Delegate more to `MainViewModel`

### ⚠️ **Medium Priority Issues**

3. **JournalService appears redundant**
   - **Problem**: GameStateService already processes journal events
   - **Impact**: Code duplication, confusion about responsibility
   - **Recommended Action**: Investigate usage, consider removal or clarify role

4. **Misplaced files**
   - **InverseBooleanConverter.cs** in Controls/ → should be in Converters/
   - **ProgressToWidthConverter.cs** in Util/ → should be in Converters/
   - **Impact**: Poor discoverability, inconsistent organization

5. **CardLayoutManager caching complexity**
   - **Problem**: Manual card element caching may be unnecessary with WPF virtualization
   - **Impact**: Added complexity, potential memory overhead
   - **Recommended Fix**: Evaluate if caching provides measurable benefit

### 💡 **Low Priority / Future Improvements**

6. **Converters organization**
   - Some converters could be combined (e.g., generic scaling converter)
   - Consider using `IMultiValueConverter` for complex scenarios

7. **Service location anti-pattern**
   - Some singletons accessed via `.Instance` instead of dependency injection
   - **Recommended**: Consider using DI container for better testability

8. **Batch update system complexity**
   - `GameStateService.BeginUpdate()` / `EndUpdate()` pattern is error-prone
   - **Recommended**: Consider Reactive Extensions (Rx.NET) for throttling

9. **Hard-coded UI logic in ViewModels**
   - Some ViewModels have specific layout awareness
   - **Recommended**: Use attached behaviors or data templates

---

## Recommended Refactoring Plan

### Phase 1: Quick Wins (Low Risk)
1. ✅ Move `InverseBooleanConverter.cs` to Converters/
2. ✅ Move `ProgressToWidthConverter.cs` to Converters/
3. ✅ Document the role of JournalService or remove if unused
4. ✅ Extract window positioning logic from MainWindow to `WindowPositioningService`

### Phase 2: Service Decomposition (Medium Risk)
1. Create `JournalMonitorService` to handle file watching and parsing
2. Create `CargoTrackingService` for carrier cargo logic
3. Create `ColonizationService` for depot tracking
4. Refactor `GameStateService` to use these new services
5. Update unit tests (if they exist) or create them

### Phase 3: MVVM Refinement (Medium Risk)
1. Move MainWindow logic to MainViewModel
2. Create `WindowManager` for window positioning
3. Remove code-behind except for event handlers that must be in code-behind

### Phase 4: Architecture Modernization (Higher Risk)
1. Introduce dependency injection container (Microsoft.Extensions.DependencyInjection)
2. Replace manual property change tracking with Reactive Extensions
3. Consider migrating to Community Toolkit MVVM for modern MVVM helpers

---

## Testing Strategy

### Current State
- No apparent unit test project in solution
- Testing likely done manually

### Recommended Approach
1. **Unit Tests**: Test ViewModels and Services in isolation
   - Use mocking framework (Moq, NSubstitute) for dependencies
   - Test GameStateService state transitions
   - Test ViewModel property change notifications

2. **Integration Tests**: Test file watching and journal processing
   - Create test journal files
   - Verify correct state updates

3. **UI Tests**: (Optional) Use FlakyTest or Coded UI Tests
   - Verify card visibility logic
   - Verify layout behavior

---

## Dependencies

### NuGet Packages
- **MaterialDesignThemes** (v5.2.1) - Material Design UI framework
- **MaterialDesignColors** (v5.2.1) - Color schemes
- **MaterialDesignThemes.MahApps** (v5.2.1) - MahApps integration
- **MQTTnet** (v5.0.1) - MQTT client
- **Serilog** (v4.3.0) - Structured logging
- **Serilog.Sinks.Console** (v6.0.0) - Console logging
- **Serilog.Sinks.File** (v7.0.0) - File logging
- **WpfScreenHelper** (v2.1.1) - Multi-monitor support
- **System.Windows.Extensions** (v9.0.5) - System utilities
- **TextCopy** (v6.2.1) - Clipboard operations

### External Dependencies
- **Elite Dangerous Game Files**: Application monitors journal files in:
  - `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\`

---

## Performance Considerations

### Current Optimizations
1. **Batch updates** in `GameStateService` to reduce property change notifications
2. **Card caching** in `CardLayoutManager` to avoid recreating UI elements
3. **Deferred updates** via `DeferredUpdate` helper
4. **File position tracking** in journal processing to avoid re-reading entire files

### Potential Bottlenecks
1. **Frequent layout recalculations** when card visibility changes
2. **Large journal file processing** on startup (mitigated by position tracking)
3. **PropertyChanged notifications** causing unnecessary UI updates

### Recommended Optimizations
1. Use `VirtualizingStackPanel` for lists with many items
2. Throttle `PropertyChanged` notifications using Reactive Extensions
3. Profile with PerfView or dotMemory to identify actual bottlenecks

---

## Conclusion

EliteInfoPanel is a well-structured WPF application that successfully implements MVVM architecture with MaterialDesign theming. The main area for improvement is **decomposing the GameStateService** into smaller, more focused services to improve maintainability and testability.

The application demonstrates good practices in:
- ✅ Clear MVVM separation
- ✅ Real-time file monitoring
- ✅ Dynamic UI layout
- ✅ Comprehensive logging
- ✅ MaterialDesign integration

Areas for improvement:
- 🔴 GameStateService decomposition
- ⚠️ Reduce code-behind in MainWindow
- ⚠️ File organization (converters)
- 💡 Introduce dependency injection
- 💡 Add unit tests

---

**Document Version**: 1.0  
**Last Updated**: 2025-10-11  
**Authors**: Claude (Architectural Analysis)
