using System.Windows;
using System.Windows.Controls;
using EliteInfoPanel.Core;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        public AppSettings Settings { get; set; }

        private Dictionary<Flag, CheckBox> flagCheckBoxes = new();

        public OptionsWindow()
        {
            InitializeComponent();

            // Setup Serilog for logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("EliteInfoPanel.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Center window on screen
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Settings = SettingsManager.Load();
            Settings.DisplayOptions ??= new DisplayOptions();
            Settings.DisplayOptions.VisibleFlags ??= new List<Flag>();

            Log.Information("Loaded settings: {@Settings}", Settings);

            DataContext = Settings.DisplayOptions;

            Loaded += (s, e) =>
            {
                PopulateDisplayOptions();
                PopulateFlagOptions();
            };
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
            if (FindName("FlagOptionsPanel") is not StackPanel flagPanel) return;

            flagPanel.Children.Clear();
            flagCheckBoxes.Clear();

            var visibleFlags = Settings.DisplayOptions.VisibleFlags ?? new List<Flag>();

            foreach (Flag flag in System.Enum.GetValues(typeof(Flag)))
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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
