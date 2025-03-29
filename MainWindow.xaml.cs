using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using MaterialDesignThemes.Wpf;
using WpfScreenHelper;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;
using static System.Net.Mime.MediaTypeNames;
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

    private StackPanel fuelStack;
    private TextBlock fuelText;
    private ProgressBar fuelBar;

    private StackPanel flagsPanel1;
    private StackPanel flagsPanel2;

    private double lastFuelValue = -1;

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

    private void GameState_DataUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            var display = appSettings.DisplayOptions;
            var status = gameState.CurrentStatus;
            var cargo = gameState.CurrentCargo;
            var backpack = gameState.CurrentBackpack;
            var materials = gameState.CurrentMaterials;
            var route = gameState.CurrentRoute;

            SetOrUpdateSummaryText("Commander", $"Commander: {gameState?.CommanderName ?? "(Unknown)"}");
            SetOrUpdateSummaryText("System", $"System: {gameState?.CurrentSystem ?? "(Unknown)"}");
            if (!string.IsNullOrEmpty(gameState?.ShipName) || !string.IsNullOrEmpty(gameState?.ShipLocalised))
            {
                var shipLabel = $"Ship: {gameState.UserShipName ?? gameState.ShipName} ";
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
                SetOrUpdateSummaryText("CarrierJumpCountdown", $"Carrier Jump In: {gameState.JumpCountdown.Value:mm\\:ss}");
            if (gameState.IsOverheating)
                SetOrUpdateSummaryText("OverheatWarning", "WARNING: Ship Overheating!");

            if (display.ShowFuelLevel && status?.Fuel != null)
            {
                double newValue = Math.Round(status.Fuel.FuelMain, 2);
                if (Math.Abs(lastFuelValue - newValue) > 0.01)
                {
                    fuelBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
                    fuelBar.Value = newValue;
                    lastFuelValue = newValue;
                }
                fuelText.Text = $"Fuel: Main {newValue:0.00} / Reserve {status.Fuel.FuelReservoir:0.00}";
            }

            flagsPanel1.Children.Clear();
            flagsPanel2.Children.Clear();

            void AddFlagIcon(bool condition, string emoji, string tooltip, StackPanel target)
            {
                target.Children.Add(new TextBlock
                {
                    Text = emoji,
                    FontSize = 24,
                    ToolTip = tooltip,
                    Margin = new Thickness(4, 0, 4, 0),
                    Foreground = condition ? Brushes.LightGreen : Brushes.Gray
                });
            }

            if (status != null && appSettings.DisplayOptions.VisibleFlags != null)
            {
                foreach (var flag in appSettings.DisplayOptions.VisibleFlags)
                {
                    if (FlagEmojiMap.TryGetValue(flag, out var emoji))
                    {
                        bool isActive = flag == Flag.OverHeating
                            ? gameState.IsOverheating
                            : status.Flags.HasFlag(flag);

                        // Grouping: first 4 flags in panel1, rest in panel2
                        var targetPanel = flagsPanel1.Children.Count < 4 ? flagsPanel1 : flagsPanel2;
                        AddFlagIcon(isActive, emoji, flag.ToString(), targetPanel);
                    }
                }
            }


            // Ship Modules
            modulesContent.Children.Clear();
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

            // Cargo
            cargoContent.Children.Clear();
            if (display.ShowCargo && cargo?.Inventory != null)
            {
                foreach (var item in cargo.Inventory.OrderByDescending(i => i.Count))
                {
                    cargoContent.Children.Add(new TextBlock
                    {
                        Text = $"{item.Name}: {item.Count}",
                        Foreground = GetBodyBrush(),
                        FontSize = 20
                    });
                }
            }

            // Backpack
            backpackContent.Children.Clear();
            if (display.ShowBackpack && backpack?.Inventory != null)
            {
                var grouped = backpack.Inventory.GroupBy(i => i.Category).OrderBy(g => g.Key);

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

            // Fleet Carrier Materials
            fcMaterialsContent.Children.Clear();
            if (display.ShowFCMaterials && materials?.Materials != null)
            {
                foreach (var item in materials.Materials.OrderByDescending(i => i.Count))
                {
                    fcMaterialsContent.Children.Add(new TextBlock
                    {
                        Text = $"{item.Name_Localised ?? item.Name}: {item.Count}",
                        FontSize = 18,
                        Foreground = GetBodyBrush()
                    });
                }
            }

            // Nav Route
            routeContent.Children.Clear();
            if (display.ShowRoute && route?.Route?.Any() == true)
            {
                foreach (var jump in route.Route)
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
        });
    }

    private Brush GetBodyBrush() => (Brush)System.Windows.Application.Current.Resources["MaterialDesignBody"];

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

    public static class ProgressBarFix
    {
        public static void SetValueInstantly(ProgressBar bar, double value)
        {
            bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
            bar.Value = value;
        }
    }
}