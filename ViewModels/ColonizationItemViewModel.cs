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

        // Dynamic properties that look up values from the parent
        public int ShipCargoQuantity
        {
            get
            {
                if (GameState?.CurrentCargo?.Inventory == null) return 0;

                return GameState.CurrentCargo.Inventory
                    .FirstOrDefault(i => string.Equals(i.Name, Name, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            }
        }

        // In ColonizationItemViewModel.cs - add debug logging to CarrierCargoQuantity
        public int CarrierCargoQuantity
        {
            get
            {
                if (GameState?.CurrentCarrierCargo != null)
                {
                    Log.Debug("Available cargo items: {Items}",
                        string.Join(", ", GameState.CurrentCarrierCargo.Select(i => $"\"{i.Name}\"")));

                    Log.Debug("Looking for: \"{ResourceName}\"", Name);
                }

                var match = GameState.CurrentCarrierCargo
                    .FirstOrDefault(i => string.Equals(i.Name, Name, StringComparison.OrdinalIgnoreCase));

                Log.Debug("Checking carrier cargo for {Name}: Found match? {HasMatch}, Quantity: {Quantity}",
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

        public int Provided
        {
            get => _provided;
            set => SetProperty(ref _provided, value);
        }

        public int Remaining
        {
            get => _remaining;
            set => SetProperty(ref _remaining, value);
        }

        public int Required
        {
            get => _required;
            set => SetProperty(ref _required, value);
        }

        #endregion Public Properties

    }

}