// BackpackViewModel.cs
using System.Collections.ObjectModel;
using System.Linq;
using EliteInfoPanel.Core;

namespace EliteInfoPanel.ViewModels
{
    public class BackpackViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;

        public ObservableCollection<BackpackItemViewModel> Items { get; } = new();

        public BackpackViewModel(GameStateService gameState) : base("Backpack")
        {
            _gameState = gameState;

            // Subscribe to game state updates
            _gameState.DataUpdated += UpdateBackpack;

            // Initial update
            UpdateBackpack();
        }

        private void UpdateBackpack()
        {
            Items.Clear();

            if (_gameState.CurrentBackpack?.Inventory == null)
                return;

            // Only show when on foot
            IsVisible = _gameState.CurrentStatus?.OnFoot == true;

            var grouped = _gameState.CurrentBackpack.Inventory
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                bool isFirst = true;
                foreach (var item in group.OrderByDescending(i => i.Count))
                {
                    Items.Add(new BackpackItemViewModel(
                        item.Name_Localised ?? item.Name,
                        item.Count,
                        group.Key,
                        isFirst));

                    isFirst = false;
                }
            }
        }
    }

    public class BackpackItemViewModel : ViewModelBase
    {
        private string _name;
        private int _count;
        private string _category;
        private bool _isFirstInCategory;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public bool IsFirstInCategory
        {
            get => _isFirstInCategory;
            set => SetProperty(ref _isFirstInCategory, value);
        }

        public BackpackItemViewModel(string name, int count, string category, bool isFirstInCategory = false)
        {
            _name = name;
            _count = count;
            _category = category;
            _isFirstInCategory = isFirstInCategory;
        }
    }
}