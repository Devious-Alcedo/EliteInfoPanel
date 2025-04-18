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
        private readonly Grid _mainGrid;
        private readonly AppSettings _appSettings;
        private readonly MainViewModel _viewModel;

        public CardLayoutManager(Grid mainGrid, AppSettings appSettings, MainViewModel viewModel)
        {
            _mainGrid = mainGrid ?? throw new ArgumentNullException(nameof(mainGrid));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void UpdateLayout()
        {
            try
            {
                ClearGrid();

                var visibleCards = GetVisibleCards();

                // Use the same layout approach regardless of window mode
                ApplyHorizontalLayout(visibleCards);

                Log.Debug("Card layout updated with {Count} visible cards", visibleCards.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating card layout");
            }
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
                cardElement.Content = new Controls.SummaryCard { DataContext = viewModel };
            else if (viewModel is CargoViewModel)
                cardElement.Content = new Controls.CargoCard { DataContext = viewModel };
            else if (viewModel is BackpackViewModel)
                cardElement.Content = new Controls.BackpackCard { DataContext = viewModel };
            else if (viewModel is RouteViewModel)
                cardElement.Content = new Controls.RouteCard { DataContext = viewModel };
            else if (viewModel is ModulesViewModel)
                cardElement.Content = new Controls.ModulesCard { DataContext = viewModel };
            else if (viewModel is FlagsViewModel)
                cardElement.Content = new Controls.FlagsCard { DataContext = viewModel };

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
    }
}