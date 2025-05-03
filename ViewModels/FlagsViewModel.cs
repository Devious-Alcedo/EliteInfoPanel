using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;
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
            _appSettings = SettingsManager.Load();

            // Subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Subscribe to first load completed event
            if (_gameState.FirstLoadCompleted)
            {
                // Data already loaded, update immediately
                Log.Information("FlagsViewModel: GameState already loaded, updating flags immediately");
                UpdateFlags();
            }
            else
            {
                // Wait for data to be loaded
                Log.Information("FlagsViewModel: Waiting for GameState to complete loading");
                _gameState.FirstLoadCompletedEvent += () => {
                    Log.Information("FlagsViewModel: GameState loading completed, now updating flags");
                    UpdateFlags();
                };
            }

   
        }
        #endregion

        #region Private Methods
        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //log all flags we receive
          
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

                    // Log the raw flags value for debugging
                    uint currentStatusFlags = (uint)_gameState.CurrentStatus.Flags;
                    Log.Information("Updating flags - Raw flags value: 0x{RawFlags:X8}", currentStatusFlags);

                    // Use default flags if none defined
                    if (visibleFlags == null || visibleFlags.Count == 0)
                    {
                        Log.Warning("No visible flags defined in settings! Using default flags.");

                        // This is our default flags list
                        visibleFlags = new List<Flag>
                {
                    Flag.Docked,
                    Flag.LandingGearDown,
                    Flag.ShieldsUp,
                    Flag.Supercruise,
                    Flag.FlightAssistOff,
                    Flag.HardpointsDeployed,
                    Flag.CargoScoopDeployed,
                    Flag.SilentRunning,
                    SyntheticFlags.HudInCombatMode,
                    SyntheticFlags.Docking
                };

                        // Save to settings
                        settings.DisplayOptions.VisibleFlags = visibleFlags;
                        SettingsManager.Save(settings);
                    }

                    // Clear items
                    Items.Clear();

                    // Debug - print all the flags
                    Log.Information("Processing flags. Current flag value: 0x{FlagValue:X8}", (uint)_gameState.CurrentStatus.Flags);

                    // Actually add flags to Items collection
                    foreach (var flag in visibleFlags)
                    {
                        // Check if this is a synthetic flag
                        if (flag == SyntheticFlags.HudInCombatMode &&
                            !_gameState.CurrentStatus.Flags.HasFlag(Flag.HudInAnalysisMode))
                        {
                            // Analysis mode is NOT active, so combat mode is active
                            Log.Information("Adding synthetic flag: HudInCombatMode");
                            AddFlagToItems(flag);
                        }
                        else if (flag == SyntheticFlags.Docking &&
                            !_gameState.CurrentStatus.Flags.HasFlag(Flag.Docked) &&
                            _gameState.IsDocking)
                        {
                            // Not docked but docking is in progress
                            Log.Information("Adding synthetic flag: Docking");
                            AddFlagToItems(flag);
                        }
                        // For standard flags, check if the flag is set
                        else if (flag != SyntheticFlags.HudInCombatMode &&
                                flag != SyntheticFlags.Docking &&
                                _gameState.CurrentStatus.Flags.HasFlag(flag))
                        {
                            // Flag is active in current status
                            Log.Information("Adding standard flag: {Flag}", flag);
                            AddFlagToItems(flag);
                        }
                    }

                    // Update visibility based on items count
                    bool hasFlags = Items.Count > 0;
                    Log.Information("Setting FlagsCard visibility: {Visible} (Items count: {Count})",
                        hasFlags, Items.Count);
                    IsVisible = hasFlags;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error updating flags");
                }
            });
        }
        private HashSet<Flag> GetActiveFlags()
        {
            var status = _gameState.CurrentStatus;
            if (status == null)
                return new HashSet<Flag>();

            // Add direct logging of all flags for debugging
            Log.Information("🚩 Raw flag status: 0x{RawFlags:X8}", (uint)status.Flags);

            // Get all active standard flags
            var activeFlags = Enum.GetValues(typeof(Flag))
                .Cast<Flag>()
                .Where(flag => status.Flags.HasFlag(flag) && flag != Flag.None)
                .ToHashSet();

            Log.Information("🚩 Active standard flags: {Flags}",
                string.Join(", ", activeFlags.Select(f => f.ToString())));

            // Add synthetic flags if active
            if (!status.Flags.HasFlag(Flag.HudInAnalysisMode))
            {
                activeFlags.Add(SyntheticFlags.HudInCombatMode);
                Log.Information("🚩 Added synthetic flag: HudInCombatMode");
            }

            if (!status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
            {
                activeFlags.Add(SyntheticFlags.Docking);
                Log.Information("🚩 Added synthetic flag: Docking");
            }

            Log.Information("🚩 Total active flags: {Count}", activeFlags.Count);
            return activeFlags;
        }

        private bool AreActiveFlagsEqual(HashSet<Flag> set1, HashSet<Flag> set2)
        {
            if (set1.Count != set2.Count)
                return false;

            // Check each flag explicitly for more reliable comparison
            foreach (var flag in set1)
            {
                if (!set2.Contains(flag))
                    return false;
            }

            foreach (var flag in set2)
            {
                if (!set1.Contains(flag))
                    return false;
            }

            return true;
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
                var flagItem = new FlagItemViewModel(flag, meta.Tooltip, meta.Icon, flag.ToString(), meta.Color)
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
                Log.Error(ex, "Failed to add flag {Flag} to Items: {Error}", flag, ex.Message);
            }
        }
        #endregion
    }

    public class FlagItemViewModel : ViewModelBase
    {
        #region Private Fields
        private Flag _flag;
        private string _displayText;
        private string _icon;
        private string _tooltip;
        private Brush _iconColor = Brushes.White;
        private int _fontSize = 14;
        #endregion

        #region Public Properties
        public Flag Flag
        {
            get => _flag;
            set => SetProperty(ref _flag, value);
        }

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

                // Optional: log fallback or use a default
                return PackIconKind.Help; // or whatever default you want
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }
        #endregion

        #region Constructor
        public FlagItemViewModel(Flag flag, string displayText, string icon, string tooltip, Brush iconColor = null)
        {
            _flag = flag;
            _displayText = displayText;
            _icon = icon;
            _tooltip = tooltip;
            _iconColor = iconColor ?? Brushes.White;
        }
        #endregion
    }
}