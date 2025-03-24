using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfScreenHelper;

namespace EliteInfoPanel;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Screen screen;
    private AppSettings appSettings = SettingsManager.Load();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var allScreens = Screen.AllScreens.ToList();

        screen = allScreens.FirstOrDefault(s => s.DeviceName == appSettings.SelectedScreenId);

        if (screen == null)
        {
            screen = await PromptUserToSelectScreenAsync(allScreens);
            if (screen == null)
            {
                Application.Current.Shutdown();
                return;
            }

            appSettings.SelectedScreenId = screen.DeviceName;
            SettingsManager.Save(appSettings);
        }

        ApplyScreenBounds(screen);
    }



    private void ApplyScreenBounds(Screen targetScreen)
    {
        this.Left = targetScreen.Bounds.Left;
        this.Top = targetScreen.Bounds.Top;
        this.Width = targetScreen.Bounds.Width;
        this.Height = targetScreen.Bounds.Height;
        this.WindowStyle = WindowStyle.None;
        this.WindowState = WindowState.Maximized;
        this.Topmost = true;
    }

    private Task<Screen?> PromptUserToSelectScreenAsync(List<Screen> screens)
    {
        var dialog = new SelectScreenDialog(screens);
        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            return Task.FromResult<Screen?>(dialog.SelectedScreen);
        }

        return Task.FromResult<Screen?>(null);
    }

}

