using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace EliteInfoPanel.ViewModels
{
    public class ColonizationItemViewModel : ViewModelBase
    {
        private string _name;
        private int _required;
        private int _provided;
        private int _payment;
        private double _completionPercentage;
        private bool _isComplete;
        private int _profitPerUnit;
        private int _fontsize = 14;
        private int _remaining;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Required
        {
            get => _required;
            set => SetProperty(ref _required, value);
        }

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

        public int Payment
        {
            get => _payment;
            set => SetProperty(ref _payment, value);
        }

        public double CompletionPercentage
        {
            get => _completionPercentage;
            set => SetProperty(ref _completionPercentage, value);
        }

        public bool IsComplete
        {
            get => _isComplete;
            set => SetProperty(ref _isComplete, value);
        }

        public int ProfitPerUnit
        {
            get => _profitPerUnit;
            set => SetProperty(ref _profitPerUnit, value);
        }

        public int FontSize
        {
            get => _fontsize;
            set => SetProperty(ref _fontsize, value);
        }

        public Brush ProgressColor => IsComplete ?
                                     new SolidColorBrush(Colors.LimeGreen) :
                                     CompletionPercentage > 75 ?
                                         new SolidColorBrush(Colors.GreenYellow) :
                                         CompletionPercentage > 25 ?
                                             new SolidColorBrush(Colors.Yellow) :
                                             new SolidColorBrush(Colors.OrangeRed);
    }

    public class ColonizationViewModel : CardViewModel
    {
        private readonly GameStateService _gameState;
        private double _progressPercentage;
        private bool _hasActiveColonization;
        private DateTime _lastUpdated;
        private string _completionText;
        private int _completedItems;
        private int _totalItems;
        private bool _isConstructionComplete;
        private string _sortBy = "Missing";
        private bool _showCompleted = true;

        public ObservableCollection<ColonizationItemViewModel> Items { get; } = new ObservableCollection<ColonizationItemViewModel>();

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public bool HasActiveColonization
        {
            get => _hasActiveColonization;
            private set => SetProperty(ref _hasActiveColonization, value);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        public string CompletionText
        {
            get => _completionText;
            set => SetProperty(ref _completionText, value);
        }

        public int CompletedItems
        {
            get => _completedItems;
            set => SetProperty(ref _completedItems, value);
        }

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        public bool IsConstructionComplete
        {
            get => _isConstructionComplete;
            set => SetProperty(ref _isConstructionComplete, value);
        }

        public string SortBy
        {
            get => _sortBy;
            set
            {
                if (SetProperty(ref _sortBy, value))
                {
                    UpdateSort();
                }
            }
        }

        public bool ShowCompleted
        {
            get => _showCompleted;
            set
            {
                if (SetProperty(ref _showCompleted, value))
                {
                    UpdateColonizationData();
                }
            }
        }

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

        public RelayCommand SortByMissing { get; }
        public RelayCommand SortByName { get; }
        public RelayCommand SortByValue { get; }
        public RelayCommand ToggleShowCompleted { get; }

        public ColonizationViewModel(GameStateService gameState) : base("Colonization Project")
        {
            _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

            // Initialize commands
            SortByMissing = new RelayCommand(_ => SortBy = "Missing");
            SortByName = new RelayCommand(_ => SortBy = "Name");
            SortByValue = new RelayCommand(_ => SortBy = "Value");
            ToggleShowCompleted = new RelayCommand(_ => ShowCompleted = !ShowCompleted);

            // Subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // Initial update
            UpdateColonizationData();
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CurrentColonization))
            {
                UpdateColonizationData();
            }
        }

        private void UpdateColonizationData()
        {
            try
            {
                RunOnUIThread(() =>
                {
                    var colonizationData = _gameState.CurrentColonization;
                    HasActiveColonization = colonizationData != null && !colonizationData.ConstructionComplete;

                    // Update visibility
                    IsVisible = HasActiveColonization;

                    if (!HasActiveColonization)
                    {
                        Items.Clear();
                        return;
                    }

                    // Update top-level properties
                    ProgressPercentage = colonizationData.ConstructionProgress;
                    LastUpdated = colonizationData.LastUpdated;
                    IsConstructionComplete = colonizationData.ConstructionComplete;
                    CompletedItems = colonizationData.CompletedResources;
                    TotalItems = colonizationData.TotalResources;

                    CompletionText = $"Overall: {colonizationData.CompletionPercentage:N1}% Complete ({CompletedItems}/{TotalItems} resources)";

                    // Update title with completion percentage
                    Title = $"Colonization Project ({colonizationData.CompletionPercentage:N1}%)";

                    // Clear and rebuild items list
                    Items.Clear();

                    // Filter based on ShowCompleted setting
                    var resources = colonizationData.ResourcesRequired;
                    if (!ShowCompleted)
                    {
                        resources = resources.Where(r => !r.IsComplete).ToList();
                    }

                    // Apply sorting
                    resources = SortResources(resources);

                    foreach (var resource in resources)
                    {
                        Items.Add(new ColonizationItemViewModel
                        {
                            Name = resource.DisplayName,
                            Required = resource.RequiredAmount,
                            Provided = resource.ProvidedAmount,
                            Remaining = resource.RemainingAmount,
                            Payment = resource.Payment,
                            CompletionPercentage = resource.CompletionPercentage,
                            IsComplete = resource.IsComplete,
                            ProfitPerUnit = resource.ProfitPerUnit,
                            FontSize = (int)this.FontSize
                        });
                    }

                    Log.Information("Colonization data updated: {Progress:P2} complete, {CompletedItems}/{TotalItems} resources",
                        ProgressPercentage, CompletedItems, TotalItems);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating colonization data");
            }
        }

        private List<ColonizationResource> SortResources(List<ColonizationResource> resources)
        {
            switch (SortBy)
            {
                case "Name":
                    return resources.OrderBy(r => r.DisplayName).ToList();
                case "Value":
                    return resources.OrderByDescending(r => r.RemainingValue).ThenBy(r => r.DisplayName).ToList();
                case "Missing":
                default:
                    return resources.OrderBy(r => r.IsComplete)
                        .ThenByDescending(r => r.RemainingAmount)
                        .ThenBy(r => r.DisplayName)
                        .ToList();
            }
        }

        private void UpdateSort()
        {
            UpdateColonizationData();
        }
    }
}