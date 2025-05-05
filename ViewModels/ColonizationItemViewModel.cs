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
        private int _profitPerUnit;
        private int _provided;
        private int _remaining;
        private int _required;

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

    public class ColonizationViewModel : CardViewModel
    {
        #region Private Fields

        private readonly GameStateService _gameState;
        private int _completedItems;
        private string _completionText;
        private bool _hasActiveColonization;
        private bool _isConstructionComplete;
        private bool _isInMainWindow = true;
        private DateTime _lastUpdated;
        private double _progressPercentage;
        private bool _showCompleted = true;
        private string _sortBy = "Missing";
        private int _totalItems;

        #endregion Private Fields

        #region Public Constructors

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
            ExportToCsvCommand = new RelayCommand(_ => ExportToCsv());
            // Initial update
            UpdateColonizationDataInternal();
        }

        #endregion Public Constructors

        #region Public Properties

        public int CompletedItems
        {
            get => _completedItems;
            set => SetProperty(ref _completedItems, value);
        }

        public string CompletionText
        {
            get => _completionText;
            set => SetProperty(ref _completionText, value);
        }

        // Add this command property to ColonizationViewModel class
        public RelayCommand ExportToCsvCommand { get; }

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

        public bool HasActiveColonization
        {
            get => _hasActiveColonization;
            private set => SetProperty(ref _hasActiveColonization, value);
        }

        public bool IsConstructionComplete
        {
            get => _isConstructionComplete;
            set => SetProperty(ref _isConstructionComplete, value);
        }

        public bool IsInMainWindow
        {
            get => _isInMainWindow;
            set => SetProperty(ref _isInMainWindow, value);
        }

        public ObservableCollection<ColonizationItemViewModel> Items { get; } = new ObservableCollection<ColonizationItemViewModel>();
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        public RelayCommand OpenInNewWindowCommand { get; }
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
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

        public RelayCommand SortByMissing { get; }

        public RelayCommand SortByName { get; }

        public RelayCommand SortByValue { get; }

        public RelayCommand ToggleShowCompleted { get; }

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        #endregion Public Properties

        #region Private Methods

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
                window.Loaded += (s, e) =>
                {
                    settings.ColonizationWindowLeft = window.Left;
                    settings.ColonizationWindowTop = window.Top;
                    settings.ColonizationWindowWidth = window.Width;
                    settings.ColonizationWindowHeight = window.Height;
                    SettingsManager.Save(settings);
                };
            }
        }

        // Default to true for the main window
        // Add this method to ColonizationViewModel.cs
        private void ExportToCsv()
        {
            try
            {
                if (_gameState.CurrentColonization?.ResourcesRequired == null ||
                    !_gameState.CurrentColonization.ResourcesRequired.Any())
                {
                    ShowToast("No colonization data to export.");
                    return;
                }

                // Create a save file dialog
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "ColonizationData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                    DefaultExt = ".csv",
                    Filter = "CSV documents (.csv)|*.csv"
                };

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    var resources = _gameState.CurrentColonization.ResourcesRequired;

                    // Format: Name,Required,Provided,Remaining,Completion %,Payment per unit
                    var csvContent = new StringBuilder();
                    csvContent.AppendLine("Resource,Required,Provided,Remaining,Completion %,Payment per unit");

                    foreach (var resource in resources)
                    {
                        csvContent.AppendLine($"\"{resource.DisplayName}\",{resource.RequiredAmount},{resource.ProvidedAmount}," +
                                             $"{resource.RemainingAmount},{resource.CompletionPercentage:F1},{resource.Payment}");
                    }

                    // Add a summary line
                    csvContent.AppendLine();
                    csvContent.AppendLine($"\"Overall Progress\",,,," +
                                         $"{_gameState.CurrentColonization.CompletionPercentage:F1}%,");
                    csvContent.AppendLine($"\"Last Updated\",\"{_gameState.CurrentColonization.LastUpdated:g}\",,,,");

                    File.WriteAllText(dialog.FileName, csvContent.ToString());

                    ShowToast($"Exported to {dialog.FileName}");

                    // Open the folder containing the file
                    Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting colonization data to CSV");
                ShowToast("Failed to export data: " + ex.Message);
            }
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.CurrentColonization))
            {
                UpdateColonizationDataInternal();
            }
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
            window.LocationChanged += (s, e) =>
            {
                if (window.WindowState == WindowState.Normal)
                {
                    settings.ColonizationWindowLeft = window.Left;
                    settings.ColonizationWindowTop = window.Top;
                    SettingsManager.Save(settings);
                }
            };

            window.SizeChanged += (s, e) =>
            {
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

        // Add this to handle showing toast notifications
        private void ShowToast(string message)
        {
            // Find the main VM through the application
            if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.ShowToast(message);
            }
            else
            {
                // Fallback if MainViewModel not accessible
                System.Diagnostics.Debug.WriteLine($"Toast: {message}");
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
        private void UpdateSort()
        {
            UpdateColonizationDataInternal();
        }

        #endregion Private Methods
    }
}