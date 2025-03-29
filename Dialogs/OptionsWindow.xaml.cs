using System.Windows;
using System.Windows.Controls;
using EliteInfoPanel.Core;
using System.Collections.Generic;
using System.Linq;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        public AppSettings Settings { get; set; }

        private Dictionary<Flag, CheckBox> flagCheckBoxes = new();

        public OptionsWindow()
        {
            InitializeComponent();

            // Center window on screen
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Settings = SettingsManager.Load();
            if (Settings.DisplayOptions.VisibleFlags == null)
                Settings.DisplayOptions.VisibleFlags = new List<Flag>();

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
            var panelOptions = new Dictionary<string, bool>
            {
                { nameof(displayOptions.ShowCommanderName), displayOptions.ShowCommanderName },
                { nameof(displayOptions.ShowFuelLevel), displayOptions.ShowFuelLevel },
                { nameof(displayOptions.ShowCargo), displayOptions.ShowCargo },
                { nameof(displayOptions.ShowBackpack), displayOptions.ShowBackpack },
                { nameof(displayOptions.ShowFCMaterials), displayOptions.ShowFCMaterials },
                { nameof(displayOptions.ShowRoute), displayOptions.ShowRoute }
            };

            foreach (var option in panelOptions)
            {
                var checkbox = new CheckBox
                {
                    Content = option.Key.Replace("Show", "Show "),
                    IsChecked = option.Value,
                    Margin = new Thickness(5),
                    Tag = option.Key
                };

                checkbox.Checked += (s, e) =>
                {
                    var prop = typeof(DisplayOptions).GetProperty(option.Key);
                    if (prop != null) prop.SetValue(displayOptions, true);
                };
                checkbox.Unchecked += (s, e) =>
                {
                    var prop = typeof(DisplayOptions).GetProperty(option.Key);
                    if (prop != null) prop.SetValue(displayOptions, false);
                };

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

                var checkBox = new CheckBox
                {
                    Content = flag.ToString().Replace("_", " "),
                    IsChecked = visibleFlags.Contains(flag),
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
            if (Settings.DisplayOptions.VisibleFlags == null)
                Settings.DisplayOptions.VisibleFlags = new List<Flag>();

            var visibleFlags = Settings.DisplayOptions.VisibleFlags;

            if (isChecked && !visibleFlags.Contains(flag))
                visibleFlags.Add(flag);
            else if (!isChecked && visibleFlags.Contains(flag))
                visibleFlags.Remove(flag);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Ensure we sync checkbox states before saving
            foreach (var kvp in flagCheckBoxes)
            {
                var flag = kvp.Key;
                var isChecked = kvp.Value.IsChecked == true;
                UpdateFlagSetting(flag, isChecked);
            }

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
