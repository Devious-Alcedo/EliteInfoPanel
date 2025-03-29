using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using MaterialDesignThemes.Wpf;
using WpfScreenHelper;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;
using System.Windows.Shapes;
using Serilog;
using EliteInfoPanel.Util;
using System.Windows.Data;

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
    { Flag.OverHeating, "🔥" },
    { Flag.LandingGearDown, "🛬" },
    { Flag.LightsOn, "💡" },
    { Flag.LowFuel, "⛽" }
};
    private Dictionary<string, Card> cardMap = new();
    private AppSettings appSettings = SettingsManager.Load();
    private StackPanel backpackContent;
    private StackPanel cargoContent;
    private StackPanel fcMaterialsContent;
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
    private Grid fuelBarGrid;
    private Rectangle fuelBarFilled;
    private Rectangle fuelBarEmpty;
    private WrapPanel flagsPanel1;
    private WrapPanel flagsPanel2;

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

            bool shouldShowPanels = status != null && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            MainGrid.Visibility = shouldShowPanels ? Visibility.Visible : Visibility.Collapsed;

            if (!shouldShowPanels) return;

            UpdateSummaryCard();
            UpdateMaterialsCard();
            UpdateRouteCard();
            UpdateFuelDisplay(status);
            UpdateFlagChips(status);
            UpdateCargoCard(status, cardMap["Cargo"]);
            UpdateBackpackCard(status, cardMap["Backpack"]);
            UpdateModulesCard(status, cardMap["Ship Modules"]);
            UpdateFlagsCard(status);
            // Call to dynamically rearrange the UI based on the visible cards
            RefreshCardsLayout();
        });
    }
    #region cards
    private void UpdateFlagChips(StatusJson? status)
    {
        if (status == null) return;

        Log.Debug("Active flags: {Flags}", status.Flags); // ← Add this here

        if (flagsPanel1 == null)
            flagsPanel1 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

        if (flagsPanel2 == null)
            flagsPanel2 = new WrapPanel { Orientation = Orientation.Horizontal };

        flagsPanel1.Children.Clear();
        flagsPanel2.Children.Clear();

        if (status == null) return;

        var activeFlags = Enum.GetValues(typeof(Flag))
     .Cast<Flag>()
     .Where(flag => status.Flags.HasFlag(flag))
     .ToList();

        for (int i = 0; i < activeFlags.Count; i++)
        {
            var chip = new Chip
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                {
                    new PackIcon
                    {
                        Kind = PackIconKind.CheckCircleOutline,
                        Width = 24,
                        Height = 24,
                        Margin = new Thickness(0, 0, 6, 0)
                    },
                    new TextBlock
                    {
                        Text = activeFlags[i].ToString(),
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                }
                },
                Margin = new Thickness(6),
                ToolTip = activeFlags[i].ToString(),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White
            };

            if (i < 5)
                flagsPanel1.Children.Add(chip);
            else
                flagsPanel2.Children.Add(chip);
        }
    }

    private void UpdateFlagsCard(StatusJson? status)
    {
        if (status == null) return;

        var flags1 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
        var flags2 = new WrapPanel { Orientation = Orientation.Horizontal };

        var activeFlags = Enum.GetValues(typeof(Flag))
            .Cast<Flag>()
            .Where(flag => status.Flags.HasFlag(flag))
            .ToList();

        for (int i = 0; i < activeFlags.Count; i++)
        {
            var chip = new Chip
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                {
                    new PackIcon
                    {
                        Kind = PackIconKind.CheckCircleOutline,
                        Width = 24,
                        Height = 24,
                        Margin = new Thickness(0, 0, 6, 0)
                    },
                    new TextBlock
                    {
                        Text = activeFlags[i].ToString(),
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                }
                },
                Margin = new Thickness(6),
                ToolTip = activeFlags[i].ToString(),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White
            };

            if (i < 5)
                flags1.Children.Add(chip);
            else
                flags2.Children.Add(chip);
        }

        var content = new StackPanel();
        content.Children.Add(flags1);
        content.Children.Add(flags2);

        if (!cardMap.ContainsKey("Status Flags"))
        {
            AddCard("Status Flags", content);
        }
        else
        {
            cardMap["Status Flags"].Content = new StackPanel
            {
                Children =
            {
                new TextBlock
                {
                    Text = "Status Flags",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                },
                content
            }
            };
        }
    }


    private void InitializeCards()
    {
        summaryContent ??= new StackPanel();
        cargoContent ??= new StackPanel();
        backpackContent ??= new StackPanel();
        fcMaterialsContent ??= new StackPanel();
        routeContent ??= new StackPanel();
        modulesContent ??= new StackPanel();
        fuelStack ??= new StackPanel();
        if (flagsPanel1 == null)
            flagsPanel1 ??= new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

        if (flagsPanel2 == null)
            flagsPanel2 ??= new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };



       
        if (fuelText == null)
        {
            fuelText = new TextBlock
            {
                Text = "Fuel:",
                Foreground = GetBodyBrush(),
                FontSize = 26
            };

            fuelBarFilled = new Rectangle
            {
                Fill = Brushes.Orange,
                RadiusX = 2,
                RadiusY = 2
            };

            fuelBarEmpty = new Rectangle
            {
                Fill = Brushes.DarkSlateGray,
                RadiusX = 2,
                RadiusY = 2
            };

            fuelBarGrid = new Grid
            {
                Height = 34,
                Margin = new Thickness(0, 4, 0, 0),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            fuelBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fuelBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });

            Grid.SetColumn(fuelBarFilled, 0);
            Grid.SetColumn(fuelBarEmpty, 1);

            fuelBarGrid.Children.Add(fuelBarFilled);
            fuelBarGrid.Children.Add(fuelBarEmpty);

            fuelStack.Children.Add(fuelText);
            fuelStack.Children.Add(fuelBarGrid);
        }


        if (!summaryContent.Children.Contains(fuelStack))
            summaryContent.Children.Add(fuelStack);
    }
    private ControlTemplate CreateNonAnimatedProgressBarTemplate()
    {
        var template = new ControlTemplate(typeof(ProgressBar));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        var backgroundFactory = new FrameworkElementFactory(typeof(Rectangle));
        backgroundFactory.SetValue(Rectangle.FillProperty, Brushes.DarkSlateGray);
        gridFactory.AppendChild(backgroundFactory);

        var progressFactory = new FrameworkElementFactory(typeof(Rectangle));
        progressFactory.Name = "PART_Track";
        progressFactory.SetValue(Rectangle.FillProperty, Brushes.Orange);
        progressFactory.SetValue(Rectangle.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        progressFactory.SetValue(Rectangle.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        progressFactory.SetBinding(Rectangle.WidthProperty, new System.Windows.Data.Binding("Value")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
            Converter = new ProgressToWidthConverter(),
            ConverterParameter = 32 // Max value — keep in sync
        });

        gridFactory.AppendChild(progressFactory);
        template.VisualTree = gridFactory;

        return template;
    }

    private Style CreateNonAnimatingProgressBarStyle()
    {
        Style style = new Style(typeof(ProgressBar), null);

        // Disable animation for the Value property completely
        var animationSetter = new Setter(ProgressBar.TemplateProperty, null);
        style.Setters.Add(animationSetter);

        return style;
    }
    private void RefreshCardsLayout()
    {
        var status = gameState.CurrentStatus;

        // Set visibility directly on cards
        cardMap["Summary"].Visibility = Visibility.Visible;
        cardMap["Cargo"].Visibility = (status.Flags.HasFlag(Flag.InSRV) || status.Flags.HasFlag(Flag.InMainShip))
            ? Visibility.Visible : Visibility.Collapsed;
        cardMap["Backpack"].Visibility = status.OnFoot ? Visibility.Visible : Visibility.Collapsed;
        cardMap["Fleet Carrier Materials"].Visibility = fcMaterialsContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        cardMap["Nav Route"].Visibility = routeContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        cardMap["Ship Modules"].Visibility = modulesContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Rearrange visible cards dynamically WITHOUT CLEARING THE GRID
        var visibleCards = cardMap.Values.Where(card => card.Visibility == Visibility.Visible).ToList();

        MainGrid.ColumnDefinitions.Clear();
        MainGrid.Children.Clear(); // Now we do this carefully and ONCE here.

        for (int i = 0; i < visibleCards.Count; i++)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(visibleCards[i], i);
            MainGrid.Children.Add(visibleCards[i]);
        }
    }

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
        var display = appSettings.DisplayOptions;
        if (display.ShowFuelLevel && status?.Fuel != null)
        {
            double value = Math.Round(status.Fuel.FuelMain, 2);
            double max = Math.Round(status.Fuel.FuelCapacity, 2); // ← USE journal-derived max

            if (max <= 0) return; // avoid divide by zero

            double ratio = Math.Min(1.0, value / max);
            lastFuelValue = value;

            fuelText.Text = $"Fuel: Main {value:0.00} / Reserve {status.Fuel.FuelReservoir:0.00}";

            // Update fuel bar width based on ratio
            fuelBarGrid.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
            fuelBarGrid.ColumnDefinitions[1].Width = new GridLength(1 - ratio, GridUnitType.Star);
        }
    }


    #endregion cards

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