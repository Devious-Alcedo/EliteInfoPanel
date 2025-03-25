using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using MaterialDesignThemes.Wpf;
using WpfScreenHelper;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;
using System.Linq;

namespace EliteInfoPanel;

public partial class MainWindow : Window
{
    #region Private Fields

    private AppSettings appSettings = SettingsManager.Load();
    private Screen screen;
    private GameStateService gameState;
    private JournalWatcher journalWatcher;

    private StackPanel summaryContent;
    private StackPanel cargoContent;
    private StackPanel backpackContent;
    private StackPanel fcMaterialsContent;
    private StackPanel routeContent;
    private StackPanel modulesContent;
    private StackPanel shipStatsContent;
    private StackPanel sessionStatsContent;

    private StackPanel fuelStack;
    private TextBlock fuelText;
    private ProgressBar fuelBar;

    #endregion Private Fields

    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
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

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private Card CreateCard(string title, UIElement content)
    {
        var parent = LogicalTreeHelper.GetParent(content) as Panel;
        parent?.Children.Remove(content);

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(content);

        return new Card { Margin = new Thickness(5), Padding = new Thickness(5), Content = panel };
    }

    private void SetupDisplayUi()
    {
        InitializeCards();

        MainGrid.Children.Clear();
        MainGrid.ColumnDefinitions.Clear();

        var display = appSettings.DisplayOptions;
        var panelsToDisplay = new List<UIElement>();

        if (display.ShowCommanderName || display.ShowRoute || display.ShowFuelLevel)
            panelsToDisplay.Add(CreateCard("Summary", summaryContent));
        if (display.ShowCargo)
            panelsToDisplay.Add(CreateCard("Cargo", cargoContent));
        if (display.ShowBackpack)
            panelsToDisplay.Add(CreateCard("Backpack", backpackContent));
        if (display.ShowFCMaterials)
            panelsToDisplay.Add(CreateCard("Fleet Carrier Materials", fcMaterialsContent));
        if (display.ShowRoute)
            panelsToDisplay.Add(CreateCard("Nav Route", routeContent));

        panelsToDisplay.Add(CreateCard("Ship Modules", modulesContent));
        panelsToDisplay.Add(CreateCard("Ship Status", shipStatsContent));
        panelsToDisplay.Add(CreateCard("Session Stats", sessionStatsContent));

        int maxColumns = 6;
        for (int i = 0; i < Math.Min(panelsToDisplay.Count, maxColumns); i++)
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < panelsToDisplay.Count && i < maxColumns; i++)
        {
            Grid.SetColumn(panelsToDisplay[i], i);
            MainGrid.Children.Add(panelsToDisplay[i]);
        }
    }

    private void InitializeCards()
    {
        summaryContent ??= new StackPanel();
        fuelStack ??= new StackPanel();
        cargoContent ??= new StackPanel();
        backpackContent ??= new StackPanel();
        fcMaterialsContent ??= new StackPanel();
        routeContent ??= new StackPanel();
        modulesContent ??= new StackPanel();
        shipStatsContent ??= new StackPanel();
        sessionStatsContent ??= new StackPanel();

        if (fuelText == null)
        {
            fuelText = new TextBlock
            {
                Text = "Fuel:",
                Foreground = GetBodyBrush(),
                FontSize = 26
            };
            fuelBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 32,
                Height = 34,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = Brushes.Orange,
                Background = Brushes.DarkSlateGray
            };
            fuelStack.Children.Add(fuelText);
            fuelStack.Children.Add(fuelBar);
        }
    }

    private void GameState_DataUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            summaryContent.Children.Clear();
            modulesContent.Children.Clear();
            shipStatsContent.Children.Clear();
            sessionStatsContent.Children.Clear();

            var display = appSettings.DisplayOptions;
            var status = gameState.CurrentStatus;
            var cargo = gameState.CurrentCargo;

            if (display.ShowCommanderName)
            {
                summaryContent.Children.Add(new TextBlock
                {
                    Text = $"Commander: {gameState?.CommanderName ?? "(Unknown)"}",
                    Foreground = GetBodyBrush(),
                    FontSize = 24
                });
            }

            if (!string.IsNullOrEmpty(gameState?.CurrentSystem))
            {
                summaryContent.Children.Add(new TextBlock
                {
                    Text = $"System: {gameState.CurrentSystem}",
                    Foreground = GetBodyBrush(),
                    FontSize = 24
                });
            }

            if (!string.IsNullOrEmpty(gameState?.ShipName) || !string.IsNullOrEmpty(gameState?.ShipLocalised))
            {
                var shipLabel = $"Ship: {gameState.UserShipName ?? gameState.ShipName} ";
                if (!string.IsNullOrEmpty(gameState.UserShipId))
                    shipLabel += $" [{gameState.UserShipId}]";
                shipLabel += $"\nType: {gameState.ShipLocalised}";

                summaryContent.Children.Add(new TextBlock
                {
                    Text = shipLabel,
                    Foreground = GetBodyBrush(),
                    FontSize = 24
                });
            }

            if (gameState.Balance.HasValue)
            {
                summaryContent.Children.Add(new TextBlock
                {
                    Text = $"Balance: {gameState.Balance.Value:N0} CR",
                    Foreground = GetBodyBrush(),
                    FontSize = 24
                });
            }

            if (!string.IsNullOrEmpty(gameState?.SquadronName))
            {
                summaryContent.Children.Add(new TextBlock
                {
                    Text = $"Squadron: {gameState.SquadronName}",
                    Foreground = GetBodyBrush(),
                    FontSize = 24
                });
            }

            if (gameState?.JumpCountdown != null && gameState.JumpCountdown.Value.TotalSeconds > 0)
            {
                summaryContent.Children.Add(new TextBlock
                {
                    Text = $"Carrier Jump In: {gameState.JumpCountdown.Value:mm\\:ss}",
                    Foreground = Brushes.Orange,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold
                });
            }

            if (gameState.IsOverheating)
            {
                summaryContent.Children.Add(new TextBlock
                {
                    Text = "WARNING: Ship Overheating!",
                    Foreground = Brushes.Red,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold
                });
            }

            if (display.ShowFuelLevel && status?.Fuel != null)
            {
                fuelText.Text = $"Fuel: Main {status.Fuel.FuelMain:0.00} / Reserve {status.Fuel.FuelReservoir:0.00}";
                if (Math.Abs(fuelBar.Value - status.Fuel.FuelMain) > 0.01)
                    ProgressBarFix.SetValueInstantly(fuelBar, status.Fuel.FuelMain);
                summaryContent.Children.Add(fuelStack);
            }

            if (gameState.CurrentLoadout?.Modules != null)
            {
                foreach (var module in gameState.CurrentLoadout.Modules.OrderByDescending(m => m.Health))
                {
                    modulesContent.Children.Add(new TextBlock
                    {
                        Text = $"{module.Slot}: {module.ItemLocalised ?? module.Item} ({module.Health:P0})",
                        FontSize = 20,
                        Foreground = GetBodyBrush()
                    });
                }
            }

            if (status != null)
            {
                shipStatsContent.Children.Add(new TextBlock
                {
                    Text = $"Shields Up: {status.Flags.HasFlag(Flag.ShieldsUp)}",
                    Foreground = GetBodyBrush(),
                    FontSize = 20
                });

                shipStatsContent.Children.Add(new TextBlock
                {
                    Text = $"Supercruise: {status.Flags.HasFlag(Flag.Supercruise)}",
                    Foreground = GetBodyBrush(),
                    FontSize = 20
                });

                shipStatsContent.Children.Add(new TextBlock
                {
                    Text = $"Hardpoints Deployed: {status.Flags.HasFlag(Flag.HardpointsDeployed)}",
                    Foreground = GetBodyBrush(),
                    FontSize = 20
                });

                shipStatsContent.Children.Add(new TextBlock
                {
                    Text = $"Silent Running: {status.Flags.HasFlag(Flag.SilentRunning)}",
                    Foreground = GetBodyBrush(),
                    FontSize = 20
                });
            }

            sessionStatsContent.Children.Add(new TextBlock
            {
                Text = $"Session Started: {DateTime.Now:HH:mm:ss}",
                FontSize = 20,
                Foreground = GetBodyBrush()
            });
        });
    }

    private Brush GetBodyBrush() => (Brush)Application.Current.Resources["MaterialDesignBody"];

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new OptionsWindow { Owner = this };
        if (options.ShowDialog() == true)
        {
            appSettings = SettingsManager.Load();
            SetupDisplayUi();
            GameState_DataUpdated();
        }
    }

    private Task<Screen?> PromptUserToSelectScreenAsync(List<Screen> screens)
    {
        var dialog = new SelectScreenDialog(screens);
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.SelectedScreen : null);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var allScreens = Screen.AllScreens.ToList();
        screen = allScreens.FirstOrDefault(s => s.DeviceName == appSettings.SelectedScreenId);

        if (screen == null)
        {
            screen = await PromptUserToSelectScreenAsync(allScreens);
            if (screen == null) { Application.Current.Shutdown(); return; }

            appSettings.SelectedScreenId = screen.DeviceName;
            SettingsManager.Save(appSettings);
        }

        ApplyScreenBounds(screen);
        SetupDisplayUi();

        string gamePath = EliteDangerousPaths.GetSavedGamesPath();
        gameState = new GameStateService(gamePath);
        gameState.DataUpdated += GameState_DataUpdated;

        string latestJournal = Directory.GetFiles(gamePath, "Journal.*.log")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(latestJournal))
        {
            journalWatcher = new JournalWatcher(latestJournal);
            journalWatcher.StartWatching();
        }

        GameState_DataUpdated();
    }

    public static class ProgressBarFix
    {
        public static void SetValueInstantly(ProgressBar bar, double value)
        {
            bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
            bar.Value = value;
        }
    }
}
