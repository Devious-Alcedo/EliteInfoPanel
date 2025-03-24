using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfScreenHelper;
using Path = System.IO.Path;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;

namespace EliteInfoPanel;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Screen screen;
    private AppSettings appSettings = SettingsManager.Load();
    private GameStateService gameState;
    private TextBlock commanderText, shipText, fuelText, cargoText;


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

        string gamePath = EliteDangerousPaths.GetSavedGamesPath();
        gameState = new GameStateService(gamePath);
        gameState.DataUpdated += GameState_DataUpdated;

        SetupDisplayUi(); // build controls
        GameState_DataUpdated(); // do an initial update
    }
    private void GameState_DataUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            var display = appSettings.DisplayOptions;
            var status = gameState.CurrentStatus;
            var cargo = gameState.CurrentCargo;

            if (display.ShowCommanderName)
                commanderText.Text = $"Commander: {status?.GameMode}"; // placeholder for Commander name

            if (display.ShowShipInfo)
                shipText.Text = $"Ship: {status?.Ship ?? "N/A"}";

            if (display.ShowFuelLevel)
                fuelText.Text = $"Fuel: {status?.Fuel ?? "N/A"}";

            if (display.ShowCargo && cargo?.Inventory != null)
                cargoText.Text = $"Cargo: {cargo.Inventory.Sum(i => i.Count)} items";
        });
    }
    private void OpenOptions()
    {
        var options = new OptionsWindow();
        options.Owner = this;
        bool? result = options.ShowDialog();

        if (result == true)
        {
            appSettings = SettingsManager.Load();
            GameState_DataUpdated(); // refresh visible UI
        }
    }
    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new OptionsWindow();
        options.Owner = this;
        bool? result = options.ShowDialog();

        if (result == true)
        {
            appSettings = SettingsManager.Load();
            GameState_DataUpdated(); // refresh display
        }
    }

    private void SetupDisplayUi()
    {
        InfoPanel.Children.Clear();

        commanderText = new TextBlock { FontSize = 18, Margin = new Thickness(0, 10, 0, 0) };
        shipText = new TextBlock { FontSize = 18 };
        fuelText = new TextBlock { FontSize = 18 };
        cargoText = new TextBlock { FontSize = 18 };

        InfoPanel.Children.Add(commanderText);
        InfoPanel.Children.Add(shipText);
        InfoPanel.Children.Add(fuelText);
        InfoPanel.Children.Add(cargoText);

    }




    private void ApplyScreenBounds(Screen targetScreen)
    {
        this.Left = targetScreen.WpfBounds.Left;
        this.Top = targetScreen.WpfBounds.Top;
        this.Width = targetScreen.WpfBounds.Width;
        this.Height = targetScreen.WpfBounds.Height;
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();   
    }
}

