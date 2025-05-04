using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using EliteInfoPanel.Converters;
using WpfScreenHelper;
using System.Windows.Data;
using System.Collections.ObjectModel;
using EliteInfoPanel.Controls;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        #region Private Fields

        private SettingsViewModel _viewModel;
        private bool _originalUseFloating;
        private double _originalFloatingScale;
        private double _originalFullscreenScale;
        private OrderableCheckBoxList _flagsControl;

        #endregion Private Fields

        #region Public Constructors

        public OptionsWindow()
        {
            InitializeComponent();

            // Configure logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("EliteInfoPanel.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Load settings
            var settings = SettingsManager.Load();
            _originalUseFloating = settings.UseFloatingWindow;
            _originalFloatingScale = settings.FloatingFontScale;
            _originalFullscreenScale = settings.FullscreenFontScale;

            settings.DisplayOptions ??= new DisplayOptions();
            settings.DisplayOptions.VisibleFlags ??= new List<Flag>();

            // Initialize font scales if they're 0
            if (settings.FullscreenFontScale <= 0)
                settings.FullscreenFontScale = 1.0;

            if (settings.FloatingFontScale <= 0)
                settings.FloatingFontScale = 1.0;

            Log.Information("Loaded settings: {@Settings}", settings);

            // Create view model
            _viewModel = new SettingsViewModel(settings);
            _viewModel.IsFloatingWindowMode = settings.UseFloatingWindow;
            DataContext = _viewModel;

            // Connect commands
            _viewModel.SaveCommand = new RelayCommand(_ =>
            {
                var settings = _viewModel.AppSettings;
                bool windowModeChanged = _viewModel.IsFloatingWindowMode != _originalUseFloating;
                bool fontScaleChanged = (settings.FloatingFontScale != _originalFloatingScale) ||
                                        (settings.FullscreenFontScale != _originalFullscreenScale);

                // Update the actual setting before saving
                settings.UseFloatingWindow = _viewModel.IsFloatingWindowMode;

                // Update the flags from our orderable control
                if (_flagsControl != null)
                {
                    settings.DisplayOptions.VisibleFlags = _flagsControl.GetSelectedFlags();
                    Log.Debug("Saving ordered flags: {FlagCount} flags", settings.DisplayOptions.VisibleFlags.Count);
                }

                // Save the settings
                _viewModel.SaveSettings();

                // Notify for changes requiring UI updates
                if (windowModeChanged)
                {
                    // Notify about window mode change
                    Log.Information("Window mode changed - notifying main window");
                    WindowModeChanged?.Invoke(_viewModel.IsFloatingWindowMode);
                }

                if (fontScaleChanged)
                {
                    // Notify about font size change
                    Log.Information("Font scale changed - notifying main window");
                    FontSizeChanged?.Invoke();
                }

                // Always notify about card visibility changes
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    Log.Information("Refreshing card visibility based on new settings");
                    mainViewModel.RefreshLayout(true);
                }

                DialogResult = true;
                Close();
            });
            _viewModel.CancelCommand = new RelayCommand(_ =>
            {
                DialogResult = false;
                Close();
            });

            _viewModel.ChangeDisplayCommand = new RelayCommand(_ => ChangeDisplayButton_Click(null, null));

            // Subscribe to events
            _viewModel.ScreenChanged += screen => ScreenChanged?.Invoke(screen);
            _viewModel.FontSizeChanged += () => FontSizeChanged?.Invoke();

            // Position the window
            PositionWindowOnScreen(settings);

            // Load UI when window is shown
            Loaded += (s, e) =>
            {

                PopulateDisplayOptions();
                PopulateFlagOptions();
                PopulateWindowModeOptions();
                PopulateCardOptions();
            };
        }

        #endregion Public Constructors

        #region Public Events

        public event Action<Screen> ScreenChanged;
        public event Action<bool> WindowModeChanged;
        public event Action FontSizeChanged;

        #endregion Public Events

        #region Public Properties

        public Screen SelectedNewScreen { get; private set; }
        public AppSettings Settings => _viewModel.AppSettings;

        #endregion Public Properties

        #region Private Methods

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        private void ChangeDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            var screens = Screen.AllScreens.ToList();
            var dialog = new SelectScreenDialog(screens, this);

            if (dialog.ShowDialog() == true && dialog.SelectedScreen != null)
            {
                _viewModel.SelectScreen(dialog.SelectedScreen);
                Log.Information("User changed display to: {DeviceName}", dialog.SelectedScreen.DeviceName);
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (scalePercentageText != null && sender is Slider slider)
            {
                // Format as percentage
                scalePercentageText.Text = $"Scale: {slider.Value:P0}";
            }
        }

        private void OnDisplayChangeRequested()
        {
            // This calls your existing method for changing displays
            ChangeDisplayButton_Click(null, null);
        }

        private void PopulateDisplayOptions()
        {
            if (FindName("DisplayOptionsPanel") is not StackPanel displayPanel) return;

            displayPanel.Children.Clear();

            var displayOptions = Settings.DisplayOptions;
            var panelOptions = new Dictionary<string, string>
            {
                { nameof(displayOptions.ShowCommanderName), "Commander Name" },
                { nameof(displayOptions.ShowFuelLevel), "Fuel Level" },
                { nameof(displayOptions.ShowCargo), "Cargo" },
                { nameof(displayOptions.ShowBackpack), "Backpack" },
                { nameof(displayOptions.ShowFCMaterials), "Fleet Carrier Materials" },
                { nameof(displayOptions.ShowRoute), "Navigation Route" }
            };
            var dynamicVisibilityOptions = new Dictionary<string, string>
            {
                { nameof(displayOptions.ShowWhenSupercruise), "Show Only in Supercruise" },
                { nameof(displayOptions.ShowWhenDocked), "Show Only When Docked" },
                { nameof(displayOptions.ShowWhenInSRV), "Show Only in SRV" },
                { nameof(displayOptions.ShowWhenOnFoot), "Show Only On Foot" },
                { nameof(displayOptions.ShowWhenInFighter), "Show Only In Fighter" }
            };

            foreach (var option in panelOptions)
            {
                var prop = typeof(DisplayOptions).GetProperty(option.Key);
                bool value = (bool)(prop?.GetValue(displayOptions) ?? false);

                Log.Debug("Loading DisplayOption {Option}: {Value}", option.Key, value);

                var checkbox = new CheckBox
                {
                    Content = option.Value,
                    IsChecked = value,
                    Margin = new Thickness(5),
                    Tag = option.Key
                };

                checkbox.Checked += (s, e) => prop?.SetValue(displayOptions, true);
                checkbox.Unchecked += (s, e) => prop?.SetValue(displayOptions, false);

                displayPanel.Children.Add(checkbox);
            }
        }

        private void PopulateFlagOptions()
        {
            var appSettings = _viewModel.AppSettings;

            // Create and configure the OrderableCheckBoxList control
            _flagsControl = new OrderableCheckBoxList();

            // Initialize with all flags and the selected ones
            var visibleFlags = appSettings.DisplayOptions.VisibleFlags ?? new List<Flag>();
            var allFlags = Enum.GetValues(typeof(Flag)).Cast<Flag>()
                 .Where(f => f != Flag.None)
                 .Concat(new[] { Flag.HudInCombatMode, Flag.Docking }) // Add synthetic flags manually
                 .Distinct(); // Ensure no duplicates

            Log.Information("Populating FlagOptions with {Count} flags", allFlags.Count());

            _flagsControl.InitializeFlags(allFlags, visibleFlags);

            // Add the control to the FlagOptionsPanel
            if (FlagOptionsPanel is Panel panel)
            {
                panel.Children.Clear();
                panel.Children.Add(_flagsControl);
            }
        }

        private void PopulateWindowModeOptions()
        {
            // Find the container for window mode options
            if (FindName("WindowModePanel") is not StackPanel panel) return;

            // Create radio buttons for window mode
            var fullScreenRadio = new RadioButton
            {
                Content = "Full Screen Mode (on selected display)",
                IsChecked = !_viewModel.AppSettings.UseFloatingWindow,
                Margin = new Thickness(5),
                GroupName = "WindowMode"
            };

            var floatingWindowRadio = new RadioButton
            {
                Content = "Floating Window Mode (movable and resizable)",
                IsChecked = _viewModel.AppSettings.UseFloatingWindow,
                Margin = new Thickness(5),
                GroupName = "WindowMode"
            };

            fullScreenRadio.Checked += (s, e) =>
            {
                _viewModel.IsFloatingWindowMode = false;
                // Update font preview for the current mode
                _viewModel.NotifyFontSizeChanged();
            };

            floatingWindowRadio.Checked += (s, e) =>
            {
                _viewModel.IsFloatingWindowMode = true;
                // Update font preview for the current mode
                _viewModel.NotifyFontSizeChanged();
            };

            panel.Children.Add(fullScreenRadio);
            panel.Children.Add(floatingWindowRadio);

            // Add floating window specific options
            var floatingOptions = new StackPanel { Margin = new Thickness(24, 5, 0, 0) };

            // Add always on top checkbox
            var alwaysOnTopCheck = new CheckBox
            {
                Content = "Always on Top",
                IsChecked = _viewModel.AlwaysOnTop,
                Margin = new Thickness(0, 5, 0, 5)
            };

            alwaysOnTopCheck.Checked += (s, e) => _viewModel.AlwaysOnTop = true;
            alwaysOnTopCheck.Unchecked += (s, e) => _viewModel.AlwaysOnTop = false;

            floatingOptions.Children.Add(alwaysOnTopCheck);

            // Create a binding for IsEnabled
            var enabledBinding = new Binding("IsFloatingWindowMode")
            {
                Source = _viewModel
            };

            floatingOptions.SetBinding(IsEnabledProperty, enabledBinding);

            panel.Children.Add(floatingOptions);
        }

        private void PositionWindowOnScreen(AppSettings settings)
        {
            var mainWindowHandle = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
            var mainScreen = Screen.FromHandle(mainWindowHandle);
            var allScreens = Screen.AllScreens;

            // Try to restore last used screen
            var targetScreen = allScreens.FirstOrDefault(s => s.DeviceName == settings.LastOptionsScreenId);

            // If not found or not valid, choose a different one than main, or fallback to main
            if (targetScreen == null || targetScreen.DeviceName == mainScreen.DeviceName)
                targetScreen = allScreens.FirstOrDefault(s => s.DeviceName != mainScreen.DeviceName) ?? mainScreen;

            WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = targetScreen.WpfBounds.Left + (targetScreen.WpfBounds.Width - this.Width) / 2;
            this.Top = targetScreen.WpfBounds.Top + (targetScreen.WpfBounds.Height - this.Height) / 2;
        }
        private void PopulateCardOptions()
        {
            if (CardsOptionsPanel is not StackPanel panel) return;

            panel.Children.Clear();

            var appSettings = _viewModel.AppSettings;

            // Directly referencing top-level properties
            var cardMap = new Dictionary<string, string>
            {
                { nameof(appSettings.ShowSummary), "Summary" },
                { nameof(appSettings.ShowFlags), "Flags" },
                { nameof(appSettings.ShowCargo), "Cargo" },
                { nameof(appSettings.ShowBackpack), "Backpack" },
                { nameof(appSettings.ShowRoute), "Route" },
                { nameof(appSettings.ShowModules), "Modules" },
                { nameof(appSettings.ShowColonisation), "Colonisation" }
            };

            foreach (var entry in cardMap)
            {
                var prop = typeof(AppSettings).GetProperty(entry.Key);
                bool value = (bool)(prop?.GetValue(appSettings) ?? false);

                var checkbox = new CheckBox
                {
                    Content = entry.Value,
                    IsChecked = value,
                    Margin = new Thickness(5),
                    Tag = entry.Key
                };

                checkbox.Checked += (s, e) => prop?.SetValue(appSettings, true);
                checkbox.Unchecked += (s, e) => prop?.SetValue(appSettings, false);

                panel.Children.Add(checkbox);
            }
        }
        private void SaveSettings()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(handle);
            Settings.SelectedScreenId = currentScreen.DeviceName;

            // Update flag settings from the orderable control
            if (_flagsControl != null)
            {
                Settings.DisplayOptions.VisibleFlags = _flagsControl.GetSelectedFlags();
            }

            Log.Information("Saving settings: {@Settings}", Settings);
            _viewModel.SaveCommand.Execute(null);
        }

        #endregion Private Methods
    }
}