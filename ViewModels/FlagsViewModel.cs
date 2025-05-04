using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class FlagsViewModel : CardViewModel
    {
        #region Private Fields
        private readonly GameStateService _gameState;
        private readonly AppSettings _appSettings;
        private HashSet<Flag> _lastActiveFlags = new HashSet<Flag>();
        private List<Flags2> _defaultVisibleFlags2 = new List<Flags2>();
        private List<Flag> _lastVisibleFlags = new List<Flag>();
        private bool _lastDockedState;
        private bool _lastHyperspaceState;
        private uint _lastStatusFlags;
        #endregion

        #region Public Properties
        public override double FontSize
        {
            get => base.FontSize;
            set
            {
                if (base.FontSize != value)
                {
                    base.FontSize = value;

                    foreach (var item in Items)
                    {
                        item.FontSize = (int)value;
                    }
                }
            }
        }

        public ObservableCollection<FlagItemViewModel> Items { get; } = new();
        #endregion

        #region Constructor
        public FlagsViewModel(GameStateService gameState) : base("Status Flags")
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

            // Initialize default visible flags
            InitializeDefaultFlags();

            // Subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Debug: Log all available flag metadata
            FlagVisualHelper.LogAllMetadata();

            // Update flags if game state is already loaded
            if (_gameState.FirstLoadCompleted)
            {
                Log.Information("FlagsViewModel: GameState already loaded, updating flags immediately");
                UpdateFlags();
            }
            else
            {
                Log.Information("FlagsViewModel: Waiting for GameState to complete loading");
                _gameState.FirstLoadCompletedEvent += () => {
                    Log.Information("FlagsViewModel: GameState loading completed, now updating flags");
                    UpdateFlags();
                };
            }
        }
        #endregion

        #region Private Fields Declaration
        private List<Flag> _defaultVisibleFlags = new List<Flag>
        {
            Flag.Docked,
            Flag.LandingGearDown,
            Flag.ShieldsUp,
            Flag.Supercruise,
            Flag.FlightAssistOff,
            Flag.HardpointsDeployed,
            Flag.LightsOn,            // Added LightsOn
            Flag.NightVision,         // Added NightVision
            Flag.CargoScoopDeployed,
            Flag.SilentRunning,
            SyntheticFlags.HudInCombatMode,
            SyntheticFlags.Docking
        };
        #endregion

        #region Private Methods
        private void InitializeDefaultFlags()
        {
            // Primary flags to display by default
            _defaultVisibleFlags = new List<Flag>
            {
                Flag.Docked,
                Flag.Landed,
                Flag.LandingGearDown,
                Flag.ShieldsUp,
                Flag.Supercruise,
                Flag.FlightAssistOff,
                Flag.HardpointsDeployed,
                Flag.InWing,
                Flag.LightsOn,
                Flag.CargoScoopDeployed,
                Flag.SilentRunning,
                Flag.ScoopingFuel,
                Flag.FsdMassLocked,
                Flag.FsdCharging,
                Flag.FsdCooldown,
                Flag.LowFuel,
                Flag.Overheating,
                Flag.IsInDanger,
                Flag.BeingInterdicted,
                Flag.NightVision,
                Flag.FsdJump,
                SyntheticFlags.HudInCombatMode,
                SyntheticFlags.Docking
            };

            // Flags2 to display by default
            _defaultVisibleFlags2 = new List<Flags2>
            {
                Flags2.OnFoot,
                Flags2.InTaxi,
                Flags2.InMulticrew,
                Flags2.AimDownSight,
                Flags2.LowOxygen,
                Flags2.LowHealth,
                Flags2.Cold,
                Flags2.Hot,
                Flags2.VeryCold,
                Flags2.VeryHot,
                Flags2.GlideMode,
                Flags2.BreathableAtmosphere,
                Flags2.FsdHyperdriveCharging
            };
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Expand the properties we listen to
            if (e.PropertyName == nameof(GameStateService.CurrentStatus) ||
                e.PropertyName == nameof(GameStateService.IsDocking) ||
                e.PropertyName == nameof(GameStateService.IsHyperspaceJumping) ||
                e.PropertyName == nameof(GameStateService.CurrentSystem))
            {
                Log.Debug("FlagsViewModel: Detected change in {Property}", e.PropertyName);
                UpdateFlags();
            }
        }

        private void UpdateFlags()
        {
            if (_gameState.CurrentStatus == null)
                return;

            RunOnUIThread(() => {
                try
                {
                    // Get settings and visible flags
                    var settings = SettingsManager.Load();
                    var visibleFlags = settings.DisplayOptions.VisibleFlags;

                    // Log the raw flags values for debugging
                    uint currentStatusFlags = (uint)_gameState.CurrentStatus.Flags;
                    int flags2Value = _gameState.CurrentStatus.Flags2;
                    Log.Information("Updating flags - Raw Flags value: 0x{RawFlags:X8}, Flags2: {Flags2}",
                        currentStatusFlags, flags2Value);

                    // Use default flags if none defined
                    if (visibleFlags == null || visibleFlags.Count == 0)
                    {
                        Log.Warning("No visible flags defined in settings! Using default flags.");
                        visibleFlags = _defaultVisibleFlags;

                        // Save to settings
                        settings.DisplayOptions.VisibleFlags = visibleFlags;
                        SettingsManager.Save(settings);
                    }

                    // Get all currently active flags
                    var activeFlags = new List<Flag>();
                    foreach (Flag flag in Enum.GetValues(typeof(Flag)))
                    {
                        if (flag != Flag.None && _gameState.CurrentStatus.Flags.HasFlag(flag))
                        {
                            activeFlags.Add(flag);
                        }
                    }

                    // Add synthetic flags if their conditions are met
                    if (!_gameState.CurrentStatus.Flags.HasFlag(Flag.HudInAnalysisMode))
                    {
                        activeFlags.Add(SyntheticFlags.HudInCombatMode);
                    }

                    if (_gameState.IsDocking && !_gameState.CurrentStatus.Flags.HasFlag(Flag.Docked))
                    {
                        activeFlags.Add(SyntheticFlags.Docking);
                    }

                    // Get active Flags2 values
                    var activeFlags2 = new List<Flags2>();
                    foreach (Flags2 flag in Enum.GetValues(typeof(Flags2)))
                    {
                        if (flag != Flags2.None && (_gameState.CurrentStatus.Flags2 & (int)flag) != 0)
                        {
                            activeFlags2.Add(flag);
                        }
                    }

                    // Log active flags for debugging
                    Log.Information("Current active flags: {ActiveFlags}",
                        string.Join(", ", activeFlags.Select(f => f.ToString())));
                    Log.Information("Current active Flags2: {ActiveFlags2}",
                        string.Join(", ", activeFlags2.Select(f => f.ToString())));
                    Log.Information("Visible flags set in settings: {VisibleFlags}",
                        string.Join(", ", visibleFlags.Select(f => f.ToString())));

                    // Clear items and rebuild
                    Items.Clear();

                    // Process primary flags
                    foreach (var flag in visibleFlags)
                    {
                        bool shouldShow = false;

                        // Special handling for synthetic flags
                        if (flag == SyntheticFlags.HudInCombatMode)
                        {
                            shouldShow = !_gameState.CurrentStatus.Flags.HasFlag(Flag.HudInAnalysisMode);
                            Log.Debug("Synthetic flag HudInCombatMode: {ShouldShow}", shouldShow);
                        }
                        else if (flag == SyntheticFlags.Docking)
                        {
                            shouldShow = !_gameState.CurrentStatus.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking;
                            Log.Debug("Synthetic flag Docking: {ShouldShow} (IsDocking={IsDocking})",
                                shouldShow, _gameState.IsDocking);
                        }
                        // Regular flags
                        else
                        {
                            shouldShow = _gameState.CurrentStatus.Flags.HasFlag(flag);
                            Log.Debug("Standard flag {Flag}: {ShouldShow}", flag, shouldShow);
                        }

                        if (shouldShow)
                        {
                            AddFlagToItems(flag);
                        }
                    }

                    // Process Flags2 values
                    foreach (var flag in _defaultVisibleFlags2)
                    {
                        bool shouldShow = (_gameState.CurrentStatus.Flags2 & (int)flag) != 0;
                        Log.Debug("Flags2 {Flag}: {ShouldShow}", flag, shouldShow);

                        if (shouldShow)
                        {
                            AddFlags2ToItems(flag);
                        }
                    }

                    // Update visibility based on items count
                    bool hasFlags = Items.Count > 0;
                    Log.Information("Setting FlagsCard visibility: {Visible} (Items count: {Count})",
                        hasFlags, Items.Count);
                    IsVisible = hasFlags && SettingsManager.Load().ShowFlags;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error updating flags");
                }
            });
        }

        private List<Flag> GetDefaultFlags()
        {
            return new List<Flag>
            {
                Flag.Docked,
                Flag.LandingGearDown,
                Flag.ShieldsUp,
                Flag.Supercruise,
                Flag.FlightAssistOff,
                Flag.HardpointsDeployed,
                Flag.CargoScoopDeployed,
                Flag.SilentRunning,
                Flag.LightsOn,
                Flag.NightVision,
                SyntheticFlags.HudInCombatMode,
                SyntheticFlags.Docking
            };
        }

        private void AddFlagToItems(Flag flag)
        {
            try
            {
                if (!FlagVisualHelper.TryGetMetadata(flag, out var meta))
                {
                    meta = ("Flag", flag.ToString().Replace("_", " "), Brushes.Gray);
                    Log.Warning("No visual metadata found for flag: {Flag}", flag);
                }

                // Create and add the flag item
                var flagItem = new FlagItemViewModel(
                    flag,
                    meta.Tooltip,
                    meta.Icon,
                    flag.ToString(),
                    meta.Color)
                {
                    FontSize = (int)this.FontSize
                };

                // Add to collection
                Items.Add(flagItem);

                // Log success
                Log.Information("Added flag to Items: {Flag}", flag);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add flag {Flag} to Items", flag);
            }
        }

        private void AddFlags2ToItems(Flags2 flag)
        {
            try
            {
                if (!Flags2VisualHelper.TryGetMetadata(flag, out var meta))
                {
                    meta = ("Flag", flag.ToString().Replace("_", " "), Brushes.Gray);
                    Log.Warning("No visual metadata found for Flags2: {Flag}", flag);
                }

                // Create and add the flag item
                var flagItem = new FlagItemViewModel(
                    flag,
                    meta.Tooltip,
                    meta.Icon,
                    flag.ToString(),
                    meta.Color)
                {
                    FontSize = (int)this.FontSize
                };

                // Add to collection
                Items.Add(flagItem);

                // Log success
                Log.Information("Added Flags2 to Items: {Flag}", flag);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add Flags2 {Flag} to Items", flag);
            }
        }
        #endregion
    }

    public class FlagItemViewModel : ViewModelBase
    {
        #region Private Fields
        private object _flagValue; // Can be either Flag or Flags2
        private string _displayText;
        private string _icon;
        private string _tooltip;
        private Brush _iconColor = Brushes.White;
        private int _fontSize = 14;
        private readonly string _flagTypeDescription; // "Flag" or "Flags2"
        #endregion

        #region Public Properties
        public object FlagValue
        {
            get => _flagValue;
            set => SetProperty(ref _flagValue, value);
        }

        public Flag Flag => _flagValue is Flag flag ? flag : Flag.None;

        public Flags2 Flags2 => _flagValue is Flags2 flags2 ? flags2 : Flags2.None;

        public string FlagType => _flagTypeDescription;

        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        public Brush IconColor
        {
            get => _iconColor;
            set => SetProperty(ref _iconColor, value);
        }

        public PackIconKind IconKind
        {
            get
            {
                if (Enum.TryParse(typeof(PackIconKind), _icon, out var kind))
                {
                    return (PackIconKind)kind;
                }
                return PackIconKind.Help; // Default
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }
        #endregion

        #region Constructors
        // Constructor for primary Flag enum
        public FlagItemViewModel(Flag flag, string displayText, string icon, string tooltip, Brush iconColor = null)
        {
            _flagValue = flag;
            _displayText = displayText;
            _icon = icon;
            _tooltip = tooltip;
            _iconColor = iconColor ?? Brushes.White;
            _flagTypeDescription = "Flag";
        }

        // Constructor for Flags2 enum
        public FlagItemViewModel(Flags2 flag, string displayText, string icon, string tooltip, Brush iconColor = null)
        {
            _flagValue = flag;
            _displayText = displayText;
            _icon = icon;
            _tooltip = tooltip;
            _iconColor = iconColor ?? Brushes.White;
            _flagTypeDescription = "Flags2";
        }
        #endregion
    }
}