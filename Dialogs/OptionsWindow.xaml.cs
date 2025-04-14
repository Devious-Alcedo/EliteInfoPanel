using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using WpfScreenHelper;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        #region Private Fields

        private SettingsViewModel _viewModel;
        private Dictionary<Flag, CheckBox> flagCheckBoxes = new();

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
            settings.DisplayOptions ??= new DisplayOptions();
            settings.DisplayOptions.VisibleFlags ??= new List<Flag>();

            Log.Information("Loaded settings: {@Settings}", settings);

            // Create view model
            _viewModel = new SettingsViewModel(settings);
            DataContext = _viewModel;
            // Connect commands
            _viewModel.SaveCommand = new RelayCommand(_ =>
            {
                // Don't call SaveSettings() again from here
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var currentScreen = Screen.FromHandle(handle);
                Settings.SelectedScreenId = currentScreen.DeviceName;

                // Save the settings
                _viewModel.SaveSettings(); // Call the ViewModel's save method

                // Close the dialog
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

            // Position the window
            PositionWindowOnScreen(settings);

            // Set the data context
          

            // Load UI when window is shown
            Loaded += (s, e) =>
            {
                PopulateDisplayOptions();
                PopulateFlagOptions();
            };
        }

        #endregion Public Constructors

        #region Public Events

        public event Action<Screen> ScreenChanged;

        #endregion Public Events

        #region Public Properties

        public Screen SelectedNewScreen { get; private set; }
        public AppSettings Settings => _viewModel.AppSettings;

        #endregion Public Properties

        #region Private Methods

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Don't call SaveSettings() here, just execute the command
            _viewModel.SaveCommand.Execute(null);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void OnDisplayChangeRequested()
        {
            // This calls your existing method for changing displays
            ChangeDisplayButton_Click(null, null);
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
            var appSettings = SettingsManager.Load();

            if (FindName("FlagOptionsPanel") is not StackPanel flagPanel) return;

            flagPanel.Children.Clear();
            flagCheckBoxes.Clear();

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
                    Tag = flag
                };

                checkBox.Checked += (s, e) => UpdateFlagSetting(flag, true);
                checkBox.Unchecked += (s, e) => UpdateFlagSetting(flag, false);

                flagPanel.Children.Add(checkBox);
                flagCheckBoxes[flag] = checkBox;
            }
        }

        private void UpdateFlagSetting(Flag flag, bool isChecked)
        {
            var visibleFlags = Settings.DisplayOptions.VisibleFlags;

            if (isChecked && !visibleFlags.Contains(flag))
                visibleFlags.Add(flag);
            else if (!isChecked && visibleFlags.Contains(flag))
                visibleFlags.Remove(flag);

            Log.Information("Flag {Flag} set to {Checked}", flag, isChecked);
        }
        #endregion Private Methods
    }
}
