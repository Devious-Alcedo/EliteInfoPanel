using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.Util
{
    public class CardLayoutManager
    {
        #region Private Fields
        private readonly Grid _mainGrid;
        private readonly AppSettings _appSettings;
        private readonly MainViewModel _viewModel;
        private Dictionary<Type, UIElement> _cardCache = new Dictionary<Type, UIElement>();
        private List<CardViewModel> _lastVisibleCards = new List<CardViewModel>();
        private bool _initialLayoutComplete = false;
        #endregion

        #region Constructor
        public CardLayoutManager(Grid mainGrid, AppSettings appSettings, MainViewModel viewModel)
        {
            _mainGrid = mainGrid ?? throw new ArgumentNullException(nameof(mainGrid));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
        #endregion

        #region Public Methods
        public void UpdateLayout(bool forceRebuild = false)
        {
            try
            {
                var visibleCards = GetVisibleCards();

                // Check if visible cards have changed
                bool cardsChanged = !_initialLayoutComplete || forceRebuild ||
                                   !AreCardListsEqual(_lastVisibleCards, visibleCards);

                if (cardsChanged)
                {
                    Log.Debug("Rebuilding card layout: initial={0}, force={1}, changed={2}",
                              !_initialLayoutComplete, forceRebuild,
                              _initialLayoutComplete && !forceRebuild && !AreCardListsEqual(_lastVisibleCards, visibleCards));

                    // Clear and rebuild the entire grid
                    ClearGrid();
                    ApplyHorizontalLayout(visibleCards);
                    _lastVisibleCards = visibleCards.ToList();
                    _initialLayoutComplete = true;

                    Log.Debug("Card layout rebuilt with {Count} visible cards", visibleCards.Count);
                }
                else
                {
                    // No layout change needed - the data binding will handle content updates
                    Log.Debug("Card layout unchanged - skipping rebuild");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating card layout");
            }
        }
        #endregion

        #region Private Methods
        private bool AreCardListsEqual(List<CardViewModel> list1, List<CardViewModel> list2)
        {
            if (list1.Count != list2.Count)
            {
                Log.Debug("Card lists differ in count: {Count1} vs {Count2}", list1.Count, list2.Count);
                return false;
            }

            for (int i = 0; i < list1.Count; i++)
            {
                // Compare by type and visibility
                if (list1[i].GetType() != list2[i].GetType())
                {
                    Log.Debug("Card lists differ in type at index {Index}: {Type1} vs {Type2}",
                              i, list1[i].GetType().Name, list2[i].GetType().Name);
                    return false;
                }
            }

            return true;
        }

        private void ClearGrid()
        {
            _mainGrid.ColumnDefinitions.Clear();
            _mainGrid.RowDefinitions.Clear();

            // Remove all cards from the grid (but keep other elements)
            for (int i = _mainGrid.Children.Count - 1; i >= 0; i--)
            {
                if (_mainGrid.Children[i] is Card)
                {
                    _mainGrid.Children.RemoveAt(i);
                }
            }
        }

        private List<CardViewModel> GetVisibleCards()
        {
            // Always add summary first if visible
            var result = new List<CardViewModel>();

            if (_viewModel.SummaryCard.IsVisible)
                result.Add(_viewModel.SummaryCard);

            // Add cargo/backpack next (only one should be visible)
            if (_viewModel.CargoCard.IsVisible)
                result.Add(_viewModel.CargoCard);
            else if (_viewModel.BackpackCard.IsVisible)
                result.Add(_viewModel.BackpackCard);

            // Add route
            if (_viewModel.RouteCard.IsVisible)
                result.Add(_viewModel.RouteCard);

            // Add modules (will take extra space)
            if (_viewModel.ModulesCard.IsVisible)
                result.Add(_viewModel.ModulesCard);

            // Add flags last
            if (_viewModel.FlagsCard.IsVisible)
                result.Add(_viewModel.FlagsCard);

            return result;
        }

        private UIElement CreateCardElement(CardViewModel viewModel)
        {
            // Check if we already have this card type cached
            var vmType = viewModel.GetType();

            if (_cardCache.TryGetValue(vmType, out UIElement cachedElement))
            {
                // Update DataContext before returning cached element
                if (cachedElement is FrameworkElement element)
                {
                    element.DataContext = viewModel;

                    // Also update the content DataContext if it's separate
                    if (element is Card card && card.Content is FrameworkElement content)
                    {
                        content.DataContext = viewModel;
                    }
                }

                Log.Debug("Using cached card element for {CardType}", vmType.Name);
                return cachedElement;
            }

            // Create the materialDesign Card
            var cardElement = new Card
            {
                Margin = new Thickness(5),
                Padding = new Thickness(5),
                DataContext = viewModel,
                // Set a smaller font size specifically for floating mode
                FontSize = _appSettings.UseFloatingWindow ? 11.0 : 14.0
            };

            // Create appropriate content based on card type
            if (viewModel is SummaryViewModel)
                cardElement.Content = new EliteInfoPanel.Controls.SummaryCard { DataContext = viewModel };
            else if (viewModel is CargoViewModel)
                cardElement.Content = new EliteInfoPanel.Controls.CargoCard { DataContext = viewModel };
            else if (viewModel is BackpackViewModel)
                cardElement.Content = new EliteInfoPanel.Controls.BackpackCard { DataContext = viewModel };
            else if (viewModel is RouteViewModel)
                cardElement.Content = new EliteInfoPanel.Controls.RouteCard { DataContext = viewModel };
            else if (viewModel is ModulesViewModel)
                cardElement.Content = new EliteInfoPanel.Controls.ModulesCard { DataContext = viewModel };
            else if (viewModel is FlagsViewModel)
                cardElement.Content = new EliteInfoPanel.Controls.FlagsCard { DataContext = viewModel };

            // Cache the created element for future use
            _cardCache[vmType] = cardElement;
            Log.Debug("Created and cached new card element for {CardType}", vmType.Name);

            return cardElement;
        }

        // Use a single horizontal layout for both modes
        private void ApplyHorizontalLayout(List<CardViewModel> visibleCards)
        {
            if (visibleCards.Count == 0) return;

            // Add a single row
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Add column definitions for each card
            for (int i = 0; i < visibleCards.Count; i++)
            {
                // Make modules card take extra space
                if (visibleCards[i] is ModulesViewModel)
                    _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                else
                    _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Add cards to the grid
            for (int i = 0; i < visibleCards.Count; i++)
            {
                var cardElement = CreateCardElement(visibleCards[i]);
                Grid.SetColumn(cardElement, i);
                Grid.SetRow(cardElement, 0);
                _mainGrid.Children.Add(cardElement);
            }
        }
        #endregion
    }
}