// FlagsViewModel.cs
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;

namespace EliteInfoPanel.ViewModels
{
    public class FlagsViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private readonly AppSettings _appSettings;

      
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
                // Add items here  



                var status = _gameState.CurrentStatus;
                if (status == null)
                    return;

                IsVisible = true;

                // Get all active flags
                var flags = System.Enum.GetValues(typeof(Flag))
                    .Cast<Flag>()
                    .Where(flag => status.Flags.HasFlag(flag) && flag != Flag.None)
                    .ToList();

                // Add synthetic flags
                if (!status.Flags.HasFlag(Flag.HudInAnalysisMode))
                    flags.Add(SyntheticFlags.HudInCombatMode);

                if (status.Flags.HasFlag(Flag.Docked) && _gameState.IsDocking)
                    flags.Add(SyntheticFlags.Docking);

                // Only include flags the user wants to see
                var visibleFlags = _appSettings.DisplayOptions.VisibleFlags;
                if (visibleFlags != null && visibleFlags.Count > 0)
                {
                    flags = flags.Where(f => visibleFlags.Contains(f)).ToList();
                }

                // Add flags to the collection
                foreach (var flag in flags)
                {
                    string displayText = flag switch
                    {
                        var f when f == SyntheticFlags.HudInCombatMode => "HUD Combat Mode",
                        var f when f == SyntheticFlags.Docking => "Docking",
                        _ => flag.ToString().Replace("_", " ")
                    };

                    Items.Add(new FlagItemViewModel(flag, displayText));
                }
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