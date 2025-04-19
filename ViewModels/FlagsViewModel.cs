// FlagsViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class FlagsViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private readonly AppSettings _appSettings;

        private int _fontSize = 14;
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

        public FlagsViewModel(GameStateService gameState) : base("Status Flags")
        {
            _gameState = gameState;
            _appSettings = SettingsManager.Load();

            // Subscribe to game state updates
            _gameState.DataUpdated += UpdateFlags;

            // Initial update
            UpdateFlags();
        }

        private void UpdateFlags()
        {
            RunOnUIThread(() =>
            {
                Items.Clear();

                var status = _gameState.CurrentStatus;
                if (status == null)
                    return;

                IsVisible = true;

                // Get all active flags
                var activeFlags = System.Enum.GetValues(typeof(Flag))
                    .Cast<Flag>()
                    .Where(flag => status.Flags.HasFlag(flag) && flag != Flag.None)
                    .ToHashSet();
                Log.Debug("HudInAnalysisMode? {Value}", status.Flags.HasFlag(Flag.HudInAnalysisMode));

                // Add synthetic flags if active
                foreach (var synthetic in SyntheticFlags.All)
                {
                    if (synthetic == SyntheticFlags.HudInCombatMode && !status.Flags.HasFlag(Flag.HudInAnalysisMode))
                    {
                        activeFlags.Add(synthetic);
                    }

                    if (synthetic == SyntheticFlags.Docking && status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
                    {
                        activeFlags.Add(synthetic);
                    }
                }


                Log.Debug("Active flags after synthetic injection: {Flags}",
                    string.Join(", ", activeFlags.Select(f => f.ToString())));

                // Get the user's ordered visible flags
                // Reload latest settings in case they changed
                var latestSettings = SettingsManager.Load();
                var visibleFlags = latestSettings.DisplayOptions.VisibleFlags;

                Log.Debug("Loaded VisibleFlags from settings: {Flags}", string.Join(", ", visibleFlags));
                Log.Debug("ActiveFlags this frame: {Flags}", string.Join(", ", activeFlags));

                // If no visible flags are defined, use default order of active flags
                if (visibleFlags == null || visibleFlags.Count == 0)
                {
                    foreach (var flag in visibleFlags)
                    {
                        if (flag == Flag.HudInCombatMode && !status.Flags.HasFlag(Flag.HudInAnalysisMode))
                        {
                            Log.Debug("Adding synthetic flag: HudInCombatMode");
                            AddFlagToItems(flag);
                        }
                        else if (flag == Flag.Docking && status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
                        {
                            Log.Debug("Adding synthetic flag: Docking");
                            AddFlagToItems(flag);
                        }
                        else if (activeFlags.Contains(flag))
                        {
                            Log.Debug("Adding standard flag: \"{Flag}\"", flag);
                            AddFlagToItems(flag);
                        }
                        else
                        {
                            Log.Debug("Skipping flag \"{Flag}\" ... not active", flag);
                        }
                    }

                }
                else
                {
                    // Only display flags that are both:
                    // 1. Selected by the user in the options (in visibleFlags)
                    // 2. Actually active in the current game state (in activeFlags)

                    // Respect the exact order set by the user in the options dialog
                    foreach (var flag in visibleFlags)
                    {
                        // ✅ Handle synthetic HudInCombatMode
                        if (flag == Flag.HudInCombatMode && !status.Flags.HasFlag(Flag.HudInAnalysisMode))
                        {
                            AddFlagToItems(flag);
                            Log.Debug("Adding synthetic flag: HudInCombatMode");
                        }
                        // ✅ Handle synthetic Docking
                        else if (flag == Flag.Docking && status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
                        {
                            AddFlagToItems(flag);
                            Log.Debug("Adding synthetic flag: Docking");
                        }
                        // ✅ Handle all real flags
                        else if (activeFlags.Contains(flag))
                        {
                            AddFlagToItems(flag);
                            Log.Debug("Adding standard flag: {Flag}", flag);
                        }
                        else
                        {
                            Log.Debug("Skipping flag {Flag} — not active", flag);
                        }
                    }

                }

                // Hide the card if no flags are being displayed
                IsVisible = Items.Count > 0;

                Log.Debug("Updated Flags Display: {Count} flags shown", Items.Count);
            });
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




    }

    public class FlagItemViewModel : ViewModelBase
    {
        private Flag _flag;
        private string _displayText;
        private string _icon;
        private string _tooltip;
        private Brush _iconColor = Brushes.White;
        private int _fontSize = 14;

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

        public FlagItemViewModel(Flag flag, string displayText, string icon, string tooltip, Brush iconColor = null)
        {
            _flag = flag;
            _displayText = displayText;
            _icon = icon;
            _tooltip = tooltip;
            _iconColor = iconColor ?? Brushes.White;
        }
    }

}