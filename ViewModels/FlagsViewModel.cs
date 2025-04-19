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
            // Only update when status changes
            if (e.PropertyName == nameof(GameStateService.CurrentStatus) ||
                e.PropertyName == nameof(GameStateService.IsDocking))
            {
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
                // Get all active flags
                var activeFlags = GetActiveFlags();

                // Check if active flags have changed
                if (AreActiveFlagsEqual(activeFlags, _lastActiveFlags))
                {
                    Log.Debug("Active flags unchanged, skipping update");
                    return;
                }

                _lastActiveFlags = new HashSet<Flag>(activeFlags);

                // Get the user's ordered visible flags
                // Reload latest settings in case they changed
                var latestSettings = SettingsManager.Load();
                var visibleFlags = latestSettings.DisplayOptions.VisibleFlags;

                // Check if visible flags have changed
                bool visibleFlagsChanged = !AreFlagListsEqual(visibleFlags, _lastVisibleFlags);
                if (visibleFlagsChanged)
                {
                    _lastVisibleFlags = visibleFlags.ToList();
                }

                // If no changes to what should be displayed, exit
                if (!visibleFlagsChanged && Items.Count > 0)
                {
                    Log.Debug("No changes to displayed flags, skipping UI update");
                    return;
                }

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
                    if (flag == Flag.HudInCombatMode &&
                        !_gameState.CurrentStatus.Flags.HasFlag(Flag.HudInAnalysisMode))
                    {
                        Log.Debug("Adding synthetic flag: HudInCombatMode");
                        AddFlagToItems(flag);
                    }
                    else if (flag == Flag.Docking &&
                             _gameState.CurrentStatus.Flags.HasFlag(Flag.Docked) &&
                             _gameState.IsDocking)
                    {
                        Log.Debug("Adding synthetic flag: Docking");
                        AddFlagToItems(flag);
                    }
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

            return set1.SetEquals(set2);
        }

        private bool AreFlagListsEqual(List<Flag> list1, List<Flag> list2)
        {
            if (list1 == null && list2 == null)
                return true;
            if (list1 == null || list2 == null)
                return false;
            if (list1.Count != list2.Count)
                return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i] != list2[i])
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