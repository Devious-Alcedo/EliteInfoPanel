using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using EliteInfoPanel.Controls;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class ColonizationItemViewModel : ViewModelBase
    {
        #region Private Fields

        private double _completionPercentage;
        private int _fontsize = 14;
        private bool _isComplete;
        private string _name;
        private int _payment;
        public GameStateService GameState { get; set; }
        private int _profitPerUnit;
        private int _provided;
        private int _remaining;
        private int _required;
        private ColonizationViewModel _parent;

        public void SetParent(ColonizationViewModel parent)
        {
            _parent = parent;
        }

        public double AvailableCargoPercentage
        {
            get
            {
                // Calculate available cargo as the minimum of what you have and what's still needed
                int availableCargo = Math.Min(ShipCargoQuantity + CarrierCargoQuantity, Remaining);

                // Return as percentage of the total requirement
                return Required > 0 ? (double)availableCargo / Required * 100.0 : 0;
            }
        }

        // Calculate total potential completion (current + available cargo)
        public double TotalPotentialPercentage
        {
            get
            {
                return Math.Min(100, CompletionPercentage + AvailableCargoPercentage);
            }
        }

        // Color for the available cargo portion
        public Brush AvailableCargoColor => new SolidColorBrush(Colors.DeepSkyBlue);

        // Ship cargo quantity - convert internal names to display names like CargoViewModel does
        public int ShipCargoQuantity
        {
            get
            {
                if (GameState?.CurrentCargo?.Inventory == null) return 0;

                // Convert each cargo item's internal name to display name and compare
                // This mirrors exactly what CargoViewModel does for the cargo card
                foreach (var cargoItem in GameState.CurrentCargo.Inventory)
                {
                    string displayNameFromCargo = CommodityMapper.GetDisplayName(cargoItem.Name);
                    if (string.Equals(displayNameFromCargo, Name, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Debug("Ship cargo match for {DisplayName} (internal: {InternalName}): {Quantity}",
                            Name, cargoItem.Name, cargoItem.Count);
                        return cargoItem.Count;
                    }
                }

                Log.Debug("Ship cargo no match found for {DisplayName}", Name);
                return 0;
            }
        }

        // Carrier cargo quantity - should already use display names
        public int CarrierCargoQuantity
        {
            get
            {
                if (GameState?.CurrentCarrierCargo == null) return 0;

                // Carrier cargo should already be using display names
                var match = GameState.CurrentCarrierCargo
                    .FirstOrDefault(i => string.Equals(i.Name, Name, StringComparison.OrdinalIgnoreCase));

                Log.Debug("Checking carrier cargo for {DisplayName}: Found match? {HasMatch}, Quantity: {Quantity}",
                    Name, match != null, match?.Quantity ?? 0);

                return match?.Quantity ?? 0;
            }
        }

        public bool HasAvailableCargo => ShipCargoQuantity > 0 || CarrierCargoQuantity > 0;

        #endregion Private Fields

        #region Public Properties

        public double CompletionPercentage
        {
            get => _completionPercentage;
            set => SetProperty(ref _completionPercentage, value);
        }

        public int FontSize
        {
            get => _fontsize;
            set => SetProperty(ref _fontsize, value);
        }

        public bool IsComplete
        {
            get => _isComplete;
            set => SetProperty(ref _isComplete, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Payment
        {
            get => _payment;
            set => SetProperty(ref _payment, value);
        }
        public int TotalAvailableCargo => ShipCargoQuantity + CarrierCargoQuantity;

        public int RemainingToAcquire => Math.Max(0, Remaining - TotalAvailableCargo);

        public bool HasSufficientCargo => TotalAvailableCargo >= Remaining;
        public string CargoStatusText
        {
            get
            {
                if (IsComplete)
                    return "✅ Complete";

                if (HasSufficientCargo)
                    return $"✅ Ready to deliver ({TotalAvailableCargo} available)";

                if (TotalAvailableCargo > 0)
                    return $"⚠️ Partial ({TotalAvailableCargo} available, need {RemainingToAcquire} more)";

                return $"❌ Need to acquire {RemainingToAcquire}";
            }

        }
        public long RemainingValue => (long)RemainingToAcquire * Payment;
        public void NotifyCargoChanged()
        {
            // Notify all cargo-related properties that they may have changed
            OnPropertyChanged(nameof(ShipCargoQuantity));
            OnPropertyChanged(nameof(CarrierCargoQuantity));
            OnPropertyChanged(nameof(TotalAvailableCargo));
            OnPropertyChanged(nameof(RemainingToAcquire));
            OnPropertyChanged(nameof(HasSufficientCargo));
            OnPropertyChanged(nameof(CargoCompletionPercentage));
            OnPropertyChanged(nameof(EnhancedProgressColor));
            OnPropertyChanged(nameof(CargoStatusText));
            OnPropertyChanged(nameof(CargoStatusColor));
            OnPropertyChanged(nameof(RemainingValue));
            OnPropertyChanged(nameof(RemainingValueText));
            OnPropertyChanged(nameof(HasAvailableCargo));
            OnPropertyChanged(nameof(AvailableCargoPercentage));
        }
       
        public string RemainingValueText => RemainingValue > 0 ? $"{RemainingValue:N0} credits" : "Complete";
        public Brush CargoStatusColor
        {
            get
            {
                if (IsComplete || HasSufficientCargo)
                    return new SolidColorBrush(Colors.LimeGreen);
                else if (TotalAvailableCargo > 0)
                    return new SolidColorBrush(Colors.Gold);
                else
                    return new SolidColorBrush(Colors.OrangeRed);
            }
        }
        public Brush EnhancedProgressColor
        {
            get
            {
                if (IsComplete)
                    return new SolidColorBrush(Colors.LimeGreen);

                if (HasSufficientCargo)
                    return new SolidColorBrush(Colors.Gold); // We have enough cargo to complete this

                if (CargoCompletionPercentage > 75)
                    return new SolidColorBrush(Colors.LightGreen);
                else if (CargoCompletionPercentage > 50)
                    return new SolidColorBrush(Colors.Yellow);
                else if (CompletionPercentage > 25)
                    return new SolidColorBrush(Colors.Orange);
                else
                    return new SolidColorBrush(Colors.OrangeRed);
            }
        }
        public double CargoCompletionPercentage
        {
            get
            {
                if (Required <= 0) return 0;

                // Calculate what percentage of the total requirement could be fulfilled with current cargo
                int totalPotential = Provided + TotalAvailableCargo;
                return Math.Min(100, (double)totalPotential / Required * 100.0);
            }
        }
        public int ProfitPerUnit
        {
            get => _profitPerUnit;
            set => SetProperty(ref _profitPerUnit, value);
        }

        public Brush ProgressColor => IsComplete ?
                                     new SolidColorBrush(Colors.LimeGreen) :
                                     CompletionPercentage > 75 ?
                                         new SolidColorBrush(Colors.GreenYellow) :
                                         CompletionPercentage > 25 ?
                                             new SolidColorBrush(Colors.Yellow) :
                                             new SolidColorBrush(Colors.OrangeRed);

        public  int Provided
        {
            get => _provided;
            set
            {
                if (SetProperty(ref _provided, value))
                {
                    NotifyCargoChanged();
                }
            }
        }

        public  int Remaining
        {
            get => _remaining;
            set
            {
                if (SetProperty(ref _remaining, value))
                {
                    NotifyCargoChanged();
                }
            }
        }

        public  int Required
        {
            get => _required;
            set
            {
                if (SetProperty(ref _required, value))
                {
                    NotifyCargoChanged();
                }
            }
        }

        #endregion Public Properties
    }
}