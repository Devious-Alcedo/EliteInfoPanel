// ModulesViewModel.cs
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;

namespace EliteInfoPanel.ViewModels
{
    public class ModulesViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private int _currentPage = 0;

        public ObservableCollection<ModuleItemViewModel> Items { get; } = new();

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    UpdateModules();
                }
            }
        }

        public ModulesViewModel(GameStateService gameState) : base("Ship Modules")
        {
            _gameState = gameState;
            // Remove ColumnSpan = 2 assignment

            // Subscribe to game state events
            _gameState.DataUpdated += UpdateModules;

            // Initial update
            UpdateModules();

            // Set up page rotation
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) => NextPage();
            timer.Start();
        }

        public void NextPage()
        {
            CurrentPage = (CurrentPage + 1) % 2;
        }

        private void UpdateModules()
        {
            RunOnUIThread(() =>
            {
                Items.Clear();

                if (_gameState.CurrentLoadout?.Modules == null)
                    return;

                // Determine visibility
                var status = _gameState.CurrentStatus;
                IsVisible = status != null &&
                            status.Flags.HasFlag(Flag.InMainShip) &&
                            !status.OnFoot &&
                            !status.Flags.HasFlag(Flag.InSRV) &&
                            !status.Flags.HasFlag(Flag.InFighter);

                if (!IsVisible)
                    return;

                // Filter out cosmetic modules
                var modules = _gameState.CurrentLoadout.Modules
                    .Where(m =>
                        !string.IsNullOrWhiteSpace(m.Item) &&
                        !m.Item.StartsWith("Decal_", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.StartsWith("Nameplate_", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.StartsWith("PaintJob_", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.StartsWith("VoicePack_", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.Contains("spoiler", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.Contains("bumper", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.Contains("bobble", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.Contains("weaponcustomisation", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.Contains("enginecustomisation", System.StringComparison.OrdinalIgnoreCase) &&
                        !m.Item.Contains("wings", System.StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();

                // Categorize modules
                var hardpoints = modules.Where(m => m.Slot.StartsWith("SmallHardpoint") ||
                                m.Slot.StartsWith("MediumHardpoint") ||
                                m.Slot.StartsWith("LargeHardpoint") ||
                                m.Slot.StartsWith("TinyHardpoint")).ToList();

                var coreInternals = modules.Where(m => m.Slot is "PowerPlant" or "MainEngines" or
                                    "FrameShiftDrive" or "LifeSupport" or "PowerDistributor" or
                                    "Radar" or "FuelTank").ToList();

                var optionals = modules.Where(m => m.Slot.StartsWith("Slot")).ToList();
                var other = modules.Except(hardpoints).Except(coreInternals).Except(optionals).ToList();

                var groupedPages = new System.Collections.Generic.List<System.Collections.Generic.List<LoadoutModule>>
            {
                hardpoints.Concat(coreInternals).ToList(), // Page 1
                optionals.Concat(other).ToList()           // Page 2
            };

                var pageModules = groupedPages[CurrentPage];

                foreach (var module in pageModules)
                {
                    string rawName = module.ItemLocalised ?? module.Item;
                    string displayName = ModuleNameMapper.GetFriendlyName(rawName);

                    Items.Add(new ModuleItemViewModel(
                        displayName,
                        module.Health,
                        module.Slot,
                        module.On
                    ));
                }
            });
        }
    }

    public class ModuleItemViewModel : ViewModelBase
    {
        private string _name;
        private float _health;
        private string _slot;
        private bool _isOn;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public float Health
        {
            get => _health;
            set => SetProperty(ref _health, value);
        }

        public string Slot
        {
            get => _slot;
            set => SetProperty(ref _slot, value);
        }

        public bool IsOn
        {
            get => _isOn;
            set => SetProperty(ref _isOn, value);
        }

        public Brush HealthColor => Health < 0.7f ? Brushes.Red :
                                  Health < 0.95f ? Brushes.Orange :
                                  Brushes.White;

        public ModuleItemViewModel(string name, float health, string slot, bool isOn)
        {
            _name = name;
            _health = health;
            _slot = slot;
            _isOn = isOn;
        }
    }
}