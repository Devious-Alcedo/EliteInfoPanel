using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using WpfScreenHelper;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace EliteInfoPanel
{
    public partial class SelectScreenDialog : Window
    {
        public Screen? SelectedScreen { get; private set; }


        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScreenListBox.SelectedItem != null)
            {
                var screenProp = ScreenListBox.SelectedItem.GetType().GetProperty("Screen");
                SelectedScreen = screenProp?.GetValue(ScreenListBox.SelectedItem) as Screen;
                DialogResult = true; // ✅ this closes the dialog
            }
        }
        public SelectScreenDialog(List<Screen> screens, Window ownerWindow)
        {
            InitializeComponent();

            Owner = ownerWindow; // ensure modal behavior

            ScreenListBox.ItemsSource = screens.Select((s, i) => new
            {
                Screen = s,
                DisplayText = $"Display {i + 1}: {s.DeviceName} ({s.WpfBounds.Width}x{s.WpfBounds.Height})"
            }).ToList();

            ScreenListBox.DisplayMemberPath = "DisplayText";

            // ✅ Center on the screen the owner is currently on
            var ownerHandle = new System.Windows.Interop.WindowInteropHelper(ownerWindow).Handle;
            var ownerScreen = WpfScreenHelper.Screen.FromHandle(ownerHandle);

            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = ownerScreen.WpfBounds.Left + (ownerScreen.WpfBounds.Width - this.Width) / 2;
            this.Top = ownerScreen.WpfBounds.Top + (ownerScreen.WpfBounds.Height - this.Height) / 2;

            this.Topmost = true;
            this.Loaded += (s, e) =>
            {
                this.Activate();
                this.Focus();
            };
        }



        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void IdentifyScreens_Click(object sender, RoutedEventArgs e)
        {
            int index = 1;
            foreach (var screen in Screen.AllScreens)
            {
                var overlay = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    Topmost = true,
                    ShowInTaskbar = false,
                    Left = screen.WpfBounds.Left,
                    Top = screen.WpfBounds.Top,
                    Width = screen.WpfBounds.Width,
                    Height = screen.WpfBounds.Height,
                    Content = new Grid
                    {
                        Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                        Children =
                {
                    new TextBlock
                    {
                        Text = index.ToString(),
                        FontSize = Math.Min(screen.WpfBounds.Height, 200), // scale font if height is small
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                }
                    }
                };

                overlay.Show();

                // Close after 2 seconds
                Task.Delay(2000).ContinueWith(_ => overlay.Dispatcher.Invoke(() => overlay.Close()));
                index++;
            }
        }

    }

}


