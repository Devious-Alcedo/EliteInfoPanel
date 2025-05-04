// BackpackViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class BackpackViewModel : CardViewModel
    {
        #region Private Fields
        private readonly GameStateService _gameState;
        #endregion

        #region Public Properties
        public ObservableCollection<BackpackItemViewModel> Items { get; } = new();

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
        #endregion

        #region Constructor
        public BackpackViewModel(GameStateService gameState) : base("Backpack")
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

            // Subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Initial update
            UpdateBackpack();
        }
        #endregion

        #region Event Handlers
        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GameStateService.CurrentBackpack):
                    UpdateBackpack();
                    break;
                case nameof(GameStateService.CurrentStatus):
                    // Update visibility based on player state
                    UpdateVisibility();
                    break;
                case nameof(GameStateService.IsHyperspaceJumping):
                    UpdateVisibility();
                    break;
            }
        }
        #endregion

        #region Private Methods
        private void UpdateBackpack()
        {
            try
            {
                RunOnUIThread(() =>
                {
                    Items.Clear();

                    // Update visibility first (will set IsVisible appropriately)
                    UpdateVisibility();

                    // If we shouldn't be visible, don't bother populating
                    if (!IsVisible)
                    {
                        Log.Debug("BackpackViewModel: Card is hidden, skipping content update");
                        return;
                    }

                    if (_gameState.CurrentBackpack?.Inventory == null)
                    {
                        Log.Debug("BackpackViewModel: No backpack inventory available");
                        return;
                    }

                    var grouped = _gameState.CurrentBackpack.Inventory
                        .GroupBy(i => i.Category)
                        .OrderBy(g => g.Key);

                    foreach (var group in grouped)
                    {
                        bool isFirst = true;
                        Log.Debug("BackpackViewModel: Processing category {Category} with {Count} items",
                            group.Key, group.Count());

                        foreach (var item in group.OrderByDescending(i => i.Count))
                        {
                            string displayName = item.Name_Localised ?? item.Name;
                            Items.Add(new BackpackItemViewModel(
                                displayName,
                                item.Count,
                                group.Key,
                                isFirst)
                            {
                                FontSize = (int)this.FontSize
                            });

                            isFirst = false;
                        }
                    }

                    Log.Debug("BackpackViewModel: Added {Count} items to display", Items.Count);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating backpack");
            }
        }

        private void UpdateVisibility()
        {
            try
            {
                var status = _gameState.CurrentStatus;
                if (status == null)
                {
                    if (IsVisible)
                    {
                        Log.Debug("BackpackViewModel: No status available, hiding backpack");
                        IsVisible = false;
                    }
                    return;
                }

                bool isOnFoot = status.OnFoot;

                bool hasItems = false;
                if (_gameState.CurrentBackpack != null)
                {
                    int totalItems =
                        (_gameState.CurrentBackpack.Items?.Count ?? 0) +
                        (_gameState.CurrentBackpack.Components?.Count ?? 0) +
                        (_gameState.CurrentBackpack.Consumables?.Count ?? 0) +
                        (_gameState.CurrentBackpack.Data?.Count ?? 0);

                    hasItems = totalItems > 0;
                }

                bool isJumping = _gameState.IsHyperspaceJumping;
                bool userWantsBackpack = SettingsManager.Load().ShowBackpack;

                bool shouldShow = userWantsBackpack && isOnFoot && hasItems && !isJumping;

                Log.Debug("BackpackViewModel: Visibility check - OnFoot:{OnFoot}, HasItems:{HasItems}, " +
                         "Jumping:{Jumping}, UserEnabled:{UserEnabled}, ShouldShow:{ShouldShow}",
                         isOnFoot, hasItems, isJumping, userWantsBackpack, shouldShow);

                if (IsVisible != shouldShow)
                {
                    Log.Debug("BackpackViewModel: Visibility changed to {Visibility}", shouldShow);
                    IsVisible = shouldShow;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating backpack visibility");
            }
        }
        #endregion
    }

    public class BackpackItemViewModel : ViewModelBase
    {
        #region Private Fields
        private string _name;
        private int _count;
        private string _category;
        private bool _isFirstInCategory;
        private int _fontSize = 14;
        #endregion

        #region Public Properties
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

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }
        #endregion

        #region Constructor
        public BackpackItemViewModel(string name, int count, string category, bool isFirstInCategory = false)
        {
            _name = name;
            _count = count;
            _category = category;
            _isFirstInCategory = isFirstInCategory;
        }
        #endregion
    }
}