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
    private StackPanel fuelPanel;
    private StackPanel shipPanel;
    private StackPanel cargoPanel;
    private StackPanel backpackPanel;
    private StackPanel fcMaterialsPanel;


    private Border fuelCard;

    private JournalWatcher journalWatcher;
    private ProgressBar fuelBar;
  
    private StackPanel fuelStack;




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
        SetupFuelCard();
        GameState_DataUpdated(); // do an initial update
    }
    public static class ProgressBarFix
    {
        public static void SetValueInstantly(ProgressBar bar, double value)
        {
            bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null); // Cancel animation
            bar.Value = value;
        }
    }

    private void SetupFuelCard()
    {
        fuelBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 32,
            Height = 24,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = (Brush)Application.Current.Resources["PrimaryHueMidBrush"],
            Background = Brushes.DarkSlateGray
        };

        fuelText = new TextBlock
        {
            Text = "Fuel:",
            Foreground = GetBodyBrush(),
            FontSize = 26
        };

        fuelStack = new StackPanel();
        fuelStack.Children.Add(fuelText);
        fuelStack.Children.Add(fuelBar);

        fuelCard = CreateCard("Fuel", fuelStack); // 🟠 store reference but don't add yet
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
                        FontSize = 26
                    });
                }

                if (display.ShowShipInfo)
                {
                    string ship = gameState?.ShipLocalised ?? status?.ShipType ?? "(Unknown)";
                    summaryStack.Children.Add(new TextBlock
                    {
                        Text = $"Ship: {ship}",
                        Foreground = GetBodyBrush(),
                        FontSize = 26
                    });
                }

                if (display.ShowFuelLevel && status?.Fuel != null)
                {
                    if (!fuelPanel.Children.Contains(fuelCard))
                        fuelPanel.Children.Add(fuelCard);

                    fuelText.Text = $"Fuel: Main {status.Fuel.FuelMain:0.00} / Reserve {status.Fuel.FuelReservoir:0.00}";

                    if (Math.Abs(fuelBar.Value - status.Fuel.FuelMain) > 0.01)
                    {
                        ProgressBarFix.SetValueInstantly(fuelBar, status.Fuel.FuelMain);
                    }
                }
                else
                {
                    // remove if not visible or no data
                    if (fuelPanel.Children.Contains(fuelCard))
                        fuelPanel.Children.Remove(fuelCard);
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
                        FontSize = 26
                    });
                }

                cargoPanel.Children.Add(CreateCard("Cargo", cargoList));
            }

                   
            // BACKPACK
            if (display.ShowBackpack)
            {
                var backpackStack = new StackPanel();

                var grouped = gameState.CurrentBackpack.Inventory
                    .GroupBy(i => i.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
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
                            Text = $"{item.Name_Localised ?? item.Name}: {item.Count}",
                            FontSize = 20,
                            Margin = new Thickness(8, 0, 0, 2),
                            Foreground = GetBodyBrush()
                        });
                    }
                }

                backpackPanel.Children.Add(CreateCard("Backpack", backpackStack));
            }

            // NAV ROUTE
            if (display.ShowRoute && gameState.CurrentRoute?.Route?.Any() == true)
            {
                var routeStack = new StackPanel();

                foreach (var jump in gameState.CurrentRoute.Route)
                {
                    routeStack.Children.Add(new TextBlock
                    {
                        Text = $"{jump.StarSystem} ({jump.StarClass})",
                        FontSize = 20,
                        Margin = new Thickness(8, 0, 0, 2),
                        Foreground = GetBodyBrush()
                    });
                }

                // Place in column 4 (or create a new panel in SetupDisplayUi if needed)
                var routeCard = CreateCard("Nav Route", routeStack);
                InfoPanel.Children.Add(routeCard);
                Grid.SetColumn(routeCard, 4);
            }


            // FCMATERIALS
            if (display.ShowFCMaterials && gameState.CurrentMaterials?.Materials?.Any() == true)
            {
                fcMaterialsPanel.Children.Clear(); // <== Clear first!

                var fcStack = new StackPanel();

                foreach (var item in gameState.CurrentMaterials.Materials.OrderByDescending(i => i.Count))
                {
                    fcStack.Children.Add(new TextBlock
                    {
                        Text = $"{item.Name_Localised ?? item.Name}: {item.Count}",
                        FontSize = 20,
                        Margin = new Thickness(8, 0, 0, 2),
                        Foreground = GetBodyBrush()
                    });
                }

                fcMaterialsPanel.Children.Add(CreateCard("Fleet Carrier Materials", fcStack));
            }
            else
            {
                fcMaterialsPanel.Children.Clear(); // Also clear if nothing to show
            }




        });
    }
    private Brush GetBodyBrush() =>
    (Brush)Application.Current.Resources["MaterialDesignBody"];


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

        // Column 0: Status and Fuel stacked vertically
        var statusAndFuel = new StackPanel();
        summaryPanel = new StackPanel();
        fuelPanel = new StackPanel();
        statusAndFuel.Children.Add(summaryPanel);
        statusAndFuel.Children.Add(fuelPanel);
        Grid.SetColumn(statusAndFuel, 0);
        InfoPanel.Children.Add(statusAndFuel);

        // Column 1: Cargo
        cargoPanel = new StackPanel();
        Grid.SetColumn(cargoPanel, 1);
        InfoPanel.Children.Add(cargoPanel);

        // Column 2: Backpack
        backpackPanel = new StackPanel();
        Grid.SetColumn(backpackPanel, 2);
        InfoPanel.Children.Add(backpackPanel);

        // Column 3: FC Materials
        fcMaterialsPanel = new StackPanel();
        Grid.SetColumn(fcMaterialsPanel, 3);
        InfoPanel.Children.Add(fcMaterialsPanel);


      
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
            Margin = new Thickness(0, 10, 10, 0),
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
                    FontSize = 26,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Orange,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                content
            }
            }
        };
    }


}

