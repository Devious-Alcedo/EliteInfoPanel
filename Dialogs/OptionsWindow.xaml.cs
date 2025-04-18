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

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        #region Private Fields

        private SettingsViewModel _viewModel;
        private Dictionary<Flag, CheckBox> flagCheckBoxes = new();
        private bool _originalUseFloating;
        private double _originalFloatingScale;
        private double _originalFullscreenScale;
        public ObservableCollection<CheckBox> FlagCheckBoxes { get; } = new();
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

                // ✅ Update the actual setting before saving
                settings.UseFloatingWindow = _viewModel.IsFloatingWindowMode;

                _viewModel.SaveSettings();

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
            };
        }

        #endregion Public Constructors

        #region Public Events

        public event Action<Screen> ScreenChanged;
        public event Action<bool> WindowModeChanged;
        public event Action FontSizeChanged;
        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (scalePercentageText != null && sender is Slider slider)
            {
                // Format as percentage
                scalePercentageText.Text = $"Scale: {slider.Value:P0}";
            }
        }
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

        private void OnDisplayChangeRequested()
        {
            // This calls your existing method for changing displays
            ChangeDisplayButton_Click(null, null);
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

            // Update the radio button event handlers in PopulateWindowModeOptions() in OptionsWindow.xaml.cs

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

        private void SaveSettings()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(handle);
            Settings.SelectedScreenId = currentScreen.DeviceName;

            foreach (var kvp in flagCheckBoxes)
            {
                var flag = kvp.Key;
                var isChecked = kvp.Value.IsChecked == true;
                UpdateFlagSetting(flag, isChecked);
            }

            Log.Information("Saving settings: {@Settings}", Settings);
            _viewModel.SaveCommand.Execute(null);
        }

        #endregion Private Methods

        #region Private Methods
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

            // then create checkboxes similarly to existing code

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

            FlagCheckBoxes.Clear(); // clear old

            var visibleFlags = appSettings.DisplayOptions.VisibleFlags ?? new List<Flag>();

            foreach (Flag flag in Enum.GetValues(typeof(Flag)))
            {
                if (flag == Flag.None) continue;

                var isChecked = visibleFlags.Contains(flag);
                Log.Debug("Loading FlagOption {Flag}: {Checked}", flag, isChecked);

                var checkBox = new CheckBox
                {
                    Content = flag.ToString().Replace("_", " "),
                    IsChecked = isChecked,
                    Margin = new Thickness(5),
                    Tag = flag,
                    Style = (Style)FindResource("ThemedCheckBoxStyle")
                };

                checkBox.Checked += (s, e) => UpdateFlagSetting(flag, true);
                checkBox.Unchecked += (s, e) => UpdateFlagSetting(flag, false);

                flagCheckBoxes[flag] = checkBox;
                FlagCheckBoxes.Add(checkBox);
            }

            FlagOptionsPanel.ItemsSource = FlagCheckBoxes;
        }


        private void UpdateFlagSetting(Flag flag, bool isChecked)
        {
            var visibleFlags = Settings.DisplayOptions.VisibleFlags;

            if (isChecked && !visibleFlags.Contains(flag))
                visibleFlags.Add(flag);
            else if (!isChecked && visibleFlags.Contains(flag))
                visibleFlags.Remove(flag);

            Log.Debug("Flag {Flag} set to {Checked}", flag, isChecked);
        }
        #endregion Private Methods
    }
}