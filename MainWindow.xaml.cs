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

using System.Windows.Input;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Threading;
using EliteInfoPanel.Core.EliteInfoPanel.Core;
using System.Text.RegularExpressions;
using MaterialDesignColors.Recommended;


namespace EliteInfoPanel;

public partial class MainWindow : Window
{
    #region Private Fields

    private AppSettings appSettings = SettingsManager.Load();
    private StackPanel backpackContent;
    private Dictionary<string, Card> cardMap = new();
    private StackPanel cargoContent;
    //private StackPanel fcMaterialsContent;
    private int currentModulesPage = 0;
    private Grid hyperspaceOverlay;

    private WrapPanel flagsPanel1;
    private WrapPanel flagsPanel2;
    private ProgressBar fuelBar;
    private Rectangle fuelBarEmpty;
    private Rectangle fuelBarFilled;
    private Grid fuelBarGrid;
    private StackPanel fuelStack;
    private TextBlock fuelText;
    private GameStateService gameState;
    private double lastFuelValue = -1;
    private Grid loadingOverlay;
    private TextBlock loadingText;
    private Dictionary<string, string> moduleNameMap = new();
    private DispatcherTimer modulePageTimer;
    private StackPanel modulesContent;
    private StackPanel routeContent;
    private Screen screen;
    private StackPanel shipStatsContent;
    private StackPanel summaryContent;
    private Snackbar toastSnackbar;

    #endregion Private Fields

    #region Public Constructors

    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = this;
        LoggingConfig.Configure();
        Loaded += Window_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    #endregion Public Constructors

    #region Private Methods

    private void ApplyScreenBounds(Screen targetScreen)
    {
        this.WindowState = WindowState.Normal; // <--- force out of maximized state

        this.Left = targetScreen.WpfBounds.Left;
        this.Top = targetScreen.WpfBounds.Top;
        this.Width = targetScreen.WpfBounds.Width;
        this.Height = targetScreen.WpfBounds.Height;

        this.WindowStyle = WindowStyle.None;
        this.WindowState = WindowState.Maximized;
        this.Topmost = true;
    }


    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
   

    private string FormatDestinationName(DestinationInfo destination)
    {
        if (destination == null || string.IsNullOrWhiteSpace(destination.Name))
            return null;

        var name = destination.Name;
        if (name == "$EXT_PANEL_ColonisationBeacon_DeploymentSite;") 
        {
            name = "Colonisation beacon";
            return name;
        }
        if (Regex.IsMatch(name, @"\\b[A-Z0-9]{3}-[A-Z0-9]{3}\\b")) // matches FC ID
            return $"{name} (Carrier)";
        else if (Regex.IsMatch(name, @"Beacon|Port|Hub|Station|Ring", RegexOptions.IgnoreCase))
            return $"{name} (Station)";
        else
            return name;
    }
    // In GameStateService.cs
    // Make sure we're not setting IsHyperspaceJumping in multiple places
    // It should only be set in the StartJump event and reset in FSDJump or SupercruiseEntry

    // In MainWindow.xaml.cs
    private void UpdateOverlayVisibility()
    {
        var status = gameState.CurrentStatus;
        if (status == null) return;

        bool isHyperspaceJumping = gameState.IsHyperspaceJumping;

        // Determine if we should show the panels
        bool shouldShowPanels = !isHyperspaceJumping && (
            status.Flags.HasFlag(Flag.Docked) ||
            status.Flags.HasFlag(Flag.Supercruise) ||
            status.Flags.HasFlag(Flag.InSRV) ||
            status.OnFoot ||
            status.Flags.HasFlag(Flag.InFighter) ||
            status.Flags.HasFlag(Flag.InMainShip));

        // Update all card visibility
        foreach (var card in cardMap.Values)
        {
            card.Visibility = shouldShowPanels ? Visibility.Visible : Visibility.Collapsed;
        }

        // CRITICAL: Make sure only ONE overlay is visible at a time
        if (LoadingOverlay != null)
        {
            // Only show loading overlay when we're waiting for Elite (not in any game state)
            // AND we're not hyperspace jumping
            bool showLoadingOverlay = !shouldShowPanels && !isHyperspaceJumping;
            LoadingOverlay.Visibility = showLoadingOverlay ? Visibility.Visible : Visibility.Collapsed;
        }

        if (HyperspaceOverlay != null)
        {
            // Only show hyperspace overlay during actual hyperspace jumps
            HyperspaceOverlay.Visibility = isHyperspaceJumping ? Visibility.Visible : Visibility.Collapsed;

            // Update the jump information if we're in hyperspace
            if (isHyperspaceJumping)
            {
                UpdateHyperspaceJumpDisplay();
            }
        }
    }
    private void UpdateHyperspaceJumpDisplay()
    {
        if (HyperspaceOverlay == null || JumpDestinationText == null || StarClassText == null)
            return;

        // Set the destination system text
        if (!string.IsNullOrEmpty(gameState.HyperspaceDestination))
        {
            JumpDestinationText.Text = $"Jumping to {gameState.HyperspaceDestination}";

            if (!string.IsNullOrEmpty(gameState.HyperspaceStarClass))
            {
                StarClassText.Text = $"Star Class: {gameState.HyperspaceStarClass}";
                StarClassText.Visibility = Visibility.Visible;
            }
            else
            {
                StarClassText.Visibility = Visibility.Collapsed;
            }
        }
        else if (gameState.CurrentRoute?.Route?.FirstOrDefault() is NavRouteJson.NavRouteSystem nextSystem)
        {
            JumpDestinationText.Text = $"Jumping to {nextSystem.StarSystem}";
            StarClassText.Text = $"Star Class: {nextSystem.StarClass}";
            StarClassText.Visibility = Visibility.Visible;
        }
        else
        {
            JumpDestinationText.Text = "Hyperspace Jump in Progress...";
            StarClassText.Visibility = Visibility.Collapsed;
        }
    }

    private void GameState_DataUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            UpdateOverlayVisibility();
            var status = gameState.CurrentStatus;

            // Show carrier arrival toast
            if (gameState.FleetCarrierJumpArrived && !gameState.IsInHyperspace)
            {
                ShowToast("Fleet Carrier jump completed!");
                gameState.ResetFleetCarrierJumpFlag();
            }

            // Defer route complete toast until truly out of hyperspace
            if (gameState.RouteWasActive && gameState.RouteCompleted && !gameState.IsInHyperspace)
            {
                ShowToast("Route complete! You've arrived at your destination.");
                gameState.ResetRouteActivity();
            }

            // Null safety
            if (status == null)
            {
                Log.Warning("GameState.CurrentStatus was null during update cycle.");
                return;
            }

            // HUD Mode display
            var isAnalysis = gameState.CurrentStatus.Flags.HasFlag(Flag.HudInAnalysisMode);
            SetOrUpdateSummaryText(
                "HudMode",
                $"HUD Mode: {(isAnalysis ? "Analysis" : "Combat")}",
                foreground: isAnalysis ? Brushes.LimeGreen : Brushes.IndianRed,
                icon: isAnalysis ? PackIconKind.Microscope : PackIconKind.Crosshairs
            );

            // Should we show the UI panels?
            // Don't show panels during hyperspace jumps, but show during other conditions
            bool isHyperspaceJumping = gameState.IsHyperspaceJumping;

            // If in hyperspace jump, update the jump display
            if (isHyperspaceJumping)
            {
                UpdateHyperspaceJumpDisplay();
            }

            bool shouldShowPanels = status != null && !isHyperspaceJumping && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            MainGrid.Visibility = Visibility.Visible;

            // Toggle card visibility
            foreach (var card in cardMap.Values)
            {
                card.Visibility = shouldShowPanels ? Visibility.Visible : Visibility.Collapsed;
            }

            // Manage overlays: show only one at a time
            if (loadingOverlay != null)
            {
                // Only show loading overlay when not in a valid game state AND not hyperspace jumping
                loadingOverlay.Visibility = (!shouldShowPanels && !isHyperspaceJumping) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (hyperspaceOverlay != null)
            {
                // Show hyperspace overlay only when in hyperspace
                hyperspaceOverlay.Visibility = isHyperspaceJumping ? Visibility.Visible : Visibility.Collapsed;

                // Update hyperspace jump display if needed
                if (isHyperspaceJumping)
                {
                    UpdateHyperspaceJumpDisplay();
                }
            }

            // Stop here if nothing else should show
            if (!shouldShowPanels) return;

            // Update panels
            UpdateSummaryCard();
            UpdateRouteCard();
            UpdateFuelDisplay(status);
            UpdateFlagChips(status);
            UpdateCargoCard(status, cardMap["Cargo"]);
            UpdateBackpackCard(status, cardMap["Backpack"]);
            UpdateModulesCard(status, cardMap["Ship Modules"]);
            UpdateFlagsCard(status);

            // Final layout pass
            RefreshCardsLayout();
        });
    }


    private Brush GetBodyBrush() => (Brush)System.Windows.Application.Current.Resources["MaterialDesignBody"];

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            {
                OpenCurrentLogFile();

            }
        }
    }

    private void OpenCurrentLogFile()
    {
        try
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDirectory = System.IO.Path.Combine(appDataFolder, "EliteInfoPanel");

            if (!Directory.Exists(logDirectory))
            {
                MessageBox.Show("Log directory not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Find the most recently modified log file matching the pattern
            var latestLog = Directory.GetFiles(logDirectory, "EliteInfoPanel_Log*.log")
                                     .OrderByDescending(File.GetLastWriteTime)
                                     .FirstOrDefault();

            if (latestLog == null)
            {
                MessageBox.Show("No log files found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = latestLog,
                UseShellExecute = true
            });
            //also open explorer to the Game Save Files location
            string gameSavePath = EliteDangerousPaths.GetSavedGamesPath();
            if (Directory.Exists(gameSavePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = gameSavePath,
                    UseShellExecute = true
                });
            }

        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    #region cards
    private void AddCard(string title, StackPanel contentPanel)
    {
        var card = CreateCard(title, contentPanel);
        cardMap[title] = card;
    }

    private Card CreateCard(string title, UIElement content)
    {
        var parent = LogicalTreeHelper.GetParent(content) as Panel;
        parent?.Children.Remove(content);

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Orange,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(content);

        return new Card { Margin = new Thickness(5), Padding = new Thickness(5), Content = panel };
    }

    private async void FadeAndUpdateModules()
    {
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));

        modulesContent.BeginAnimation(OpacityProperty, fadeOut);

        await Task.Delay(400);

        currentModulesPage = (currentModulesPage + 1) % 2;
        UpdateModulesCard(gameState.CurrentStatus, cardMap["Ship Modules"]);

        modulesContent.BeginAnimation(OpacityProperty, fadeIn);
    }
    private void ShowToast(string message)
    {
        toastSnackbar?.MessageQueue?.Enqueue(message);
    }

    private void InitializeCards()
    {
        toastSnackbar = ToastHost;
        toastSnackbar.MessageQueue = ToastQueue;


        summaryContent ??= new StackPanel();
        cargoContent ??= new StackPanel();
        backpackContent ??= new StackPanel();
        //fcMaterialsContent ??= new StackPanel();
        routeContent ??= new StackPanel();
        modulesContent ??= new StackPanel { Name = "ModulesContent" };

        fuelStack ??= new StackPanel();
        if (flagsPanel1 == null)
            flagsPanel1 ??= new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

        if (flagsPanel2 == null)
            flagsPanel2 ??= new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };

        if (MainGrid.RowDefinitions.Count == 0)
            MainGrid.RowDefinitions.Add(new RowDefinition());

        if (MainGrid.ColumnDefinitions.Count == 0)
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition());

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
        if (loadingOverlay == null)
        {
            loadingText = new TextBlock
            {
                Text = "Waiting for Elite to Load...",
                FontSize = 64,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var spinner = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 160,
                Height = 16,
                Foreground = Brushes.DeepSkyBlue,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { loadingText, spinner }
            };

            loadingOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), // dark overlay
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Visible
            };

            loadingOverlay.Children.Add(stack);
            Grid.SetRowSpan(loadingOverlay, int.MaxValue);
            Grid.SetColumnSpan(loadingOverlay, int.MaxValue);
            MainGrid.Children.Add(loadingOverlay);

            Log.Debug("Added loading overlay with indeterminate progress bar");
        }
        if (hyperspaceOverlay == null)
        {
            var hyperspaceText = new TextBlock
            {
                Text = "Jumping...",
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var spinner = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 20,
                Width = 200,
                Foreground = Brushes.DeepSkyBlue,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { hyperspaceText, spinner }
            };

            hyperspaceOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 64)),
                Visibility = Visibility.Collapsed
            };

            hyperspaceOverlay.Children.Add(stack);

            Grid.SetRowSpan(hyperspaceOverlay, int.MaxValue);
            Grid.SetColumnSpan(hyperspaceOverlay, int.MaxValue);
            MainGrid.Children.Add(hyperspaceOverlay);
        }
        // Determine if Elite is already running
        var status = gameState?.CurrentStatus;
        bool isEliteRunning = status != null && (
            status.Flags.HasFlag(Flag.Docked) ||
            status.Flags.HasFlag(Flag.Supercruise) ||
            status.Flags.HasFlag(Flag.InSRV) ||
            status.OnFoot ||
            status.Flags.HasFlag(Flag.InFighter) ||
            status.Flags.HasFlag(Flag.InMainShip));

        if (loadingOverlay != null)
            loadingOverlay.Visibility = isEliteRunning ? Visibility.Collapsed : Visibility.Visible;

        if (hyperspaceOverlay != null)
            hyperspaceOverlay.Visibility = Visibility.Collapsed;


    }

    private void RefreshCardsLayout()
    {
        var status = gameState.CurrentStatus;
        if (status == null)
            return;

        // Set visibility directly on cards
        cardMap["Summary"].Visibility = Visibility.Visible;
        cardMap["Cargo"].Visibility = ((status.Flags.HasFlag(Flag.InSRV) || status.Flags.HasFlag(Flag.InMainShip))
                                 && (gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0)
                                 ? Visibility.Visible
                                 : Visibility.Collapsed;

        cardMap["Backpack"].Visibility = status.OnFoot ? Visibility.Visible : Visibility.Collapsed;
        cardMap["Nav Route"].Visibility = routeContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        cardMap["Ship Modules"].Visibility = modulesContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        // cardMap["Fleet Carrier Materials"].Visibility = Visibility.Collapsed; // fully removed now

        var visibleCards = cardMap.Values.Where(card => card.Visibility == Visibility.Visible).ToList();

        MainGrid.ColumnDefinitions.Clear();
        MainGrid.Children.Clear();

      


        int currentCol = 0;

        foreach (var card in visibleCards)
        {
            int colSpan = card == cardMap["Ship Modules"] ? 2 : 1;

            // Add as many column definitions as needed
            for (int i = 0; i < colSpan; i++)
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(card, currentCol);
            Grid.SetColumnSpan(card, colSpan);
            MainGrid.Children.Add(card);

            currentCol += colSpan;
        }
        if (loadingOverlay != null && !MainGrid.Children.Contains(loadingOverlay))
            MainGrid.Children.Add(loadingOverlay);

        if (hyperspaceOverlay != null && !MainGrid.Children.Contains(hyperspaceOverlay))
        {
            Panel.SetZIndex(hyperspaceOverlay, 101);
            MainGrid.Children.Add(hyperspaceOverlay);
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

    private void UpdateCargoCard(StatusJson status, Card cargoCard)
    {
        cargoContent.Children.Clear();

        // Only show if inventory exists and has at least one item
        bool showCargo = (gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;

        // Directly set the visibility based on actual cargo items
        cargoCard.Visibility = showCargo ? Visibility.Visible : Visibility.Collapsed;

        if (!showCargo)
            return;

        // Populate the cargo content
        foreach (var item in gameState.CurrentCargo.Inventory.OrderByDescending(i => i.Count))
        {
            cargoContent.Children.Add(new TextBlock
            {
                Text = $"{CommodityMapper.GetDisplayName(item.Name)}: {item.Count}",
                Foreground = GetBodyBrush(),
                FontSize = 20
            });
        }
    }


    private void UpdateFlagChips(StatusJson? status)
    {
        

        if (status == null) return;
  

        Log.Debug("Active flags: {Flags}", status.Flags);

        if (flagsPanel1 == null)
            flagsPanel1 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

        if (flagsPanel2 == null)
            flagsPanel2 = new WrapPanel { Orientation = Orientation.Horizontal };

        flagsPanel1.Children.Clear();
        flagsPanel2.Children.Clear();
     
        // Start with the real flags
        var activeFlags = Enum.GetValues(typeof(Flag))
            .Cast<Flag>()
            .Where(flag => status.Flags.HasFlag(flag))
            .ToList();

        // Inject synthetic HudInCombatMode flag
        activeFlags.Add(SyntheticFlags.HudInCombatMode);
       
        activeFlags.Add(SyntheticFlags.Docking);
        // Display chips
        for (int i = 0; i < activeFlags.Count; i++)
        {
            string displayText = activeFlags[i] switch
            {
                var f when f == SyntheticFlags.HudInCombatMode => "HudInCombatMode",
                var f when f == SyntheticFlags.Docking => "Docking",
                _ => activeFlags[i].ToString()
            };



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
                        Text = displayText,
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                }
                },
                Margin = new Thickness(6),
                ToolTip = displayText,
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
                        FontSize = 24,
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
            if (cardMap["Status Flags"].Content is StackPanel cardPanel)
            {
                cardPanel.Children.Clear();
                cardPanel.Children.Add(new TextBlock
                {
                    Text = "Status Flags",
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Orange,
                    FontSize = 24,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                cardPanel.Children.Add(content);
            }
        }
    }
    private void UpdateFuelDisplay(StatusJson status)
    {
        var display = appSettings.DisplayOptions;

        if (display.ShowFuelLevel && status != null)
        {
            if (status.Flags.HasFlag(Flag.InSRV) && status.SRV != null)
            {
                // SRV Fuel mode
                double value = Math.Round(status.SRV.Fuel, 2);
                lastFuelValue = value;

                fuelText.Text = $"SRV Fuel: {value:P0}";

                fuelBarGrid.ColumnDefinitions[0].Width = new GridLength(value, GridUnitType.Star);
                fuelBarGrid.ColumnDefinitions[1].Width = new GridLength(1 - value, GridUnitType.Star);
            }
            else if (status.Fuel != null)
            {
                // Standard ship fuel mode
                double value = Math.Round(status.Fuel.FuelMain, 2);
                double max = Math.Round(gameState.CurrentLoadout?.FuelCapacity?.Main ?? 0, 2);

                if (max <= 0) return; // avoid divide by zero

                double ratio = Math.Min(1.0, value / max);
                lastFuelValue = value;

                fuelText.Text = $"Fuel: Main {value:0.00} \nReserve {status.Fuel.FuelReservoir:0.00}";
                fuelBarGrid.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
                fuelBarGrid.ColumnDefinitions[1].Width = new GridLength(1 - ratio, GridUnitType.Star);
            }
        }
    }

    private void UpdateModulesCard(StatusJson status, Card modulesCard)
    {
        if (status == null || gameState.CurrentLoadout?.Modules == null)
            return;
        modulesContent.Children.Clear();
        bool showModules = status.Flags.HasFlag(Flag.InMainShip) &&
                           !status.OnFoot &&
                           !status.Flags.HasFlag(Flag.InSRV) &&
                           !status.Flags.HasFlag(Flag.InFighter);

        if (gameState.CurrentLoadout?.Modules == null || !showModules)
            return;

        // Categorize modules
        var modules = gameState.CurrentLoadout.Modules;
        modules = modules
    .Where(m =>
        !string.IsNullOrWhiteSpace(m.Item) &&
        !m.Item.StartsWith("Decal_", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.StartsWith("Nameplate_", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.StartsWith("PaintJob_", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.StartsWith("VoicePack_", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.Contains("spoiler", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.Contains("bumper", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.Contains("bobble", StringComparison.OrdinalIgnoreCase) &&
         !m.Item.Contains("weaponcustomisation", StringComparison.OrdinalIgnoreCase) &&
          !m.Item.Contains("enginecustomisation", StringComparison.OrdinalIgnoreCase) &&
        !m.Item.Contains("wings", StringComparison.OrdinalIgnoreCase)
    )
    .ToList();
        var hardpoints = modules.Where(m => m.Slot.StartsWith("SmallHardpoint") || m.Slot.StartsWith("MediumHardpoint") || m.Slot.StartsWith("LargeHardpoint") || m.Slot.StartsWith("TinyHardpoint")).ToList();
        var coreInternals = modules.Where(m => m.Slot is "PowerPlant" or "MainEngines" or "FrameShiftDrive" or "LifeSupport" or "PowerDistributor" or "Radar" or "FuelTank").ToList();
        var optionals = modules.Where(m => m.Slot.StartsWith("Slot")).ToList();
        var other = modules.Except(hardpoints).Except(coreInternals).Except(optionals).ToList();

        var groupedPages = new List<List<LoadoutModule>>
    {
        hardpoints.Concat(coreInternals).ToList(), // Page 1
        optionals.Concat(other).ToList()           // Page 2
    };

        var pageModules = groupedPages[currentModulesPage];

        // 2-column display
        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftPanel = new StackPanel();
        var rightPanel = new StackPanel();

        Grid.SetColumn(leftPanel, 0);
        Grid.SetColumn(rightPanel, 1);
        outerGrid.Children.Add(leftPanel);
        outerGrid.Children.Add(rightPanel);

        // Alternate modules into left/right
        for (int i = 0; i < pageModules.Count; i++)
        {
            var module = pageModules[i];
            string rawName = module.ItemLocalised ?? module.Item;
            string displayName = ModuleNameMapper.GetFriendlyName(rawName);

            var tb = new TextBlock
            {
                Text = $"{displayName} ({module.Health:P0})",
                FontSize = 18,
                Margin = new Thickness(4),
                Foreground = new SolidColorBrush(
                    module.Health < 0.7 ? Colors.Red :
                    module.Health <= 0.95 ? Colors.Orange :
                    Colors.White),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 280,
                ToolTip = displayName
            };

            if (i % 2 == 0)
                leftPanel.Children.Add(tb);
            else
                rightPanel.Children.Add(tb);
        }

        modulesContent.Children.Add(outerGrid);

    }



    private void UpdateRouteCard()
    {
        routeContent.Children.Clear();

        var hasRoute = gameState.CurrentRoute?.Route?.Any() == true;
        var hasDestination = !string.IsNullOrWhiteSpace(gameState.CurrentStatus?.Destination?.Name);
        if (gameState.RouteWasActive && gameState.RouteCompleted)
        {
            // Wait until fully out of hyperspace before showing the toast
            if (!gameState.IsInHyperspace)
            {
                ShowToast("Route complete! You've arrived at your destination.");
                gameState.ResetRouteActivity();
            }
        }



        routeContent.Visibility = hasRoute || hasDestination
            ? Visibility.Visible : Visibility.Collapsed;
        bool isTargetInSameSystem = string.Equals(gameState.CurrentSystem, gameState.LastFsdTargetSystem, StringComparison.OrdinalIgnoreCase);

        if (gameState.RemainingJumps.HasValue && !isTargetInSameSystem)
        {
            routeContent.Children.Add(new TextBlock
            {
                Text = $"Jumps Remaining: {gameState.RemainingJumps.Value}",
                FontSize = 20,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = GetBodyBrush()
            });
        }


        // Show formatted destination (station/carrier)
        if (hasDestination)
        {
            string destination = gameState.CurrentStatus.Destination?.Name;
            string lastRouteSystem = gameState.CurrentRoute?.Route?.LastOrDefault()?.StarSystem;

            if (!string.Equals(destination, lastRouteSystem, StringComparison.OrdinalIgnoreCase))
            {
               
                routeContent.Children.Add(new TextBlock
                {
                    Text = $"Target: {FormatDestinationName(gameState.CurrentStatus.Destination)}",
                    FontSize = 20,
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = GetBodyBrush()
                });
            }

            // Show plotted route
            if (hasRoute)
            {
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
        }
    }
    

    private void UpdateSummaryCard()
    {
        SetOrUpdateSummaryText("Commander", $"Commander: {gameState?.CommanderName ?? "(Unknown)"}");
        SetOrUpdateSummaryText("System", $"System: {gameState?.CurrentSystem ?? "(Unknown)"}");

        if (!string.IsNullOrEmpty(gameState?.UserShipName) || !string.IsNullOrEmpty(gameState?.CurrentLoadout?.Ship))
        {
            var shipLabel = $"Ship: {gameState.UserShipName ?? gameState.CurrentLoadout?.Ship}";
            if (!string.IsNullOrEmpty(gameState.UserShipId))
                shipLabel += $" [{gameState.UserShipId}]";
            shipLabel += $"\nType: {ShipNameHelper.GetLocalisedName(gameState.CurrentLoadout?.Ship)}";
            SetOrUpdateSummaryText("Ship", shipLabel);
        }


        if (gameState.Balance.HasValue)
            SetOrUpdateSummaryText("Balance", $"Balance: {gameState.Balance.Value:N0} CR");

        if (!string.IsNullOrEmpty(gameState?.SquadronName))
            SetOrUpdateSummaryText("Squadron", $"Squadron: {gameState.SquadronName}");

        if (gameState?.JumpCountdown is TimeSpan countdown && countdown.TotalSeconds > 0)
        {
            string countdownText = countdown.ToString(@"mm\:ss");
            // need to increase font and change color
            SetOrUpdateSummaryText("CarrierJumpTarget",$"System: {gameState.CarrierJumpDestinationSystem} \nBody{gameState.CarrierJumpDestinationBody}", 20, Brushes.Gold);
            SetOrUpdateSummaryText("CarrierJumpCountdown", $"Carrier Jump In: {countdownText}",30,Brushes.Gold);
        }
        else
        {
            SetOrUpdateSummaryText("CarrierJumpCountdown", "");
            SetOrUpdateSummaryText("CarrierJumpTarget", "");
        }


    }

    //private void UpdateMaterialsCard()
    //{
    //    fcMaterialsContent.Children.Clear();
    //    fcMaterialsContent.Visibility = gameState.CurrentMaterials?.Materials != null
    //        ? Visibility.Visible : Visibility.Collapsed;

    //    if (gameState.CurrentMaterials?.Materials == null) return;

    //    foreach (var item in gameState.CurrentMaterials.Materials.OrderByDescending(i => i.Count))
    //    {
    //        fcMaterialsContent.Children.Add(new TextBlock
    //        {
    //            Text = $"{item.Name_Localised ?? item.Name}: {item.Count}",
    //            FontSize = 18,
    //            Foreground = GetBodyBrush()
    //        });
    //    }
    //}
    #endregion cards
    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new OptionsWindow { Owner = this };

        options.ScreenChanged += newScreen =>
        {
            appSettings.SelectedScreenId = newScreen.DeviceName;
            appSettings.SelectedScreenBounds = newScreen.WpfBounds;
            SettingsManager.Save(appSettings);

            
            screen = newScreen;
            ApplyScreenBounds(newScreen);
            SettingsManager.Save(appSettings);
        };

        if (options.ShowDialog() == true)
        {
            appSettings = SettingsManager.Load();
            SetupDisplayUi();
            GameState_DataUpdated();
        }
    }



    private Task<Screen?> PromptUserToSelectScreenAsync(List<Screen> screens)
    {
        var dialog = new SelectScreenDialog(screens, this);

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.SelectedScreen : null);
    }

    private void SetOrUpdateSummaryText(string key, string content, int fontSize = 24, Brush? foreground = null, PackIconKind? icon = null)
    {
        var existing = summaryContent.Children
            .OfType<StackPanel>()
            .FirstOrDefault(sp => sp.Tag?.ToString() == key);
        if (string.IsNullOrWhiteSpace(content))
        {
            if (existing != null)
            {
                summaryContent.Children.Remove(existing);
            }
            return;
        }
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Tag = key
        };

        if (icon != null)
        {
            stack.Children.Add(new PackIcon
            {
                Kind = icon.Value,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = foreground ?? GetBodyBrush()
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = content,
            FontSize = fontSize,
            Foreground = foreground ?? GetBodyBrush(),
            VerticalAlignment = VerticalAlignment.Center
        });

        if (existing != null)
        {
            int index = summaryContent.Children.IndexOf(existing);
            summaryContent.Children.RemoveAt(index);
            summaryContent.Children.Insert(index, stack);
        }
        else
        {
            // Maintain insertion order: insert before the fuelStack if it's present
            var fuelIndex = summaryContent.Children.IndexOf(fuelStack);
            if (fuelIndex > 0)
                summaryContent.Children.Insert(fuelIndex, stack);
            else
                summaryContent.Children.Add(stack);
        }
    }


    private void SetupDisplayUi()
    {
        InitializeCards();

        var preserveLoadingOverlay = loadingOverlay;
        MainGrid.Children.Clear();
        if (preserveLoadingOverlay != null && !MainGrid.Children.Contains(preserveLoadingOverlay))
            MainGrid.Children.Add(preserveLoadingOverlay);

        MainGrid.ColumnDefinitions.Clear();
        cardMap.Clear();

        AddCard("Summary", summaryContent);
        AddCard("Cargo", cargoContent);
        AddCard("Backpack", backpackContent);
        //AddCard("Fleet Carrier Materials", fcMaterialsContent);
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

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var allScreens = Screen.AllScreens.ToList();
        screen = Screen.AllScreens.FirstOrDefault(s =>
      s.DeviceName == appSettings.SelectedScreenId ||
      s.WpfBounds == appSettings.SelectedScreenBounds)
      ?? Screen.AllScreens.FirstOrDefault();



        if (screen == null)
        {
            screen = await PromptUserToSelectScreenAsync(allScreens);
            if (screen == null) { System.Windows.Application.Current.Shutdown(); return; }

            appSettings.SelectedScreenId = screen.DeviceName;
            SettingsManager.Save(appSettings);
        }
        string mapPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModuleNameMap.json");
        if (File.Exists(mapPath))
        {
            string json = File.ReadAllText(mapPath);
            moduleNameMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }

        ApplyScreenBounds(screen);
        SetupDisplayUi();

        modulePageTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        modulePageTimer.Tick += (s, e) => FadeAndUpdateModules();

        modulePageTimer.Start();

        var rotate = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(1.2)),
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };

        string gamePath = EliteDangerousPaths.GetSavedGamesPath();

        // 🔧 Create gameState BEFORE using it
        gameState = new GameStateService(gamePath);
        gameState.DataUpdated += GameState_DataUpdated;
        gameState.HyperspaceJumping += (jumping, systemName) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (hyperspaceOverlay != null)
                {
                    hyperspaceOverlay.Visibility = jumping ? Visibility.Visible : Visibility.Collapsed;

                    if (hyperspaceOverlay.Children[0] is StackPanel stack &&
                        stack.Children[0] is TextBlock text)
                    {
                        text.Text = jumping
                            ? $"Jumping to {systemName}..."
                            : "";
                    }
                }
            });
        };


        GameState_DataUpdated();
        UpdateOverlayVisibility();

    }

    #endregion Private Methods
    public SnackbarMessageQueue ToastQueue { get; } = new(TimeSpan.FromSeconds(3));


}
