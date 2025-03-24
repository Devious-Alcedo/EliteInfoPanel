using System.Windows;
using EliteInfoPanel.Core;
using WpfScreenHelper;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        public AppSettings Settings { get; set; }

        public OptionsWindow()
        {
            InitializeComponent();
            var primaryScreen = Screen.PrimaryScreen;
            this.Left = primaryScreen.WorkingArea.Left + (primaryScreen.WorkingArea.Width - this.Width) / 2;
            this.Top = primaryScreen.WorkingArea.Top + (primaryScreen.WorkingArea.Height - this.Height) / 2;

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
