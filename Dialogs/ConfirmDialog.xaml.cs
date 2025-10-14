using System.Windows;

namespace EliteInfoPanel.Dialogs
{
    public partial class ConfirmDialog : Window
    {
        private bool _showSecondary = true;
        public string TitleText
        {
            get => TitleTextBlock.Text;
            set => TitleTextBlock.Text = value;
        }

        public string MessageText
        {
            get => MessageTextBlock.Text;
            set => MessageTextBlock.Text = value;
        }

        public string PrimaryButtonText
        {
            get => (string)PrimaryButton.Content;
            set => PrimaryButton.Content = value;
        }

        public string SecondaryButtonText
        {
            get => (string)SecondaryButton.Content;
            set => SecondaryButton.Content = value;
        }

        public ConfirmDialog()
        {
            InitializeComponent();
            Topmost = true;
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static bool Show(Window owner, string title, string message, string primaryText = "OK", string secondaryText = "Cancel", bool showSecondary = true)
        {
            var dlg = new ConfirmDialog
            {
                Owner = owner,
                Title = title,
                TitleText = title,
                MessageText = message,
                PrimaryButtonText = primaryText,
                SecondaryButtonText = secondaryText,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            dlg._showSecondary = showSecondary;
            dlg.SecondaryButton.Visibility = showSecondary ? Visibility.Visible : Visibility.Collapsed;

            return dlg.ShowDialog() == true;
        }

        public static bool ShowInfo(Window owner, string title, string message, string buttonText = "OK")
        {
            return Show(owner, title, message, buttonText, secondaryText: string.Empty, showSecondary: false);
        }

        public static bool ShowError(Window owner, string title, string message, string buttonText = "OK")
        {
            return Show(owner, title, message, buttonText, secondaryText: string.Empty, showSecondary: false);
        }
    }
}
