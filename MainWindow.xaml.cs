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
using System.Windows.Controls.Primitives;

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
    private StackPanel summaryPanel;
    private StackPanel cargoPanel;
    private StackPanel backpackPanel;
    private JournalWatcher journalWatcher;




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
        // Get latest journal file
        string latestJournal = Directory.GetFiles(gamePath, "Journal.*.log")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(latestJournal))
        {
            journalWatcher = new JournalWatcher(latestJournal);
            journalWatcher.StartWatching();
        }
        
        SetupDisplayUi(); // build controls
        GameState_DataUpdated(); // do an initial update
    }
    public static class ProgressBarFix
    {
        public static void SetValueInstantly(ProgressBar bar, double value)
        {
            bar.BeginAnimation(RangeBase.ValueProperty, null); // cancel animation
            bar.Value = value;
        }
    }

    private void GameState_DataUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            summaryPanel.Children.Clear();
            cargoPanel.Children.Clear();
            backpackPanel.Children.Clear();

            var display = appSettings.DisplayOptions;
            var status = gameState.CurrentStatus;
            var cargo = gameState.CurrentCargo;

            // Summary Card
            if (display.ShowCommanderName || display.ShowShipInfo || display.ShowFuelLevel)
            {
                var summaryStack = new StackPanel();

                if (display.ShowCommanderName)
                {
                    string commander = gameState?.CommanderName ?? "(Unknown)";
                    summaryStack.Children.Add(new TextBlock
                    {
                        Text = $"Commander: {commander}",
                        Foreground = GetBodyBrush(),
                        FontSize = 16
                    });
                }

                if (display.ShowShipInfo)
                {
                    string ship = gameState?.ShipLocalised ?? status?.ShipType ?? "(Unknown)";
                    summaryStack.Children.Add(new TextBlock
                    {
                        Text = $"Ship: {ship}",
                        Foreground = GetBodyBrush(),
                        FontSize = 16
                    });
                }

                if (display.ShowFuelLevel && status?.Fuel != null)
                {
                    var fuelStack = new StackPanel();

                    fuelStack.Children.Add(new TextBlock
                    {
                        Text = $"Fuel: Main {status.Fuel.FuelMain:0.00} / Reserve {status.Fuel.FuelReservoir:0.00}",
                        Foreground = GetBodyBrush(),
                        FontSize = 16
                    });

                    var fuelBar = new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 32,
                        Height = 12,
                        Margin = new Thickness(0, 4, 0, 0),
                        Foreground = (Brush)Application.Current.Resources["PrimaryHueMidBrush"]
                    };

                    ProgressBarFix.SetValueInstantly(fuelBar, status.Fuel.FuelMain);

                    fuelBar.Value = status.Fuel.FuelMain;

                    fuelStack.Children.Add(fuelBar);
                    summaryStack.Children.Add(fuelStack);
                }



                summaryPanel.Children.Add(CreateCard("Status", summaryStack));
            }


            // Cargo Card
            if (display.ShowCargo && cargo?.Inventory != null)
            {
                var cargoList = new StackPanel();

                foreach (var item in cargo.Inventory.OrderByDescending(i => i.Count))
                {
                    cargoList.Children.Add(new TextBlock
                    {
                        Text = $"{item.Name}: {item.Count}",
                        Foreground = GetBodyBrush(),
                        FontSize = 14
                    });
                }

                cargoPanel.Children.Add(CreateCard("Cargo", cargoList));
            }

            // Backpack / Materials Card (placeholder — add models later)
            if (display.ShowBackpack)
            {
                var backpackStack = new StackPanel();

                // Combine backpack and FC materials into one dictionary by category
                var backpackItems = gameState.CurrentBackpack?.Inventory ?? new();
                var fcMaterials = gameState.CurrentMaterials?.Materials ?? new();

                var combined = backpackItems
                    .Select(i => new { i.Category, Name = i.Name_Localised ?? i.Name, i.Count })
                    .Concat(fcMaterials.Select(i => new { i.Category, Name = i.Name_Localised ?? i.Name, i.Count }))
                    .GroupBy(i => i.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in combined)
                {
                    backpackStack.Children.Add(new TextBlock
                    {
                        Text = group.Key,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 8, 0, 4),
                        Foreground = GetBodyBrush()
                    });

                    foreach (var item in group.OrderByDescending(i => i.Count))
                    {
                        backpackStack.Children.Add(new TextBlock
                        {
                            Text = $"{item.Name}: {item.Count}",
                            FontSize = 14,
                            Margin = new Thickness(8, 0, 0, 2),
                            Foreground = GetBodyBrush()
                        });
                    }
                }

                backpackPanel.Children.Add(CreateCard("Backpack / Materials", backpackStack));
            }

        });
    }
    private Brush GetBodyBrush() =>
    (Brush)Application.Current.Resources["MaterialDesignBody"];

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

        summaryPanel = new StackPanel();
        cargoPanel = new StackPanel();
        backpackPanel = new StackPanel();

        InfoPanel.Children.Add(summaryPanel);
        InfoPanel.Children.Add(cargoPanel);
        InfoPanel.Children.Add(backpackPanel);
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

    private Border CreateCard(string title, UIElement content)
    {
        return new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(12),
            Background = (Brush)Application.Current.Resources["MaterialDesignCardBackground"],
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel
            {
                Children =
            {
                new TextBlock
                {
                    Text = title,
                    Style = (Style)Application.Current.Resources["MaterialDesignSubtitle1"]
                },
                content
            }
            }
        };
    }

}

