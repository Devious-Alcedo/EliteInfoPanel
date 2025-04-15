using System.Windows;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using EliteInfoPanel.Dialogs;
using WpfScreenHelper;
using System.Windows.Input;
using Serilog;

namespace EliteInfoPanel
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private Screen _currentScreen;

        public MainWindow()
        {
            InitializeComponent();

            // Configure logging
            LoggingConfig.Configure();

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
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the window on the specified screen
            var appSettings = SettingsManager.Load();
            var allScreens = Screen.AllScreens;

            _currentScreen = allScreens.FirstOrDefault(s =>
                s.DeviceName == appSettings.SelectedScreenId ||
                s.WpfBounds == appSettings.SelectedScreenBounds) ?? allScreens.FirstOrDefault();

            if (_currentScreen == null)
            {
                var dialog = new SelectScreenDialog(allScreens.ToList(), this);

                if (dialog.ShowDialog() == true && dialog.SelectedScreen != null)
                {
                    _currentScreen = dialog.SelectedScreen;
                    appSettings.SelectedScreenId = _currentScreen.DeviceName;
                    appSettings.SelectedScreenBounds = _currentScreen.WpfBounds;
                    SettingsManager.Save(appSettings);
                }
                else
                {
                    // Default to the first screen if none selected
                    _currentScreen = allScreens.FirstOrDefault();
                }
            }

            ApplyScreenBounds(_currentScreen);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                OpenCurrentLogFile();
            }
        }

        private void OpenCurrentLogFile()
        {
            try
            {
                string appDataFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
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
            catch (System.Exception ex)
            {
                MessageBox.Show($"Could not open log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyScreenBounds(Screen targetScreen)
        {
            WindowState = WindowState.Normal; // Force out of maximized state

            Left = targetScreen.WpfBounds.Left;
            Top = targetScreen.WpfBounds.Top;
            Width = targetScreen.WpfBounds.Width;
            Height = targetScreen.WpfBounds.Height;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Topmost = true;
        }

        private void OpenOptions()
        {
            var options = new OptionsWindow();
            options.ScreenChanged += screen =>
            {
                _currentScreen = screen;
                ApplyScreenBounds(screen);
            };

            options.ShowDialog();
        }
    }
}