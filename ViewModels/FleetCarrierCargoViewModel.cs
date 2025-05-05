using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using Serilog;


namespace EliteInfoPanel.ViewModels
{
    public class FleetCarrierCargoViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        public ObservableCollection<CarrierCargoItem> Cargo { get; } = new();

        public FleetCarrierCargoViewModel(GameStateService gameState)
             : base("Fleet Carrier Cargo")
        {
            _gameState = gameState;
            _gameState.PropertyChanged += GameState_PropertyChanged;
            Cargo.Add(new CarrierCargoItem { Name = "Palladium", Quantity = 52 });
            Cargo.Add(new CarrierCargoItem { Name = "Tritium", Quantity = 750 });

            UpdateCargo();
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CarrierCargo))
            {
                Log.Information("FleetCarrierCargoViewModel received CarrierCargo update");
                UpdateCargo();
            }
        }
        public void Initialize()
        {
            UpdateCargo();
        }


        private void UpdateCargo()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var cargoItems = _gameState.CurrentCarrierCargo;
                if (cargoItems == null || !cargoItems.Any())
                {
                    Log.Warning("UpdateCargo: No carrier cargo items available");
                    return;
                }

                Cargo.Clear();
                foreach (var item in cargoItems)
                {
                    Cargo.Add(item);
                    Log.Debug("  {Name} = {Quantity}", item.Name, item.Quantity);
                }

                // Always ensure the card is visible if we have cargo
                SetContextVisibility(true);

                Log.Information("Fleet carrier cargo updated with {Count} items", Cargo.Count);
            });
        }

    }
}
