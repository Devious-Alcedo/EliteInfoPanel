using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using EliteInfoPanel.ViewModels;
using EliteInfoPanel.Converters;
using WpfScreenHelper;
using System.Windows.Data;
using System.Collections.ObjectModel;
using EliteInfoPanel.Controls;
using System.Windows.Media;

namespace EliteInfoPanel.Dialogs
{
    public partial class OptionsWindow : Window
    {
        #region Private Fields

        private SettingsViewModel _viewModel;
        private bool _originalUseFloating;
        private double _originalFloatingScale;
        private double _originalFullscreenScale;
        private OrderableCheckBoxList _flagsControl;

        #endregion Private Fields

        #region Public Constructors

        public OptionsWindow()
        {
            InitializeComponent();

            // Configure logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("EliteInfoPanel.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            this.Loaded += (s, e) =>
            {
                // Simple solution - just copy all resources from the main window
                if (Application.Current.MainWindow != null)
                {
                    this.Resources.MergedDictionaries.Add(Application.Current.MainWindow.Resources);
                }
            };
            // Load settings
            var settings = SettingsManager.Load();
            _originalUseFloating = settings.UseFloatingWindow;
            _originalFloatingScale = settings.FloatingFontScale;
            _originalFullscreenScale = settings.FullscreenFontScale;

            settings.DisplayOptions ??= new DisplayOptions();
            settings.DisplayOptions.VisibleFlags ??= new List<Flag>();

            // Initialize font scales if they're 0
            if (settings.FullscreenFontScale <= 0)
                settings.FullscreenFontScale = 1.0;

            if (settings.FloatingFontScale <= 0)
                settings.FloatingFontScale = 1.0;

            Log.Information("Loaded settings: {@Settings}", settings);

            // Create view model
            _viewModel = new SettingsViewModel(settings);
            _viewModel.IsFloatingWindowMode = settings.UseFloatingWindow;
            DataContext = _viewModel;

            // Add log level options to viewmodel
            _viewModel.LogLevelOptions = new List<LogLevelOption>
            {
                new LogLevelOption { Value = LogLevel.Debug, Description = "Debug" },
                new LogLevelOption { Value = LogLevel.Information, Description = "Information" },
                new LogLevelOption { Value = LogLevel.Warning, Description = "Warning" },
                new LogLevelOption { Value = LogLevel.Error, Description = "Error" },
                new LogLevelOption { Value = LogLevel.Fatal, Description = "Fatal" }
            };

            // Connect commands
            _viewModel.SaveCommand = new RelayCommand(_ =>
            {
                var settings = _viewModel.AppSettings;
                bool windowModeChanged = _viewModel.IsFloatingWindowMode != _originalUseFloating;
                bool fontScaleChanged = (settings.FloatingFontScale != _originalFloatingScale) ||
                                        (settings.FullscreenFontScale != _originalFullscreenScale);

                settings.UseFloatingWindow = _viewModel.IsFloatingWindowMode;

                if (_flagsControl != null)
                {
                    settings.DisplayOptions.VisibleFlags = _flagsControl.GetSelectedFlags();
                    Log.Debug("Saving ordered flags: {FlagCount} flags", settings.DisplayOptions.VisibleFlags.Count);
                }

                _viewModel.SaveSettings();

                // Refresh MQTT settings if they changed
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                        {
                            await mainViewModel._gameState.RefreshMqttSettingsAsync();
                            Log.Information("MQTT settings refreshed after save");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error refreshing MQTT settings");
                    }
                });

                // Notify for changes requiring UI updates
                if (windowModeChanged)
                {
                    Log.Information("Window mode changed - notifying main window");
                    WindowModeChanged?.Invoke(_viewModel.IsFloatingWindowMode);
                }

                if (fontScaleChanged)
                {
                    Log.Information("Font scale changed - notifying main window");
                    FontSizeChanged?.Invoke();
                }

                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel2)
                {
                    Log.Information("Refreshing card visibility based on new settings");
                    mainViewModel2.RefreshLayout(true);
                }

                DialogResult = true;
                Close();
            });
            _viewModel.CancelCommand = new RelayCommand(_ =>
            {
                DialogResult = false;
                Close();
            });

            _viewModel.ChangeDisplayCommand = new RelayCommand(_ => ChangeDisplayButton_Click(null, null));

            // Subscribe to events
            _viewModel.ScreenChanged += screen => ScreenChanged?.Invoke(screen);
            _viewModel.FontSizeChanged += () => FontSizeChanged?.Invoke();

            // Position the window
            PositionWindowOnScreen(settings);

            // Load UI when window is shown
            Loaded += (s, e) =>
            {
                PopulateDisplayOptions();
                PopulateFlagOptions();
                PopulateWindowModeOptions();
                PopulateCardOptions();
                PopulateMqttOptions(); // Add this line
            };
        }

        #endregion Public Constructors

        #region Public Events

        public event Action<Screen> ScreenChanged;
        public event Action<bool> WindowModeChanged;
        public event Action FontSizeChanged;

        #endregion Public Events

        #region Public Properties

        public Screen SelectedNewScreen { get; private set; }
        public AppSettings Settings => _viewModel.AppSettings;

        #endregion Public Properties

        #region Private Methods
        private void PopulateMqttOptions()
        {
            try
            {
                // Handle password box binding manually since PasswordBox doesn't support data binding
                var passwordBox = FindName("MqttPasswordBox") as PasswordBox;
                if (passwordBox != null)
                {
                    // Set initial password
                    passwordBox.Password = _viewModel.MqttPassword ?? "";

                    // Handle password changes
                    passwordBox.PasswordChanged += (s, e) =>
                    {
                        _viewModel.MqttPassword = passwordBox.Password;
                    };
                }

                Log.Debug("MQTT options populated successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error populating MQTT options");
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Copy all application resources
            foreach (var key in Application.Current.Resources.Keys)
            {
                try
                {
                    this.Resources[key] = Application.Current.Resources[key];
                }
                catch { }
            }

            // Apply Elite theme to all text elements
            ApplyEliteThemeToAllElements();

            // Your existing population methods
            PopulateDisplayOptions();
            PopulateFlagOptions();
            PopulateWindowModeOptions();
            PopulateCardOptions();
            PopulateMqttOptions(); // Add this line
        }

        private void ApplyEliteThemeToAllElements()
        {
            // Get the Elite text color
            var eliteTextBrush = Application.Current.Resources["EliteHudText"] as Brush ?? Brushes.White;
            var eliteMainBrush = Application.Current.Resources["EliteHudMain"] as Brush ?? Brushes.Orange;

            // Apply to all text elements recursively
            ApplyThemeToElement(this, eliteTextBrush, eliteMainBrush);
        }

        private void ApplyThemeToElement(DependencyObject element, Brush textBrush, Brush accentBrush)
        {
            if (element == null) return;

            // Apply to different control types
            switch (element)
            {
                case TextBlock textBlock:
                    if (textBlock.Style == this.FindResource("CardTitleStyle"))
                        textBlock.Foreground = accentBrush;
                    else
                        textBlock.Foreground = textBrush;
                    break;

                case RadioButton radioButton:
                    radioButton.Foreground = textBrush;
                    break;

                case CheckBox checkBox:
                    checkBox.Foreground = textBrush;
                    break;

                case Button button:
                    if (button.Content is string buttonText && buttonText == "Change Display")
                        button.Foreground = textBrush;
                    break;

                case Label label:
                    label.Foreground = textBrush;
                    break;

                case GroupBox groupBox:
                    groupBox.Foreground = accentBrush;
                    break;

                case ContentPresenter presenter:
                    // ContentPresenter doesn't have ForegroundProperty, so we'll get its child TextBlock
                    if (presenter.Content is string)
                    {
                        // The presenter will render the text, so we'll handle it through its visual tree
                        // We'll let the recursion handle any TextBlocks inside it
                    }
                    break;
            }

            // Recursively apply to children
            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                ApplyThemeToElement(child, textBrush, accentBrush);
            }
        }
        // Override your PopulateWindowModeOptions to ensure text is visible
        private void PopulateWindowModeOptions()
        {
            if (FindName("WindowModePanel") is not StackPanel panel) return;

            var fullScreenRadio = new RadioButton
            {
                Content = "Full Screen Mode (on selected display)",
                IsChecked = !_viewModel.AppSettings.UseFloatingWindow,
                Margin = new Thickness(5),
                GroupName = "WindowMode",
                Foreground = new SolidColorBrush(Colors.White)
            };

            var floatingWindowRadio = new RadioButton
            {
                Content = "Floating Window Mode (movable and resizable)",
                IsChecked = _viewModel.AppSettings.UseFloatingWindow,
                Margin = new Thickness(5),
                GroupName = "WindowMode",
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Store the original event handlers
            fullScreenRadio.Checked += (s, e) =>
            {
                // Only update the property, nothing else
                _viewModel.IsFloatingWindowMode = false;
            };

            floatingWindowRadio.Checked += (s, e) =>
            {
                // Only update the property, nothing else
                _viewModel.IsFloatingWindowMode = true;
            };

            panel.Children.Add(fullScreenRadio);
            panel.Children.Add(floatingWindowRadio);

            // Create the "Always on Top" checkbox
            var floatingOptions = new StackPanel { Margin = new Thickness(24, 5, 0, 0) };

            var alwaysOnTopCheck = new CheckBox
            {
                Content = "Always on Top",
                IsChecked = _viewModel.AlwaysOnTop,
                Margin = new Thickness(0, 5, 0, 5),
                Foreground = new SolidColorBrush(Colors.White)
            };

            alwaysOnTopCheck.Checked += (s, e) => _viewModel.AlwaysOnTop = true;
            alwaysOnTopCheck.Unchecked += (s, e) => _viewModel.AlwaysOnTop = false;

            floatingOptions.Children.Add(alwaysOnTopCheck);

            // Bind the enabled state
            var enabledBinding = new Binding("IsFloatingWindowMode")
            {
                Source = _viewModel
            };

            floatingOptions.SetBinding(IsEnabledProperty, enabledBinding);

            panel.Children.Add(floatingOptions);
        }
        private void ApplyDarkThemeToElement(DependencyObject element)
        {
            if (element == null) return;

            if (element is CheckBox checkBox)
            {
                checkBox.Foreground = Brushes.White;
            }
            else if (element is RadioButton radioButton)
            {
                radioButton.Foreground = Brushes.White;
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.Foreground = Brushes.White;
            }
            else if (element is GroupBox groupBox)
            {
                groupBox.Foreground = Brushes.White;
                groupBox.Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
            }
            else if (element is TabItem tabItem)
            {
                tabItem.Foreground = Brushes.White;
            }

            // Recursively apply to child elements
            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                ApplyDarkThemeToElement(child as DependencyObject);
            }
        }
        private void ChangeDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            var screens = Screen.AllScreens.ToList();
            var dialog = new SelectScreenDialog(screens, this);

            if (dialog.ShowDialog() == true && dialog.SelectedScreen != null)
            {
                _viewModel.SelectScreen(dialog.SelectedScreen);
                Log.Information("User changed display to: {DeviceName}", dialog.SelectedScreen.DeviceName);
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (scalePercentageText != null && sender is Slider slider)
            {
                // ONLY update the percentage text
                scalePercentageText.Text = $"Scale: {slider.Value:P0}";

                // ONLY update the preview text size
                if (_viewModel != null)
                {
                    _viewModel.FontSizePreview = _viewModel.IsFloatingWindowMode
                        ? AppSettings.DEFAULT_FLOATING_BASE * slider.Value
                        : AppSettings.DEFAULT_FULLSCREEN_BASE * slider.Value;
                }
            }

            // DO NOT trigger any font size notifications or resource refreshes here
            // The actual font size update should only happen when OK is clicked
        }

        private void OnDisplayChangeRequested()
        {
            // This calls your existing method for changing displays
            ChangeDisplayButton_Click(null, null);
        }

        private void PopulateDisplayOptions()
        {
            if (FindName("DisplayOptionsPanel") is not StackPanel displayPanel) return;

            displayPanel.Children.Clear();

            var displayOptions = Settings.DisplayOptions;
            var panelOptions = new Dictionary<string, string>
            {
                { nameof(displayOptions.ShowCommanderName), "Commander Name" },
                { nameof(displayOptions.ShowFuelLevel), "Fuel Level" },
                { nameof(displayOptions.ShowCargo), "Cargo" },
                { nameof(displayOptions.ShowBackpack), "Backpack" },
                { nameof(displayOptions.ShowFCMaterials), "Fleet Carrier Materials" },
                { nameof(displayOptions.ShowRoute), "Navigation Route" }
            };
            var dynamicVisibilityOptions = new Dictionary<string, string>
            {
                { nameof(displayOptions.ShowWhenSupercruise), "Show Only in Supercruise" },
                { nameof(displayOptions.ShowWhenDocked), "Show Only When Docked" },
                { nameof(displayOptions.ShowWhenInSRV), "Show Only in SRV" },
                { nameof(displayOptions.ShowWhenOnFoot), "Show Only On Foot" },
                { nameof(displayOptions.ShowWhenInFighter), "Show Only In Fighter" }
            };

            foreach (var option in panelOptions)
            {
                var prop = typeof(DisplayOptions).GetProperty(option.Key);
                bool value = (bool)(prop?.GetValue(displayOptions) ?? false);

                Log.Debug("Loading DisplayOption {Option}: {Value}", option.Key, value);

                var checkbox = new CheckBox
                {
                    Content = option.Value,
                    IsChecked = value,
                    Margin = new Thickness(5),
                    Tag = option.Key
                };

                checkbox.Checked += (s, e) => prop?.SetValue(displayOptions, true);
                checkbox.Unchecked += (s, e) => prop?.SetValue(displayOptions, false);

                displayPanel.Children.Add(checkbox);
            }
        }

        private void PopulateFlagOptions()
        {
            var appSettings = _viewModel.AppSettings;

            // Create and configure the OrderableCheckBoxList control
            _flagsControl = new OrderableCheckBoxList();

            // Initialize with all flags and the selected ones
            var visibleFlags = appSettings.DisplayOptions.VisibleFlags ?? new List<Flag>();
            var allFlags = Enum.GetValues(typeof(Flag)).Cast<Flag>()
                 .Where(f => f != Flag.None)
                 .Concat(new[] { Flag.HudInCombatMode, Flag.Docking }) // Add synthetic flags manually
                 .Distinct(); // Ensure no duplicates

            Log.Information("Populating FlagOptions with {Count} flags", allFlags.Count());

            _flagsControl.InitializeFlags(allFlags, visibleFlags);

            // Add the control to the FlagOptionsPanel
            if (FlagOptionsPanel is Panel panel)
            {
                panel.Children.Clear();
                panel.Children.Add(_flagsControl);
            }
        }


        private void PositionWindowOnScreen(AppSettings settings)
        {
            // Instead of using logic to find the last screen, just use the primary screen
            var primaryScreen = WpfScreenHelper.Screen.PrimaryScreen;

            WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = primaryScreen.WpfBounds.Left + (primaryScreen.WpfBounds.Width - this.Width) / 2;
            this.Top = primaryScreen.WpfBounds.Top + (primaryScreen.WpfBounds.Height - this.Height) / 2;
        }
        private void PopulateCardOptions()
        {
            if (CardsOptionsPanel is not StackPanel panel) return;

            panel.Children.Clear();

            var appSettings = _viewModel.AppSettings;

            // Directly referencing top-level properties
            var cardMap = new Dictionary<string, string>
            {
                { nameof(appSettings.ShowSummary), "Summary" },
                { nameof(appSettings.ShowFlags), "Flags" },
                { nameof(appSettings.ShowCargo), "Cargo" },
                { nameof(appSettings.ShowBackpack), "Backpack" },
                { nameof(appSettings.ShowRoute), "Route" },
                { nameof(appSettings.ShowModules), "Modules" },
                { nameof(appSettings.ShowColonisation), "Colonisation" },
                { nameof(appSettings.ShowFleetCarrierCargoCard), "Fleet Carrier Cargo"   }
            };

            foreach (var entry in cardMap)
            {
                var prop = typeof(AppSettings).GetProperty(entry.Key);
                bool value = (bool)(prop?.GetValue(appSettings) ?? false);

                var checkbox = new CheckBox
                {
                    Content = entry.Value,
                    IsChecked = value,
                    Margin = new Thickness(5),
                    Tag = entry.Key
                };

                checkbox.Checked += (s, e) => prop?.SetValue(appSettings, true);
                checkbox.Unchecked += (s, e) => prop?.SetValue(appSettings, false);

                panel.Children.Add(checkbox);
            }
        }
        private void SaveSettings()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(handle);
            Settings.SelectedScreenId = currentScreen.DeviceName;

            // Update flag settings from the orderable control
            if (_flagsControl != null)
            {
                Settings.DisplayOptions.VisibleFlags = _flagsControl.GetSelectedFlags();
            }

            Log.Information("Saving settings: {@Settings}", Settings);
            _viewModel.SaveCommand.Execute(null);
        }

        #endregion Private Methods
    }
}