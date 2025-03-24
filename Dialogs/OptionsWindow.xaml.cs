using System.Windows;
using EliteInfoPanel.Core;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        public AppSettings Settings { get; set; }

        public OptionsWindow()
        {
            InitializeComponent();

            Settings = SettingsManager.Load();
            DataContext = Settings.DisplayOptions;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Save(Settings);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
