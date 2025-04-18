// FlagsViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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

                // Add synthetic flags if active
                if (!status.Flags.HasFlag(Flag.HudInAnalysisMode))
                    activeFlags.Add(SyntheticFlags.HudInCombatMode);

                if (status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
                    activeFlags.Add(SyntheticFlags.Docking);

                // Get the user's ordered visible flags
                var visibleFlags = _appSettings.DisplayOptions.VisibleFlags;

                // If no visible flags are defined, use default order of active flags
                if (visibleFlags == null || visibleFlags.Count == 0)
                {
                    foreach (var flag in activeFlags)
                    {
                        AddFlagToItems(flag);
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
                        // Only add the flag if it's currently active
                        if (activeFlags.Contains(flag))
                        {
                            AddFlagToItems(flag);
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
            string displayText = flag switch
            {
                var f when f == SyntheticFlags.HudInCombatMode => "HUD Combat Mode",
                var f when f == SyntheticFlags.Docking => "Docking",
                var f when f == Flag.FsdMassLocked => "Mass Locked",
                var f when f == Flag.LandingGearDown => "Landing Gear Down",
                _ => flag.ToString().Replace("_", " ")
            };

            Items.Add(new FlagItemViewModel(flag, displayText)
            {
                FontSize = (int)this.FontSize
            });
        }
    }

    public class FlagItemViewModel : ViewModelBase
    {
        private Flag _flag;
        private string _displayText;

        public Flag Flag
        {
            get => _flag;
            set => SetProperty(ref _flag, value);
        }
        private int _fontSize = 14;
        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        public FlagItemViewModel(Flag flag, string displayText)
        {
            _flag = flag;
            _displayText = displayText;
        }
    }
}