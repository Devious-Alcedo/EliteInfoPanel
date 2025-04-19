// BackpackViewModel.cs
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EliteInfoPanel.Core;

namespace EliteInfoPanel.ViewModels
{
    public class BackpackViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
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


        public ObservableCollection<BackpackItemViewModel> Items { get; } = new();

        public BackpackViewModel(GameStateService gameState) : base("Backpack")
        {
            _gameState = gameState;

            // Subscribe to game state updates
        //    _gameState.DataUpdated += UpdateBackpack;

            // Initial update
            UpdateBackpack();
        }

        private void UpdateBackpack()
        {

            RunOnUIThread(() => {
                Items.Clear();
                // Add items here  
            


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
                        isFirst)
                    {
                        FontSize = (int)this.FontSize
                    });

                    isFirst = false;
                }
            }
            });
        }
    }

    public class BackpackItemViewModel : ViewModelBase
    {
        private string _name;
        private int _count;
        private string _category;
        private bool _isFirstInCategory;
        private int _fontSize = 14;
        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

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