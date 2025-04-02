using System.Windows;
using System.Windows.Controls;
using EliteInfoPanel.Core;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;
using EliteInfoPanel.Util;
using WpfScreenHelper;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        #region Private Fields

        private Dictionary<Flag, CheckBox> flagCheckBoxes = new();

        #endregion Private Fields

        #region Public Constructors

        public OptionsWindow()
        {
            InitializeComponent();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("EliteInfoPanel.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Settings = SettingsManager.Load();
            Settings.DisplayOptions ??= new DisplayOptions();
            Settings.DisplayOptions.VisibleFlags ??= new List<Flag>();

            Log.Information("Loaded settings: {@Settings}", Settings);

            // Determine which screen to open on
            var mainWindowHandle = new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            var mainScreen = WpfScreenHelper.Screen.FromHandle(mainWindowHandle);
            var allScreens = WpfScreenHelper.Screen.AllScreens;

            // Try to restore last used screen
            var targetScreen = allScreens.FirstOrDefault(s => s.DeviceName == Settings.LastOptionsScreenId);

            // If not found or not valid, choose a different one than main, or fallback to main
            if (targetScreen == null || targetScreen.DeviceName == mainScreen.DeviceName)
                targetScreen = allScreens.FirstOrDefault(s => s.DeviceName != mainScreen.DeviceName) ?? mainScreen;

            WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = targetScreen.WpfBounds.Left + (targetScreen.WpfBounds.Width - this.Width) / 2;
            this.Top = targetScreen.WpfBounds.Top + (targetScreen.WpfBounds.Height - this.Height) / 2;

            DataContext = Settings.DisplayOptions;

            Loaded += (s, e) =>
            {
                PopulateDisplayOptions();
                PopulateFlagOptions();
            };
        }

        #endregion Public Constructors

        #region Public Properties

        public AppSettings Settings { get; set; }
        public Screen? SelectedNewScreen { get; private set; }
        #endregion Public Properties
        public event Action<Screen> ScreenChanged;

        #region Private Methods

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentScreen = WpfScreenHelper.Screen.FromHandle(handle);
            Settings.SelectedScreenId = currentScreen.DeviceName;

            foreach (var kvp in flagCheckBoxes)
            {
                var flag = kvp.Key;
                var isChecked = kvp.Value.IsChecked == true;
                UpdateFlagSetting(flag, isChecked);
            }

            Log.Information("Saving settings: {@Settings}", Settings);
            SettingsManager.Save(Settings);
            DialogResult = true;
            Close();
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
        private void ChangeDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            var screens = WpfScreenHelper.Screen.AllScreens.ToList();
            var dialog = new SelectScreenDialog(screens, this);

            if (dialog.ShowDialog() == true && dialog.SelectedScreen != null)
            {
                Settings.SelectedScreenId = dialog.SelectedScreen.DeviceName;
                Settings.SelectedScreenBounds = dialog.SelectedScreen.WpfBounds;

                Log.Information("User changed display to: {DeviceName}", dialog.SelectedScreen.DeviceName);

                ScreenChanged?.Invoke(dialog.SelectedScreen); // Raise the event

               // MessageBox.Show("Display changed.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
