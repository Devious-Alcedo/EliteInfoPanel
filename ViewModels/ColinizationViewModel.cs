using EliteInfoPanel.Controls;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using System.IO;
using System.Windows.Threading;
using EliteInfoPanel.Services;

namespace EliteInfoPanel.ViewModels
{
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
        private long? _selectedMarketId;
        private bool _showCompleted = true;
        private string _sortBy = "Missing";
        private int _totalItems;
        public RelayCommand ForceRefreshCommand { get; }

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
            ForceRefreshCommand = new RelayCommand(_ => ForceRefresh());
            OpenInNewWindowCommand = new RelayCommand(_ => OpenInNewWindow());
            ExportToCsvCommand = new RelayCommand(_ => ExportToCsv());

            // Subscribe to property changes
            _gameState.PropertyChanged += GameState_PropertyChanged;

            // ADD THIS: Load available depots
            RefreshAvailableDepots();

            // Initial update
            UpdateColonizationDataInternal();

            Log.Information("ColonizationViewModel initialized with GameState: {HasGameState}",
                _gameState != null);
        }


        #endregion Public Constructors

        #region Public Properties
        public ObservableCollection<ColonizationDepotInfo> AvailableDepots { get; } = new ObservableCollection<ColonizationDepotInfo>();

        public ColonizationDepotInfo SelectedDepot
        {
            get => AvailableDepots.FirstOrDefault(d => d.MarketID == _selectedMarketId);
            set
            {
                if (value != null && _selectedMarketId != value.MarketID)
                {
                    _selectedMarketId = value.MarketID;
                    _gameState.SelectedDepotMarketId = value.MarketID;
                    OnPropertyChanged();
                    UpdateColonizationDataInternal();
                }
            }
        }
        //set back to one for release!
        public bool HasMultipleDepots => AvailableDepots.Count > 0;
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
        public int GetShipCargoQuantity(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName) || _gameState.CurrentCargo?.Inventory == null)
                return 0;

            return _gameState.CurrentCargo.Inventory
                .FirstOrDefault(i => string.Equals(i.Name, resourceName, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
        }

        public int GetCarrierCargoQuantity(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName) || _gameState.CurrentCarrierCargo == null)
                return 0;

            return _gameState.CurrentCarrierCargo
                .FirstOrDefault(i => string.Equals(i.Name, resourceName, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
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
        // Replace the ExportToCsv method in ColonizationViewModel.cs with this enhanced version

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

                    // Enhanced CSV format with cargo information
                    var csvContent = new StringBuilder();
                    csvContent.AppendLine("Resource,Required,Provided,Remaining,Ship Cargo,Carrier Cargo,Total Available,Remaining to Acquire,Completion %,Payment per unit");

                    foreach (var resource in resources)
                    {
                        // Get cargo quantities using the display name (which matches what's shown in the UI)
                        int shipCargo = GetShipCargoQuantity(resource.DisplayName);
                        int carrierCargo = GetCarrierCargoQuantity(resource.DisplayName);
                        int totalAvailable = shipCargo + carrierCargo;

                        // Calculate how much we still need to acquire
                        // This is the remaining amount minus what we already have
                        int remainingToAcquire = Math.Max(0, resource.RemainingAmount - totalAvailable);

                        csvContent.AppendLine($"\"{resource.DisplayName}\"," +
                                             $"{resource.RequiredAmount}," +
                                             $"{resource.ProvidedAmount}," +
                                             $"{resource.RemainingAmount}," +
                                             $"{shipCargo}," +
                                             $"{carrierCargo}," +
                                             $"{totalAvailable}," +
                                             $"{remainingToAcquire}," +
                                             $"{resource.CompletionPercentage:F1}," +
                                             $"{resource.Payment}");
                    }

                    // Add summary information
                    csvContent.AppendLine();
                    csvContent.AppendLine("Summary Information");
                    csvContent.AppendLine($"\"Overall Progress\",,,,,,,," +
                                         $"{_gameState.CurrentColonization.CompletionPercentage:F1}%,");
                    csvContent.AppendLine($"\"Last Updated\",\"{_gameState.CurrentColonization.LastUpdated:g}\",,,,,,,,");

                    // Calculate totals for the summary
                    int totalShipCargo = 0;
                    int totalCarrierCargo = 0;
                    int totalRemainingToAcquire = 0;
                    long totalValue = 0;

                    foreach (var resource in resources)
                    {
                        int shipCargo = GetShipCargoQuantity(resource.DisplayName);
                        int carrierCargo = GetCarrierCargoQuantity(resource.DisplayName);
                        int remainingToAcquire = Math.Max(0, resource.RemainingAmount - (shipCargo + carrierCargo));

                        totalShipCargo += shipCargo;
                        totalCarrierCargo += carrierCargo;
                        totalRemainingToAcquire += remainingToAcquire;
                        totalValue += (long)remainingToAcquire * resource.Payment;
                    }

                    csvContent.AppendLine();
                    csvContent.AppendLine("Totals");
                    csvContent.AppendLine($"\"Total Ship Cargo\",,,,,{totalShipCargo},,,,");
                    csvContent.AppendLine($"\"Total Carrier Cargo\",,,,,,{totalCarrierCargo},,,");
                    csvContent.AppendLine($"\"Total Still to Acquire\",,,,,,,{totalRemainingToAcquire},,");
                    csvContent.AppendLine($"\"Estimated Value of Remaining Work\",,,,,,,\"{totalValue:N0} credits\",,");

                    File.WriteAllText(dialog.FileName, csvContent.ToString());

                    ShowToast($"Enhanced colonization data exported to {Path.GetFileName(dialog.FileName)}");

                    // Open the folder containing the file
                    Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting enhanced colonization data to CSV");
                ShowToast("Failed to export data: " + ex.Message);
            }
        }
        private void RefreshCargoQuantities()
        {
            // Refresh cargo quantities for all items when cargo data changes
            foreach (var item in Items)
            {
                item.NotifyCargoChanged();
            }

            Log.Debug("ColonizationViewModel: Refreshed cargo quantities for {Count} items", Items.Count);
        }
        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Log.Debug("ColonizationViewModel: PropertyChanged event received for {Property}", e.PropertyName);

            if (e.PropertyName == nameof(GameStateService.CurrentColonization) ||
                e.PropertyName == nameof(GameStateService.ColonizationDepots)) // ADD THIS CHECK
            {
                Log.Information("ColonizationViewModel: CurrentColonization changed - forcing update");

                // Force immediate UI thread update
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshAvailableDepots(); // ADD THIS
                    UpdateColonizationDataInternal();
                }), DispatcherPriority.Normal);
            }
            else if (e.PropertyName == nameof(GameStateService.CurrentCarrierCargo) ||
                     e.PropertyName == nameof(GameStateService.CurrentCargo))
            {
                Log.Debug("ColonizationViewModel: Cargo data changed - updating cargo quantities");

                // Only update if we have active colonization data
                if (_gameState.CurrentColonization != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RefreshCargoQuantities();
                        PublishColonizationMqtt();
                    }), DispatcherPriority.Background);
                }
            }
        }
        private async void PublishColonizationMqtt()
        {
            try
            {
                var colonizationData = _gameState.CurrentColonization;
                if (colonizationData == null || !Items.Any())
                {
                    Log.Warning("ColonizationViewModel: No colonization data to publish via MQTT");
                    return;
                }

                // Construct resource list for MQTT
                var resources = Items.Select(item => new ColonizationResource
                {
                    Name = item.Name,
                    Name_Localised = item.Name, // Or adjust if needed
                    RequiredAmount = item.Required,
                    ProvidedAmount = item.Provided,
                    Payment = item.Payment,
                    // Additional fields
                    ShipAmount = item.ShipCargoQuantity,
                    CarrierAmount = item.CarrierCargoQuantity,
                }).ToList();

                await MqttService.Instance.PublishColonizationDepotAsync(
                    marketId: colonizationData.MarketID,
                    progress: colonizationData.ConstructionProgress,
                    complete: colonizationData.ConstructionComplete,
                    failed: colonizationData.ConstructionFailed,
                    resources: resources
                );

                Log.Information("ColonizationViewModel: Published colonization data via MQTT for depot {MarketID}", colonizationData.MarketID);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ColonizationViewModel: Error publishing colonization data via MQTT");
            }
        }

        private void ForceRefresh()
        {
            try
            {
                Log.Information("🔄 Force refreshing colonization data...");
                ShowToast("Refreshing colonization data...");

                // Force refresh the game state data
                _gameState.ForceRefreshColonizationData();

                // Also immediately update our local data
                UpdateColonizationDataInternal();

                ShowToast("Colonization data refreshed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during force refresh");
                ShowToast("Failed to refresh data: " + ex.Message);
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
        private void RefreshAvailableDepots()
        {
            AvailableDepots.Clear();

            foreach (var depot in _gameState.GetActiveColonizationDepots())
            {
                AvailableDepots.Add(new ColonizationDepotInfo
                {
                    MarketID = depot.MarketID,
                    Progress = depot.ConstructionProgress,
                    ResourceCount = depot.ResourcesRequired?.Count ?? 0,
                    CompletedCount = depot.ResourcesRequired?.Count(r => r.IsComplete) ?? 0,
                    SystemName = GetSystemNameForDepot(depot.MarketID)
                });
            }

            // Select the currently selected depot or first if none
            _selectedMarketId = _gameState.SelectedDepotMarketId ?? AvailableDepots.FirstOrDefault()?.MarketID;

            OnPropertyChanged(nameof(AvailableDepots));
            OnPropertyChanged(nameof(SelectedDepot));
            OnPropertyChanged(nameof(HasMultipleDepots));
        }

        // ADD THIS NEW METHOD:
        private string GetSystemNameForDepot(long marketId)
        {
            // For now, return a default format
            // You might want to track the actual system name when processing the event
            return $"Depot {marketId}";
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
                Log.Debug("UpdateColonizationDataInternal: Starting update");

                // Ensure we're on the UI thread
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(UpdateColonizationDataInternal);
                    return;
                }

                var colonizationData = _gameState.CurrentColonization;

                if (colonizationData == null)
                {
                    Log.Information("UpdateColonizationDataInternal: No colonization data available");
                    SetContextVisibility(false);
                    Items.Clear();
                    return;
                }

                Log.Information("UpdateColonizationDataInternal: Processing data - Progress={Progress:P2}, Resources={Count}, LastUpdated={LastUpdated}",
                    colonizationData.ConstructionProgress,
                    colonizationData.ResourcesRequired?.Count ?? 0,
                    colonizationData.LastUpdated);

                // Check if construction is complete or failed
                bool hasActiveData = !colonizationData.ConstructionComplete && !colonizationData.ConstructionFailed;

                if (!hasActiveData)
                {
                    Log.Information("UpdateColonizationDataInternal: Construction complete or failed - hiding card");
                    SetContextVisibility(false);
                    Items.Clear();
                    return;
                }

                // Set context visibility
                SetContextVisibility(true);

                // Update properties
                ProgressPercentage = colonizationData.ConstructionProgress;
                LastUpdated = colonizationData.LastUpdated;
                IsConstructionComplete = colonizationData.ConstructionComplete;
                CompletedItems = colonizationData.CompletedResources;
                TotalItems = colonizationData.TotalResources;
                CompletionText = $"Overall: {colonizationData.CompletionPercentage:N1}% Complete ({CompletedItems}/{TotalItems} resources)";
                Title = $"Colonization Project ({colonizationData.CompletionPercentage:N1}%)";

                Log.Information("UpdateColonizationDataInternal: Updated progress to {Progress:P2} ({Completed}/{Total} resources)",
                    colonizationData.ConstructionProgress, CompletedItems, TotalItems);

                // Update items
                Items.Clear();

                if (colonizationData.ResourcesRequired == null || !colonizationData.ResourcesRequired.Any())
                {
                    Log.Warning("UpdateColonizationDataInternal: No resources found in colonization data");
                    return;
                }

                // Filter and sort resources
                var resources = colonizationData.ResourcesRequired.ToList();
                if (!ShowCompleted)
                {
                    resources = resources.Where(r => !r.IsComplete).ToList();
                    Log.Debug("UpdateColonizationDataInternal: Filtered to {Count} incomplete resources", resources.Count);
                }

                resources = SortResources(resources);

                // Create lookup dictionaries for cargo quantities with better error handling
                var shipCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (_gameState.CurrentCargo?.Inventory != null)
                {
                    foreach (var item in _gameState.CurrentCargo.Inventory)
                    {
                        if (!string.IsNullOrEmpty(item.Name))
                        {
                            shipCargo[item.Name] = item.Count;
                        }
                    }
                    Log.Debug("UpdateColonizationDataInternal: Ship cargo has {Count} items", shipCargo.Count);
                }

                var carrierCargo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (_gameState.CurrentCarrierCargo != null)
                {
                    foreach (var item in _gameState.CurrentCarrierCargo)
                    {
                        if (!string.IsNullOrEmpty(item.Name))
                        {
                            if (carrierCargo.TryGetValue(item.Name, out int existingQty))
                            {
                                carrierCargo[item.Name] = existingQty + item.Quantity;
                            }
                            else
                            {
                                carrierCargo[item.Name] = item.Quantity;
                            }
                        }
                    }
                    Log.Debug("UpdateColonizationDataInternal: Carrier cargo has {Count} items", carrierCargo.Count);
                }

                // Add items to the collection
                foreach (var resource in resources)
                {
                    try
                    {
                        var viewModel = new ColonizationItemViewModel
                        {
                            Name = resource.DisplayName,
                            Required = resource.RequiredAmount,
                            Provided = resource.ProvidedAmount,
                            Remaining = resource.RemainingAmount,
                            Payment = resource.Payment,
                            CompletionPercentage = resource.CompletionPercentage,
                            IsComplete = resource.IsComplete,
                            ProfitPerUnit = resource.ProfitPerUnit,
                            FontSize = (int)this.FontSize,
                            GameState = _gameState
                        };

                        Items.Add(viewModel);

                        Log.Debug("UpdateColonizationDataInternal: Added resource {Name} - {Provided}/{Required} ({Percentage:P1})",
                            resource.DisplayName,
                            resource.ProvidedAmount,
                            resource.RequiredAmount,
                            resource.CompletionPercentage / 100.0);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error adding resource {Name} to items", resource.DisplayName);
                    }
                }

                Log.Information("UpdateColonizationDataInternal: Update complete - Added {Count} resource items", Items.Count);
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
    // Add this class at the bottom of the ColonizationViewModel.cs file
    public class ColonizationDepotInfo
    {
        public long MarketID { get; set; }
        public double Progress { get; set; }
        public int ResourceCount { get; set; }
        public int CompletedCount { get; set; }
        public string SystemName { get; set; }
        public string DisplayName => $"{SystemName} ({Progress:P0} - {CompletedCount}/{ResourceCount})";
    }

}
