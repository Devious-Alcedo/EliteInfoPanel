using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteInfoPanel.Core;
using EliteInfoPanel.Dialogs;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using Serilog;
using WpfScreenHelper;

namespace EliteInfoPanel
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private Screen _currentScreen;
        private readonly AppSettings _appSettings;

        public MainWindow()
        {
            InitializeComponent();

            // Configure logging
            LoggingConfig.Configure(enableDebugLogging: false);

            // Load settings
            _appSettings = SettingsManager.Load();

            // Initialize default font scales if needed
            if (_appSettings.FullscreenFontScale <= 0)
                _appSettings.FullscreenFontScale = 1.0;

            if (_appSettings.FloatingFontScale <= 0)
                _appSettings.FloatingFontScale = 1.0;

            // Initialize the GameStateService
            var gamePath = EliteDangerousPaths.GetSavedGamesPath();
            var gameState = new GameStateService(gamePath);

            // Create and set ViewModel
            _viewModel = new MainViewModel(gameState);
            _viewModel.SetMainGrid(MainGrid);
            DataContext = _viewModel;

            // Connect OpenOptionsCommand to event handler
            _viewModel.OpenOptionsCommand = new RelayCommand(_ => OpenOptions());

            // Set up event handlers
            Loaded += Window_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveWindowPosition();
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply window mode settings
            ApplyWindowSettings();
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

                Log.Information("Applied floating window settings: {Left}x{Top} {Width}x{Height}",
                    Left, Top, Width, Height);
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

                Log.Information("Applied full-screen settings on screen: {Screen}",
                    _currentScreen?.DeviceName ?? "Unknown");
            }

            // ✅ Update font scaling for all cards
            double fontScale = _appSettings.UseFloatingWindow
                ? _appSettings.FloatingFontScale
                : _appSettings.FullscreenFontScale;

            double baseFontSize = _appSettings.UseFloatingWindow
                ? AppSettings.DEFAULT_FLOATING_BASE * fontScale
                : AppSettings.DEFAULT_FULLSCREEN_BASE * fontScale;

            foreach (var card in _viewModel.Cards)
            {
                card.FontSize = baseFontSize;
            }

            // Update font resources for dynamic styles
            UpdateFontResources();
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

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                OpenCurrentLogFile();
            }
            else if (e.Key == Key.Escape && _appSettings.UseFloatingWindow)
            {
                // Allow ESC to close in floating window mode
                Close();
            }
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
        private void FloatingTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Enable dragging of the window when user clicks and drags the title bar
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Minimize the window
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the application
            this.Close();
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
            Application.Current.Resources["BaseFontSize"] = baseFontSize;
            Application.Current.Resources["HeaderFontSize"] = headerFontSize;
            Application.Current.Resources["SmallFontSize"] = smallFontSize;

            Log.Debug("Updated font resources for {Mode} mode with scale {Scale}: Base={Base}, Header={Header}, Small={Small}",
                _appSettings.UseFloatingWindow ? "floating window" : "full screen",
                fontScale, baseFontSize, headerFontSize, smallFontSize);
        }

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

        private void OpenOptions()
        {
            var options = new OptionsWindow();

            options.ScreenChanged += screen =>
            {
                _currentScreen = screen;
                _appSettings.UseFloatingWindow = false;
                ApplyScreenBounds(screen);
            };

            options.WindowModeChanged += useFloatingWindow =>
            {
                _appSettings.UseFloatingWindow = useFloatingWindow;
                ApplyWindowSettings();
            };

            options.FontSizeChanged += () =>
            {
                UpdateFontResources();
                App.RefreshResources();
                InvalidateVisual();
                _viewModel.RefreshLayout();
                UpdateLayout();
            };

            // 🔁 Restart MainWindow if window mode changed
            options.RestartRequested += () =>
            {
                // Fetch the new setting from the OptionsWindow
                var updatedSettings = options.Settings;

                // Update our local _appSettings reference
                _appSettings.UseFloatingWindow = updatedSettings.UseFloatingWindow;
                _appSettings.AlwaysOnTop = updatedSettings.AlwaysOnTop;
                _appSettings.FullscreenFontScale = updatedSettings.FullscreenFontScale;
                _appSettings.FloatingFontScale = updatedSettings.FloatingFontScale;
                _appSettings.SelectedScreenId = updatedSettings.SelectedScreenId;

                // Apply and persist
                ApplyWindowSettings();
                SettingsManager.Save(_appSettings);
                Log.Information("✅ Saved updated window mode: UseFloatingWindow={Value}", _appSettings.UseFloatingWindow);

                // Restart window
                var newWindow = new MainWindow();
                newWindow.Show();
                this.Close();
            };



            options.FontSizeChanged += () =>
            {
                UpdateFontResources();             // 🟢 Rebuild font size resources
                App.RefreshResources();            // 🟢 Apply to app
                InvalidateVisual();                // 🟢 Force redraw
                _viewModel.RefreshLayout();        // 🟢 Notify all cards
                UpdateLayout();
            };

            options.ShowDialog();
        }


    }
}