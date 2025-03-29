using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using MaterialDesignThemes.Wpf;
using WpfScreenHelper;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;
using Serilog;
using EliteInfoPanel.Util;

namespace EliteInfoPanel;

public partial class MainWindow : Window
{


    #region Private Fields

    private readonly Dictionary<Flag, string> FlagEmojiMap = new()
{
    { Flag.ShieldsUp, "🛡" },
    { Flag.Supercruise, "🚀" },
    { Flag.HardpointsDeployed, "🔫" },
    { Flag.SilentRunning, "🤫" },
    { Flag.Docked, "⚓" },
    { Flag.CargoScoopDeployed, "📦" },
    { Flag.FlightAssistOff, "🎮" },
    { Flag.NightVision, "🌙" },
    { Flag.OverHeating, "🔥" }
};
    private Dictionary<string, Card> cardMap = new();
    private AppSettings appSettings = SettingsManager.Load();
    private StackPanel backpackContent;
    private StackPanel cargoContent;
    private StackPanel fcMaterialsContent;
    private StackPanel flagsPanel1;
    private StackPanel flagsPanel2;
    private ProgressBar fuelBar;
    private StackPanel fuelStack;
    private TextBlock fuelText;
    private GameStateService gameState;
    private JournalWatcher journalWatcher;
    private double lastFuelValue = -1;
    private StackPanel modulesContent;
    private StackPanel routeContent;
    private Screen screen;
    private StackPanel shipStatsContent;
    private StackPanel summaryContent;

    #endregion Private Fields

    #region Public Constructors

    public MainWindow()
    {
        InitializeComponent();
        LoggingConfig.Configure();
        Loaded += Window_Loaded;
    }

    #endregion Public Constructors

    #region Private Methods

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

    private void GameState_DataUpdated()
    {
        
        Dispatcher.Invoke(() =>
        {
            var status = gameState.CurrentStatus;
            Log.Information("Data updated: {@Status}", status);
            bool showPanels = status != null && (
             status.Flags.HasFlag(Flag.Docked) ||
             status.Flags.HasFlag(Flag.Supercruise) ||
             status.Flags.HasFlag(Flag.InSRV) ||
             status.OnFoot ||
             status.Flags.HasFlag(Flag.InFighter) ||
             status.Flags.HasFlag(Flag.InMainShip));


            MainGrid.Visibility = showPanels ? Visibility.Visible : Visibility.Collapsed;
            Log.Information("MainGrid visibility set to: {Visible}", showPanels);
            if (!showPanels) return;

            UpdateSummaryCard();
            UpdateMaterialsCard();
            UpdateRouteCard();
            UpdateFuelDisplay(status);

            // Card-level visibility managed here
            UpdateCargoCard(status, cardMap["Cargo"]);
            UpdateBackpackCard(status, cardMap["Backpack"]);
            UpdateModulesCard(status, cardMap["Ship Modules"]);
        });
    }


    #region cards

    private void UpdateCargoCard(StatusJson status, Card cargoCard)
    {
        cargoContent.Children.Clear();
        bool showCargo = status.Flags.HasFlag(Flag.InSRV) || status.Flags.HasFlag(Flag.InMainShip);
        cargoCard.Visibility = showCargo ? Visibility.Visible : Visibility.Collapsed;

        if (!showCargo || gameState.CurrentCargo?.Inventory == null) return;

        foreach (var item in gameState.CurrentCargo.Inventory.OrderByDescending(i => i.Count))
        {
            cargoContent.Children.Add(new TextBlock
            {
                Text = $"{item.Name}: {item.Count}",
                Foreground = GetBodyBrush(),
                FontSize = 20
            });
        }
    }
    private void UpdateBackpackCard(StatusJson status, Card backpackCard)
    {
        backpackContent.Children.Clear();

        bool showBackpack = status != null && status.OnFoot; // <-- Use the direct property from StatusJson
        backpackCard.Visibility = showBackpack ? Visibility.Visible : Visibility.Collapsed;

        if (showBackpack && gameState.CurrentBackpack?.Inventory != null)
        {
            var grouped = gameState.CurrentBackpack.Inventory
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                backpackContent.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetBodyBrush(),
                    Margin = new Thickness(0, 10, 0, 4)
                });

                foreach (var item in group.OrderByDescending(i => i.Count))
                {
                    backpackContent.Children.Add(new TextBlock
                    {
                        Text = $"{item.Name_Localised ?? item.Name}: {item.Count}",
                        FontSize = 18,
                        Margin = new Thickness(10, 0, 0, 2),
                        Foreground = GetBodyBrush()
                    });
                }
            }
        }
    }


    private void UpdateModulesCard(StatusJson status, Card modulesCard)
    {
        modulesContent.Children.Clear();
        bool showModules = status.Flags.HasFlag(Flag.InMainShip) &&
                           !status.OnFoot &&
                           !status.Flags.HasFlag(Flag.InSRV) &&
                           !status.Flags.HasFlag(Flag.InFighter);

        if (gameState.CurrentLoadout?.Modules != null && showModules)
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
    }

    private void UpdateSummaryCard()
    {
        SetOrUpdateSummaryText("Commander", $"Commander: {gameState?.CommanderName ?? "(Unknown)"}");
        SetOrUpdateSummaryText("System", $"System: {gameState?.CurrentSystem ?? "(Unknown)"}");

        if (!string.IsNullOrEmpty(gameState?.ShipName) || !string.IsNullOrEmpty(gameState?.ShipLocalised))
        {
            var shipLabel = $"Ship: {gameState.UserShipName ?? gameState.ShipName}";
            if (!string.IsNullOrEmpty(gameState.UserShipId))
                shipLabel += $" [{gameState.UserShipId}]";
            shipLabel += $"\nType: {gameState.ShipLocalised}";
            SetOrUpdateSummaryText("Ship", shipLabel);
        }

        if (gameState.Balance.HasValue)
            SetOrUpdateSummaryText("Balance", $"Balance: {gameState.Balance.Value:N0} CR");

        if (!string.IsNullOrEmpty(gameState?.SquadronName))
            SetOrUpdateSummaryText("Squadron", $"Squadron: {gameState.SquadronName}");

        if (gameState?.JumpCountdown != null && gameState.JumpCountdown.Value.TotalSeconds > 0)
        {
            string countdownText = gameState.JumpCountdown.Value.ToString(@"mm\:ss");
            SetOrUpdateSummaryText("CarrierJumpCountdown", $"Carrier Jump In: {countdownText}");
        }

        if (gameState.IsOverheating)
            SetOrUpdateSummaryText("OverheatWarning", "WARNING: Ship Overheating!");
    }
    private void UpdateMaterialsCard()
    {
        fcMaterialsContent.Children.Clear();
        fcMaterialsContent.Visibility = gameState.CurrentMaterials?.Materials != null
            ? Visibility.Visible : Visibility.Collapsed;

        if (gameState.CurrentMaterials?.Materials == null) return;

        foreach (var item in gameState.CurrentMaterials.Materials.OrderByDescending(i => i.Count))
        {
            fcMaterialsContent.Children.Add(new TextBlock
            {
                Text = $"{item.Name_Localised ?? item.Name}: {item.Count}",
                FontSize = 18,
                Foreground = GetBodyBrush()
            });
        }
    }
    private void UpdateRouteCard()
    {
        routeContent.Children.Clear();
        routeContent.Visibility = gameState.CurrentRoute?.Route?.Any() == true
            ? Visibility.Visible : Visibility.Collapsed;

        if (gameState.CurrentRoute?.Route == null) return;

        foreach (var jump in gameState.CurrentRoute.Route)
        {
            routeContent.Children.Add(new TextBlock
            {
                Text = $"{jump.StarSystem} ({jump.StarClass})",
                FontSize = 24,
                Margin = new Thickness(8, 0, 0, 2),
                Foreground = GetBodyBrush()
            });
        }
    }
    private void UpdateFuelDisplay(StatusJson status)
    {
        if (status?.Fuel == null)
        {
            fuelBar.Visibility = Visibility.Collapsed;
            fuelText.Visibility = Visibility.Collapsed;
            return;
        }

        fuelBar.Visibility = Visibility.Visible;
        fuelText.Visibility = Visibility.Visible;

        double newValue = Math.Round(status.Fuel.FuelMain, 2);
        if (Math.Abs(lastFuelValue - newValue) > 0.01)
        {
            fuelBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
            fuelBar.Value = newValue;
            lastFuelValue = newValue;
        }

        fuelText.Text = $"Fuel: Main {newValue:0.00} / Reserve {status.Fuel.FuelReservoir:0.00}";
    }

    #endregion cards

    private Brush GetBodyBrush() => (Brush)System.Windows.Application.Current.Resources["MaterialDesignBody"];

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
        flagsPanel1 ??= new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        flagsPanel2 ??= new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };

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

        if (!summaryContent.Children.Contains(fuelStack))
            summaryContent.Children.Add(fuelStack);

        if (!summaryContent.Children.Contains(flagsPanel1))
            summaryContent.Children.Add(flagsPanel1);
        if (!summaryContent.Children.Contains(flagsPanel2))
            summaryContent.Children.Add(flagsPanel2);
    }

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

    private void SetOrUpdateSummaryText(string key, string content, int fontSize = 24, Brush? foreground = null)
    {
        var existing = summaryContent.Children
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Tag?.ToString() == key);

        if (existing != null)
        {
            existing.Text = content;
        }
        else
        {
            summaryContent.Children.Insert(0, new TextBlock
            {
                Text = content,
                FontSize = fontSize,
                Foreground = foreground ?? GetBodyBrush(),
                Tag = key
            });
        }
    }

    private void SetupDisplayUi()
    {
        InitializeCards();

        MainGrid.Children.Clear();
        MainGrid.ColumnDefinitions.Clear();
        cardMap.Clear();

        AddCard("Summary", summaryContent);
        AddCard("Cargo", cargoContent);
        AddCard("Backpack", backpackContent);
        AddCard("Fleet Carrier Materials", fcMaterialsContent);
        AddCard("Nav Route", routeContent);
        AddCard("Ship Modules", modulesContent);

        int maxColumns = cardMap.Count;
        for (int i = 0; i < maxColumns; i++)
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int index = 0;
        foreach (var card in cardMap.Values)
        {
            Grid.SetColumn(card, index++);
            MainGrid.Children.Add(card);
        }
    }

    private void AddCard(string title, StackPanel contentPanel)
    {
        var card = CreateCard(title, contentPanel);
        cardMap[title] = card;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var allScreens = Screen.AllScreens.ToList();
        screen = allScreens.FirstOrDefault(s => s.DeviceName == appSettings.SelectedScreenId);

        if (screen == null)
        {
            screen = await PromptUserToSelectScreenAsync(allScreens);
            if (screen == null) { System.Windows.Application.Current.Shutdown(); return; }

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

    #endregion Private Methods

    #region Public Classes

    public static class ProgressBarFix
    {
        #region Public Methods

        public static void SetValueInstantly(ProgressBar bar, double value)
        {
            bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
            bar.Value = value;
        }

        #endregion Public Methods
    }

    #endregion Public Classes
}