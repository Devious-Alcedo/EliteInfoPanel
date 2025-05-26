using EliteInfoPanel.Controls;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WpfScreenHelper;


namespace EliteInfoPanel
{
    public partial class MainWindow : Window
    {
        #region Public Fields

        public readonly GameStateService _gameState;

        #endregion Public Fields

        #region Private Fields

        private readonly AppSettings _appSettings;
        private readonly MainViewModel _viewModel;
        private Screen _currentScreen;

        #endregion Private Fields

        #region Public Constructors

        public MainWindow()
        {
            InitializeComponent();
            // Configure logging
            LoggingConfig.Configure(enableDebugLogging: false);
            Log.Information("MainWindow: Getting EliteThemeManager instance...");
            var eliteTheme = EliteThemeManager.Instance;
            eliteTheme.ColorsChanged += (colors) =>
            {
                // Force refresh of all visual elements when colors change
                App.RefreshResources();
                InvalidateVisual();
            };
            Log.Information("MainWindow: Theme manager initialized");
            //============may need to comment these out===================
            //RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            //CompositionTarget.Rendering += (s, ev) =>
            //{
            //    System.Diagnostics.Debug.WriteLine("🔄 WPF redraw...");
            //};
            //============================================================

            

            // Load settings
            _appSettings = SettingsManager.Load();

            // Initialize default font scales if needed
            if (_appSettings.FullscreenFontScale <= 0)
                _appSettings.FullscreenFontScale = 1.0;

            if (_appSettings.FloatingFontScale <= 0)
                _appSettings.FloatingFontScale = 1.0;

            // Initialize the GameStateService
            var gamePath = EliteDangerousPaths.GetSavedGamesPath();
            _gameState = new GameStateService(gamePath);

            // Create and set ViewModel
            var settings = SettingsManager.Load();
            _viewModel = new MainViewModel(_gameState, settings.UseFloatingWindow);

            _viewModel.SetMainGrid(MainGrid);
            DataContext = _viewModel;
       
            // Connect OpenOptionsCommand to event handler
            _viewModel.OpenOptionsCommand = new RelayCommand(_ => OpenOptions());

            // Set up event handlers
            Loaded += Window_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Closing += MainWindow_Closing;
            _viewModel.ApplyWindowModeFromSettings();
        }

        #endregion Public Constructors

        #region Public Methods

        public void UpdateFontSizes()
        {
            double fontScale = _appSettings.UseFloatingWindow
                ? _appSettings.FloatingFontScale
                : _appSettings.FullscreenFontScale;

            double baseFontSize = _appSettings.UseFloatingWindow
                ? AppSettings.DEFAULT_FLOATING_BASE * fontScale
                : AppSettings.DEFAULT_FULLSCREEN_BASE * fontScale;

            Log.Debug("Updating font sizes with scale {Scale}, resulting in base size {BaseSize}",
                fontScale, baseFontSize);

            foreach (var card in _viewModel.Cards)
            {
                card.FontSize = baseFontSize;
            }

            // Update font resources for dynamic styles
            UpdateFontResources();

            // Force layout update
            _viewModel.RefreshLayout();
        }

        #endregion Public Methods

        #region Private Methods

        private void ApplyScreenBounds(Screen targetScreen)
        {
            WindowState = WindowState.Normal; // Force out of maximized state
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            Left = targetScreen.WpfBounds.Left;
            Top = targetScreen.WpfBounds.Top;
            Width = targetScreen.WpfBounds.Width;
            Height = targetScreen.WpfBounds.Height;

            Topmost = true;
            WindowState = WindowState.Maximized;
        }

        private void ApplyWindowSettings()
        {
            if (_appSettings.UseFloatingWindow)
            {
                // Apply floating window settings
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                Topmost = _appSettings.AlwaysOnTop;
                WindowState = WindowState.Normal;

                // Set size and position from saved settings
                Left = _appSettings.FloatingWindowLeft;
                Top = _appSettings.FloatingWindowTop;
                Width = _appSettings.FloatingWindowWidth;
                Height = _appSettings.FloatingWindowHeight;

                // Ensure window is within visible screen bounds
                EnsureWindowIsVisible();
                _viewModel.IsFullScreenMode = !_appSettings.UseFloatingWindow;
                Log.Information("Applied floating window settings: {Left}x{Top} {Width}x{Height}",
                    Left, Top, Width, Height);

                // Show the floating title bar
                //FloatingTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                // Position on the selected screen in full-screen mode
                var allScreens = Screen.AllScreens;

                _currentScreen = allScreens.FirstOrDefault(s =>
                    s.DeviceName == _appSettings.SelectedScreenId) ?? allScreens.FirstOrDefault();

                if (_currentScreen == null)
                {
                    var dialog = new SelectScreenDialog(allScreens.ToList(), this);

                    if (dialog.ShowDialog() == true && dialog.SelectedScreen != null)
                    {
                        _currentScreen = dialog.SelectedScreen;
                        _appSettings.UseFloatingWindow = false;
                        _appSettings.SelectedScreenId = _currentScreen.DeviceName;
                        _appSettings.SelectedScreenBounds = _currentScreen.WpfBounds;

                        SettingsManager.Save(_appSettings); // ✅ ensure saved
                    }
                    else
                    {
                        _currentScreen = allScreens.FirstOrDefault();
                    }
                }
                else
                {
                    // Even if screen was already known, save if switching to fullscreen
                    _appSettings.UseFloatingWindow = false;
                    SettingsManager.Save(_appSettings); // ✅ ensure saved
                }

                ApplyScreenBounds(_currentScreen);

                // Hide the floating title bar in fullscreen mode
                // FloatingTitleBar.Visibility = Visibility.Collapsed;

                Log.Information("Applied full-screen settings on screen: {Screen}",
                    _currentScreen?.DeviceName ?? "Unknown");
            }

            // ✅ Update font scaling for all cards
            UpdateFontSizes();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the application
            this.Close();
        }

        private void CloseButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                Serilog.Log.Information("CloseButton_Loaded → IsFullScreenMode = {Fullscreen}", vm.IsFullScreenMode);
            }
            else
            {
                Serilog.Log.Warning("CloseButton_Loaded: DataContext not set or incorrect type.");
            }
        }

        private void EnsureWindowIsVisible()
        {
            // Make sure the window isn't positioned off-screen
            var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
            var virtualScreenHeight = SystemParameters.VirtualScreenHeight;

            if (Left < 0) Left = 0;
            if (Top < 0) Top = 0;
            if (Left + Width > virtualScreenWidth)
                Left = Math.Max(0, virtualScreenWidth - Width);
            if (Top + Height > virtualScreenHeight)
                Top = Math.Max(0, virtualScreenHeight - Height);

            // Ensure minimum size
            Width = Math.Max(Width, 400);
            Height = Math.Max(Height, 300);
        }

        private void FloatingTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Enable dragging of the window when user clicks and drags the title bar
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveWindowPosition();
        }
       
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                Log.Information("F11 pressed, closing all windows and application.");

                // Save main window position before closing
                SaveWindowPosition();

                // Close all windows and shutdown the application
                CloseAllWindowsAndQuit();
            }
            else if (e.Key == Key.F12)
            {
                OpenCurrentLogFile();
            }
            else if (e.Key == Key.F2)
            {
                // Open settings
                if (_viewModel.OpenOptionsCommand != null && _viewModel.OpenOptionsCommand.CanExecute(null))
                {
                    _viewModel.OpenOptionsCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Escape && !_appSettings.UseFloatingWindow)
            {
                // In fullscreen mode, Escape switches to floating window mode
                _appSettings.UseFloatingWindow = true;
                SettingsManager.Save(_appSettings);
                ApplyWindowSettings();
                _viewModel.ApplyWindowModeFromSettings();
            }

            base.OnKeyDown(e);
        }

        private void CloseAllWindowsAndQuit()
        {
            try
            {
                // Get a list of all windows except the main window
                var windowsToClose = new List<Window>();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window != this) // Don't include the main window
                    {
                        windowsToClose.Add(window);
                    }
                }

                Log.Information("Found {Count} popup windows to close", windowsToClose.Count);

                // Close all popup windows first
                foreach (var window in windowsToClose)
                {
                    try
                    {
                        Log.Debug("Closing popup window: {Title}", window.Title ?? "Unknown");
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error closing popup window: {Title}", window.Title ?? "Unknown");
                    }
                }

                // Now shutdown the entire application
                // This will close the main window and terminate the application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during application shutdown");

                // Fallback: force shutdown
                try
                {
                    Application.Current.Shutdown();
                }
                catch (Exception shutdownEx)
                {
                    Log.Fatal(shutdownEx, "Failed to shutdown application gracefully");
                    Environment.Exit(1);
                }
            }
        }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Minimize the window
            this.WindowState = WindowState.Minimized;
        }

        private void OpenCurrentLogFile()
        {
            try
            {
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDirectory = System.IO.Path.Combine(appDataFolder, "EliteInfoPanel");

                if (!System.IO.Directory.Exists(logDirectory))
                {
                    MessageBox.Show("Log directory not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Find the most recently modified log file
                var latestLog = System.IO.Directory.GetFiles(logDirectory, "EliteInfoPanel_Log*.log")
                                         .OrderByDescending(System.IO.File.GetLastWriteTime)
                                         .FirstOrDefault();

                if (latestLog == null)
                {
                    MessageBox.Show("No log files found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = latestLog,
                    UseShellExecute = true
                });

                // Also open explorer to the Elite Dangerous saved games folder
                string gameSavePath = EliteDangerousPaths.GetSavedGamesPath();
                if (System.IO.Directory.Exists(gameSavePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

        private void OpenOptions()
        {
            // Store the current topmost state
            bool wasTopmost = this.Topmost;

            try
            {
                // Temporarily disable topmost behavior for the main window
                if (wasTopmost)
                {
                    this.Topmost = false;
                }

                var options = new OptionsWindow();

                // Set the main window as owner to ensure proper modal behavior
                options.Owner = this;

                // Ensure the options window appears on top
                options.Topmost = true;

                // Important: Set initial settings reference
                options.Settings.FloatingFontScale = _appSettings.FloatingFontScale;
                options.Settings.FullscreenFontScale = _appSettings.FullscreenFontScale;

                // Center on the primary screen or main window
                var allScreens = WpfScreenHelper.Screen.AllScreens;
                var primaryScreen = WpfScreenHelper.Screen.PrimaryScreen;
                if (primaryScreen != null)
                {
                    _appSettings.LastOptionsScreenId = primaryScreen.DeviceName;
                    SettingsManager.Save(_appSettings);
                }

                options.ScreenChanged += screen =>
                {
                    _currentScreen = screen;

                    if (!_appSettings.UseFloatingWindow)
                    {
                        ApplyScreenBounds(screen);
                    }
                };

                // This is the key handler for real-time updates
                options.FontSizeChanged += () =>
                {
                    _appSettings.FloatingFontScale = options.Settings.FloatingFontScale;
                    _appSettings.FullscreenFontScale = options.Settings.FullscreenFontScale;

                    UpdateFontResources();
                    App.RefreshResources();
                    InvalidateVisual();
                    _viewModel.RefreshLayout();
                    UpdateLayout();
                };

                // Show the dialog modally
                if (options.ShowDialog() == true)
                {
                    var updatedSettings = SettingsManager.Load();

                    bool modeChanged = updatedSettings.UseFloatingWindow != _appSettings.UseFloatingWindow;

                    _appSettings.UseFloatingWindow = updatedSettings.UseFloatingWindow;
                    _appSettings.FloatingFontScale = updatedSettings.FloatingFontScale;
                    _appSettings.FullscreenFontScale = updatedSettings.FullscreenFontScale;
                    _appSettings.AlwaysOnTop = updatedSettings.AlwaysOnTop;

                    if (modeChanged)
                    {
                        ApplyWindowSettings();
                        _viewModel.ApplyWindowModeFromSettings();
                    }
                    else
                    {
                        UpdateFontResources();
                        App.RefreshResources();
                        InvalidateVisual();
                        _viewModel.RefreshLayout();
                        UpdateLayout();
                    }
                }
            }
            finally
            {
                // Always restore the original topmost state
                if (wasTopmost)
                {
                    this.Topmost = true;
                }
            }
        }

        private void SaveWindowPosition()
        {
            if (_appSettings.UseFloatingWindow && WindowState == WindowState.Normal)
            {
                _appSettings.FloatingWindowLeft = Left;
                _appSettings.FloatingWindowTop = Top;
                _appSettings.FloatingWindowWidth = Width;
                _appSettings.FloatingWindowHeight = Height;
                SettingsManager.Save(_appSettings);

                Log.Information("Saved floating window position: {Left}x{Top} {Width}x{Height}",
                    Left, Top, Width, Height);
            }
        }


        private void SwitchToWindowedMode_Click(object sender, RoutedEventArgs e)
        {
            _appSettings.UseFloatingWindow = true;
            SettingsManager.Save(_appSettings);
            ApplyWindowSettings();
            _viewModel.ApplyWindowModeFromSettings();
        }

        private void TestCarrierJump_Click(object sender, RoutedEventArgs e)
        {
            _gameState.GetType().GetProperty("IsOnFleetCarrier")?.SetValue(_gameState, true);
            _gameState.GetType().GetProperty("FleetCarrierJumpInProgress")?.SetValue(_gameState, true);
            _gameState.GetType().GetProperty("CarrierJumpDestinationSystem")?.SetValue(_gameState, "Simulated System");
            _gameState.GetType().GetProperty("CarrierJumpDestinationBody")?.SetValue(_gameState, "Simulated Body");

            _gameState.GetType().GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(_gameState, new object[] { "ShowCarrierJumpOverlay" });
            Log.Information("Simulated carrier jump initiated.");
        }

        private void UpdateFontResources()
        {
            // Get the current font scale based on window mode
            double fontScale = _appSettings.UseFloatingWindow
                ? _appSettings.FloatingFontScale
                : _appSettings.FullscreenFontScale;

            // Apply the scale to the base font sizes
            double baseFontSize = _appSettings.UseFloatingWindow
                ? AppSettings.DEFAULT_FLOATING_BASE * fontScale
                : AppSettings.DEFAULT_FULLSCREEN_BASE * fontScale;

            double headerFontSize = _appSettings.UseFloatingWindow
                ? AppSettings.DEFAULT_FLOATING_HEADER * fontScale
                : AppSettings.DEFAULT_FULLSCREEN_HEADER * fontScale;

            double smallFontSize = _appSettings.UseFloatingWindow
                ? AppSettings.DEFAULT_FLOATING_SMALL * fontScale
                : AppSettings.DEFAULT_FULLSCREEN_SMALL * fontScale;

            // Update application resources
            if (Application.Current != null && Application.Current.Resources != null)
            {
                Application.Current.Resources["BaseFontSize"] = baseFontSize;
                Application.Current.Resources["HeaderFontSize"] = headerFontSize;
                Application.Current.Resources["SmallFontSize"] = smallFontSize;
            }

            // Update each card's font size directly
            if (_viewModel != null)
            {
                foreach (var card in _viewModel.Cards)
                {
                    card.FontSize = baseFontSize;
                }
            }

            Log.Debug("Updated font resources for {Mode} mode with scale {Scale}: Base={Base}, Header={Header}, Small={Small}",
                _appSettings.UseFloatingWindow ? "floating window" : "full screen",
                fontScale, baseFontSize, headerFontSize, smallFontSize);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply window mode settings
            ApplyWindowSettings();

            if (DataContext is MainViewModel vm)
            {
                // Setup HyperspaceOverlay
                if (HyperspaceOverlay != null)
                {
                    HyperspaceOverlay.ForceHidden();
                    HyperspaceOverlay.SetGameState(vm._gameState);

                    // Extra startup check
                    var startupTimer = new System.Threading.Timer((state) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (!vm._gameState.IsHyperspaceJumping)
                            {
                                HyperspaceOverlay.ForceHidden();
                                Log.Information("🔴 Startup timer verified hyperspace overlay is hidden");
                            }
                        });
                    }, null, 1000, Timeout.Infinite);
                }
                // In MainWindow.xaml.cs - Add this to the Window_Loaded method after setting up HyperspaceOverlay
                _gameState.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(GameStateService.IsHyperspaceJumping))
                    {
                        var isJumping = _gameState.IsHyperspaceJumping;
                        Log.Information("MainWindow detected IsHyperspaceJumping changed to {Value}", isJumping);

                        if (!isJumping)
                        {
                            // Force hide the overlay when not hyperspace jumping
                            Dispatcher.Invoke(() => {
                                HyperspaceOverlay.ForceHidden();
                                Log.Information("Forcing hyperspace overlay to hide");
                            });
                        }
                    }
                };
                // Setup CarrierJumpOverlay
                if (CarrierJumpOverlay != null)
                {
                    CarrierJumpOverlay.ForceHidden();
                    CarrierJumpOverlay.SetGameState(vm._gameState);

                    // First check: explicitly reset any stale jump state
                    vm._gameState.ResetFleetCarrierJumpState();

                    // Force an immediate check for carrier jump
                    Dispatcher.BeginInvoke(new Action(() => {
                        // Delay slightly to ensure all properties are properly initialized
                        CarrierJumpOverlay.UpdateVisibility();
                        Log.Information("Forced initial check of carrier jump overlay visibility");
                    }), DispatcherPriority.Background);

                    // Watch for ShowCarrierJumpOverlay changes
                    vm._gameState.PropertyChanged += (s, args) =>
                    {
                        if (args.PropertyName == nameof(GameStateService.ShowCarrierJumpOverlay))
                        {
                            var show = vm._gameState.ShowCarrierJumpOverlay;
                            Log.Information("MainWindow detected ShowCarrierJumpOverlay changed to {0}", show);

                            Dispatcher.Invoke(() => {
                                CarrierJumpOverlay.UpdateVisibility();
                            });
                        }
                    };
                }
            }
        }

        #endregion Private Methods

        // Make sure this method exists in MainWindow.xaml.cs:
        // Replace only the OpenOptions() method in MainWindow.xaml.cs with this:

        // Replace only the OpenOptions() method in MainWindow.xaml.cs with this:

        // Use this version of the OpenOptions() method in MainWindow.xaml.cs
    }
}