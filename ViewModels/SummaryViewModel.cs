// SummaryViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Media;
using EliteInfoPanel.Core;
using MaterialDesignThemes.Wpf;

namespace EliteInfoPanel.ViewModels
{
    public class SummaryViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private bool _showFuelBar;
        private double _fuelMain;
        private double _fuelReservoir;
        private double _fuelBarRatio;

        public ObservableCollection<SummaryItemViewModel> Items { get; } = new();

        public bool ShowFuelBar
        {
            get => _showFuelBar;
            set => SetProperty(ref _showFuelBar, value);
        }

        public double FuelMain
        {
            get => _fuelMain;
            set => SetProperty(ref _fuelMain, value);
        }

        public double FuelReservoir
        {
            get => _fuelReservoir;
            set => SetProperty(ref _fuelReservoir, value);
        }

        public double FuelBarRatio
        {
            get => _fuelBarRatio;
            set => SetProperty(ref _fuelBarRatio, value);
        }

        public SummaryViewModel(GameStateService gameState) : base("Summary")
        {
            _gameState = gameState;

            // Subscribe to game state updates
            _gameState.DataUpdated += UpdateSummary;

            // Initial update
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            // Implementation similar to before
        }
    }
}