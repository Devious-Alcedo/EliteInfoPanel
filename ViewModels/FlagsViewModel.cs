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

            // Initial update
            UpdateFlags();
        }
        #endregion

        #region Private Methods
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

            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(UpdateFlags);
                return;
            }

            try
            {
                // Force update if raw status flags have changed
                uint currentStatusFlags = (uint)_gameState.CurrentStatus.Flags;
                bool flagsValueChanged = currentStatusFlags != _lastStatusFlags;
                if (flagsValueChanged)
                {
                    _lastStatusFlags = currentStatusFlags;
                    Log.Debug("Status flags raw value changed: {OldValue} -> {NewValue}",
                        _lastStatusFlags, currentStatusFlags);
                }

                // Force update if hyperspace state changed
                bool hyperspaceChanged = _gameState.IsHyperspaceJumping != _lastHyperspaceState;
                if (hyperspaceChanged)
                {
                    _lastHyperspaceState = _gameState.IsHyperspaceJumping;
                    Log.Debug("Hyperspace state changed: {IsJumping}", _lastHyperspaceState);
                }

                // Force update if docked state changed
                bool dockedChanged = _gameState.CurrentStatus.Flags.HasFlag(Flag.Docked) != _lastDockedState;
                if (dockedChanged)
                {
                    _lastDockedState = _gameState.CurrentStatus.Flags.HasFlag(Flag.Docked);
                    Log.Debug("Docked state changed: {IsDocked}", _lastDockedState);
                }

                // Get all active flags
                var activeFlags = GetActiveFlags();

                // Get the user's ordered visible flags from current settings
                var settings = SettingsManager.Load();
                var visibleFlags = settings.DisplayOptions.VisibleFlags;

                // Always reload visible flags to respect order from options window
                _lastVisibleFlags = visibleFlags != null ? visibleFlags.ToList() : new List<Flag>();

                // Skip update if nothing changed
                if (!flagsValueChanged && !hyperspaceChanged && !dockedChanged &&
                    AreActiveFlagsEqual(activeFlags, _lastActiveFlags) &&
                    Items.Count > 0)
                {
                    Log.Debug("No changes in flags, skipping update");
                    return;
                }

                // Update our last active flags
                _lastActiveFlags = new HashSet<Flag>(activeFlags);

                // Clear and rebuild the Items collection
                Items.Clear();

                // Check if we have any visible flags defined
                if (visibleFlags == null || visibleFlags.Count == 0)
                {
                    Log.Debug("No visible flags defined in settings");
                    return;
                }

                // Add flags in the user-defined order, but only if active
                foreach (var flag in visibleFlags)
                {
                    // Handle special synthetic flags
                    if (flag == Flag.HudInCombatMode && !_gameState.CurrentStatus.Flags.HasFlag(Flag.HudInAnalysisMode))
                    {
                        Log.Debug("Adding synthetic flag: HudInCombatMode");
                        AddFlagToItems(flag);
                    }
                    else if (flag == Flag.Docking &&
           !_gameState.CurrentStatus.Flags.HasFlag(Flag.Docked) &&
           _gameState.IsDocking)
                    {
                        Log.Debug("Adding synthetic flag: Docking");
                        AddFlagToItems(flag);
                    }

                    // Only add the flag if it's active in the game
                    else if (activeFlags.Contains(flag))
                    {
                        Log.Debug("Adding standard flag: {Flag}", flag);
                        AddFlagToItems(flag);
                    }
                }

                // Hide the card if no flags are being displayed
                IsVisible = Items.Count > 0;

                Log.Debug("Updated Flags Display: {Count} flags shown", Items.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating flags");
            }
        }

        private HashSet<Flag> GetActiveFlags()
        {
            var status = _gameState.CurrentStatus;
            if (status == null)
                return new HashSet<Flag>();

            // Get all active standard flags
            var activeFlags = Enum.GetValues(typeof(Flag))
                .Cast<Flag>()
                .Where(flag => status.Flags.HasFlag(flag) && flag != Flag.None)
                .ToHashSet();

            // Add synthetic flags if active
            if (!status.Flags.HasFlag(Flag.HudInAnalysisMode))
            {
                activeFlags.Add(SyntheticFlags.HudInCombatMode);
            }

            if (status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
            {
                activeFlags.Add(SyntheticFlags.Docking);
            }

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
            if (!FlagVisualHelper.TryGetMetadata(flag, out var meta))
            {
                meta = ("Flag", flag.ToString().Replace("_", " "), Brushes.Gray);
            }

            Items.Add(new FlagItemViewModel(flag, meta.Tooltip, meta.Icon, flag.ToString(), meta.Color)
            {
                FontSize = (int)this.FontSize
            });
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