using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        public RelayCommand OpenInNewWindowCommand { get; }
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
                    UpdateColonizationDataInternal();
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
        private bool _isInMainWindow = true; // Default to true for the main window
        public bool IsInMainWindow
        {
            get => _isInMainWindow;
            set => SetProperty(ref _isInMainWindow, value);
        }
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
            OpenInNewWindowCommand = new RelayCommand(_ => OpenInNewWindow());

            // Initial update
            UpdateColonizationDataInternal();
        }
      private void OpenInNewWindow()
        {
            // Load settings
            var settings = SettingsManager.Load();

            // Create a new instance of the viewmodel with IsInMainWindow=false
            var popupViewModel = new ColonizationViewModel(_gameState)
            {
                IsInMainWindow = false
            };

            // Copy the current state to the new viewmodel
            popupViewModel.SortBy = this.SortBy;
            popupViewModel.ShowCompleted = this.ShowCompleted;

            // Create a new window
            var window = new Window
            {
                Title = "Colonization Project",
                Width = settings.ColonizationWindowWidth,
                Height = settings.ColonizationWindowHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = settings.ColonizationWindowLeft,
                Top = settings.ColonizationWindowTop,
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                Content = new ColonizationCard { DataContext = popupViewModel }
            };

            // Add event handlers to save position
            window.LocationChanged += (s, e) => {
                if (window.WindowState == WindowState.Normal)
                {
                    settings.ColonizationWindowLeft = window.Left;
                    settings.ColonizationWindowTop = window.Top;
                    SettingsManager.Save(settings);
                }
            };

            window.SizeChanged += (s, e) => {
                if (window.WindowState == WindowState.Normal)
                {
                    settings.ColonizationWindowWidth = window.Width;
                    settings.ColonizationWindowHeight = window.Height;
                    SettingsManager.Save(settings);
                }
            };

            // Ensure window is within screen bounds
            EnsureWindowIsVisible(window, settings);

            // Show the window
            window.Show();
        }
        private void EnsureWindowIsVisible(Window window, AppSettings settings)
        {
            // Get screen information
            var screens = WpfScreenHelper.Screen.AllScreens;
            var screenBounds = WpfScreenHelper.Screen.AllScreens.First().Bounds;



            // Check if window position is valid
            bool isPositionValid = false;
            foreach (var screen in screens)
            {
                var bounds = screen.Bounds;
                if (settings.ColonizationWindowLeft >= bounds.Left &&
                    settings.ColonizationWindowTop >= bounds.Top &&
                    settings.ColonizationWindowLeft + settings.ColonizationWindowWidth <= bounds.Right &&
                    settings.ColonizationWindowTop + settings.ColonizationWindowHeight <= bounds.Bottom)
                {
                    isPositionValid = true;
                    break;
                }
            }

            // If position is invalid, center on primary screen
            if (!isPositionValid)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // After window is loaded, save the new position
                window.Loaded += (s, e) => {
                    settings.ColonizationWindowLeft = window.Left;
                    settings.ColonizationWindowTop = window.Top;
                    settings.ColonizationWindowWidth = window.Width;
                    settings.ColonizationWindowHeight = window.Height;
                    SettingsManager.Save(settings);
                };
            }
        }
        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CurrentColonization))
            {
                UpdateColonizationDataInternal();
            }
        }

        private void UpdateColonizationDataInternal()
        {
            try
            {
                // First evaluate if we should be visible based on data
                var colonizationData = _gameState.CurrentColonization;
                bool hasActiveData = colonizationData != null && !colonizationData.ConstructionComplete;

                // Only update _contextVisible, not IsVisible directly
                SetContextVisibility(hasActiveData);

                // If no data, just clear items and stop
                if (!hasActiveData)
                {
                    RunOnUIThread(() => Items.Clear());
                    return;
                }

                // Now update all the data properties
                RunOnUIThread(() =>
                {
                    HasActiveColonization = hasActiveData;

                    // Update properties
                    ProgressPercentage = colonizationData.ConstructionProgress;
                    LastUpdated = colonizationData.LastUpdated;
                    IsConstructionComplete = colonizationData.ConstructionComplete;
                    CompletedItems = colonizationData.CompletedResources;
                    TotalItems = colonizationData.TotalResources;
                    CompletionText = $"Overall: {colonizationData.CompletionPercentage:N1}% Complete ({CompletedItems}/{TotalItems} resources)";
                    Title = $"Colonization Project ({colonizationData.CompletionPercentage:N1}%)";

                    // Update items
                    Items.Clear();

                    // Filter and sort
                    var resources = colonizationData.ResourcesRequired;
                    if (!ShowCompleted)
                    {
                        resources = resources.Where(r => !r.IsComplete).ToList();
                    }
                    resources = SortResources(resources);

                    // Add all items at once
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
            UpdateColonizationDataInternal();
        }
    }
}