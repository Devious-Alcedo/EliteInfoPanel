using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;
using WpfScreenHelper;

namespace EliteInfoPanel.ViewModels
{
    public class QosLevelOption
    {
        #region Public Properties

        public string Description { get; set; }
        public int Value { get; set; }

        #endregion Public Properties
    }

    public class SettingsViewModel : ViewModelBase
    {

        #region Private Fields

        private AppSettings _appSettings;
        private int _fontSize = 14;
        private double _fontSizePreview = 14;

        #endregion Private Fields

        #region Public Constructors

        public SettingsViewModel(AppSettings settings)
        {
            _appSettings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Initialize default font scales if they're 0
            if (_appSettings.FullscreenFontScale <= 0)
                _appSettings.FullscreenFontScale = 1.0;

            if (_appSettings.FloatingFontScale <= 0)
                _appSettings.FloatingFontScale = 1.0;

            // Initialize preview font size
            UpdateFontSizePreview();

            // Initialize commands
            SaveCommand = new RelayCommand(_ => SaveSettings());
            CancelCommand = new RelayCommand(_ => { /* Will be handled by view */ });
            ChangeDisplayCommand = new RelayCommand(_ => RequestDisplayChange());
            TestMqttConnectionCommand = new RelayCommand(async _ => await TestMqttConnection());
            // Add more dynamically if needed

        }

        #endregion Public Constructors

        #region Public Events

        public event Action DisplayChangeRequested;

        // Event for font size change
        public event Action FontSizeChanged;

        // Event for screen change notification
        public event Action<Screen> ScreenChanged;

        #endregion Public Events

        #region Public Properties
        public RelayCommand TestMqttConnectionCommand { get; }
        public bool AlwaysOnTop
        {
            get => _appSettings.AlwaysOnTop;
            set
            {
                if (_appSettings.AlwaysOnTop != value)
                {
                    _appSettings.AlwaysOnTop = value;
                    OnPropertyChanged();
                }
            }
        }

        public AppSettings AppSettings => _appSettings;

        public ObservableCollection<CardViewModel> AvailableCards { get; } = new();

        public RelayCommand CancelCommand { get; set; }

        public RelayCommand ChangeDisplayCommand { get; set; }

        public double CurrentFontScale
        {
            get => IsFloatingWindowMode ? FloatingFontScale : FullscreenFontScale;
            set
            {
                if (IsFloatingWindowMode)
                {
                    FloatingFontScale = value;
                }
                else
                {
                    FullscreenFontScale = value;
                }

                // Also notify that the scale percentage text has changed
                OnPropertyChanged(nameof(ScalePercentage));
            }
        }

        public bool DevelopmentMode
        {
            get => _appSettings.DevelopmentMode;
            set
            {
                if (_appSettings.DevelopmentMode != value)
                {
                    _appSettings.DevelopmentMode = value;
                    OnPropertyChanged();
                }
            }
        }
        public double FloatingFontScale
        {
            get => _appSettings.FloatingFontScale;
            set
            {
                if (_appSettings.FloatingFontScale != value)
                {
                    _appSettings.FloatingFontScale = value;
                    OnPropertyChanged();

                    // Update preview 
                    UpdateFontSizePreview();

                    // Notify immediately for real-time updates
                    if (_appSettings.UseFloatingWindow)
                    {
                        FontSizeChanged?.Invoke();
                    }
                }
            }
        }

        public double FontScaleStep => 0.05;

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public double FontSizePreview
        {
            get => _fontSizePreview;
            set => SetProperty(ref _fontSizePreview, value);
        }

        public double FullscreenFontScale
        {
            get => _appSettings.FullscreenFontScale;
            set
            {
                if (_appSettings.FullscreenFontScale != value)
                {
                    _appSettings.FullscreenFontScale = value;
                    OnPropertyChanged();

                    // Update preview
                    UpdateFontSizePreview();

                    // Notify immediately for real-time updates
                    if (!_appSettings.UseFloatingWindow)
                    {
                        FontSizeChanged?.Invoke();
                    }
                }
            }
        }

        public bool IsFloatingWindowMode
        {
            get => _appSettings.UseFloatingWindow;
            set
            {
                if (_appSettings.UseFloatingWindow != value)
                {
                    _appSettings.UseFloatingWindow = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFullScreenMode));
                    OnPropertyChanged(nameof(CurrentFontScale));
                    UpdateFontSizePreview();
                }
            }
        }

        // Window mode properties
        public bool IsFullScreenMode
        {
            get => !_appSettings.UseFloatingWindow;
            set
            {
                if (_appSettings.UseFloatingWindow == value)
                {
                    _appSettings.UseFloatingWindow = !value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFloatingWindowMode));
                    OnPropertyChanged(nameof(CurrentFontScale));
                    UpdateFontSizePreview();
                }
            }
        }

        public double MaxFontScale => 1.5;

        // Font scale bounds
        public double MinFontScale => 0.7;

        public string MqttBrokerHost
        {
            get => _appSettings.MqttBrokerHost;
            set
            {
                if (_appSettings.MqttBrokerHost != value)
                {
                    _appSettings.MqttBrokerHost = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MqttBrokerPort
        {
            get => _appSettings.MqttBrokerPort;
            set
            {
                if (_appSettings.MqttBrokerPort != value)
                {
                    _appSettings.MqttBrokerPort = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MqttClientId
        {
            get => _appSettings.MqttClientId;
            set
            {
                if (_appSettings.MqttClientId != value)
                {
                    _appSettings.MqttClientId = value;
                    OnPropertyChanged();
                }
            }
        }

        public System.Windows.Visibility MqttConfigurationVisibility =>
            MqttEnabled ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public bool MqttEnabled
        {
            get => _appSettings.MqttEnabled;
            set
            {
                if (_appSettings.MqttEnabled != value)
                {
                    _appSettings.MqttEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MqttConfigurationVisibility));
                }
            }
        }

        public string MqttPassword
        {
            get => _appSettings.MqttPassword;
            set
            {
                if (_appSettings.MqttPassword != value)
                {
                    _appSettings.MqttPassword = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MqttPublishIntervalMs
        {
            get => _appSettings.MqttPublishIntervalMs;
            set
            {
                if (_appSettings.MqttPublishIntervalMs != value)
                {
                    _appSettings.MqttPublishIntervalMs = Math.Max(100, value); // Minimum 100ms
                    OnPropertyChanged();
                }
            }
        }

        public bool MqttPublishOnlyChanges
        {
            get => _appSettings.MqttPublishOnlyChanges;
            set
            {
                if (_appSettings.MqttPublishOnlyChanges != value)
                {
                    _appSettings.MqttPublishOnlyChanges = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MqttQosLevel
        {
            get => _appSettings.MqttQosLevel;
            set
            {
                if (_appSettings.MqttQosLevel != value)
                {
                    _appSettings.MqttQosLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        // QoS level options for ComboBox
        public List<QosLevelOption> MqttQosLevelOptions { get; } = new List<QosLevelOption>
{
            new QosLevelOption { Value = 0, Description = "At most once (0)" },
            new QosLevelOption { Value = 1, Description = "At least once (1)" },
            new QosLevelOption { Value = 2, Description = "Exactly once (2)" }
};

        public bool MqttRetainMessages
        {
            get => _appSettings.MqttRetainMessages;
            set
            {
                if (_appSettings.MqttRetainMessages != value)
                {
                    _appSettings.MqttRetainMessages = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MqttTopicPrefix
        {
            get => _appSettings.MqttTopicPrefix;
            set
            {
                if (_appSettings.MqttTopicPrefix != value)
                {
                    _appSettings.MqttTopicPrefix = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MqttUsername
        {
            get => _appSettings.MqttUsername;
            set
            {
                if (_appSettings.MqttUsername != value)
                {
                    _appSettings.MqttUsername = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MqttUseTls
        {
            get => _appSettings.MqttUseTls;
            set
            {
                if (_appSettings.MqttUseTls != value)
                {
                    _appSettings.MqttUseTls = value;
                    OnPropertyChanged();
                }
            }
        }

        // Commands
        public RelayCommand SaveCommand { get; set; }

        // Font scale properties
        // Replace both font scale property setters in SettingsViewModel.cs
        public string ScalePercentage
        {
            get => $"Scale: {CurrentFontScale:P0}";
        }

        public bool ShowBackpack
        {
            get => _appSettings.DisplayOptions.ShowBackpack;
            set
            {
                if (_appSettings.DisplayOptions.ShowBackpack != value)
                {
                    _appSettings.DisplayOptions.ShowBackpack = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowCargo
        {
            get => _appSettings.DisplayOptions.ShowCargo;
            set
            {
                if (_appSettings.DisplayOptions.ShowCargo != value)
                {
                    _appSettings.DisplayOptions.ShowCargo = value;
                    OnPropertyChanged();
                }
            }
        }

        // Panel display options
        public bool ShowCommanderName
        {
            get => _appSettings.DisplayOptions.ShowCommanderName;
            set
            {
                if (_appSettings.DisplayOptions.ShowCommanderName != value)
                {
                    _appSettings.DisplayOptions.ShowCommanderName = value;
                    OnPropertyChanged();
                }
            }
        }

        // Flag properties
        public bool ShowFlag_ShieldsUp
        {
            get => _appSettings.DisplayOptions.ShowFlag_ShieldsUp;
            set
            {
                if (_appSettings.DisplayOptions.ShowFlag_ShieldsUp != value)
                {
                    _appSettings.DisplayOptions.ShowFlag_ShieldsUp = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowFlag_Supercruise
        {
            get => _appSettings.DisplayOptions.ShowFlag_Supercruise;
            set
            {
                if (_appSettings.DisplayOptions.ShowFlag_Supercruise != value)
                {
                    _appSettings.DisplayOptions.ShowFlag_Supercruise = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowFleetCarrierCargoCard
        {
            get => _appSettings.ShowFleetCarrierCargoCard;
            set
            {
                if (_appSettings.ShowFleetCarrierCargoCard != value)
                {
                    _appSettings.ShowFleetCarrierCargoCard = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool ShowFuelLevel
        {
            get => _appSettings.DisplayOptions.ShowFuelLevel;
            set
            {
                if (_appSettings.DisplayOptions.ShowFuelLevel != value)
                {
                    _appSettings.DisplayOptions.ShowFuelLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowRoute
        {
            get => _appSettings.DisplayOptions.ShowRoute;
            set
            {
                if (_appSettings.DisplayOptions.ShowRoute != value)
                {
                    _appSettings.DisplayOptions.ShowRoute = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion Public Properties

        #region Public Methods

        // Public method to notify font size changes
        public void NotifyFontSizeChanged()
        {
            FontSizeChanged?.Invoke();
        }

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        // Add this public method to the SettingsViewModel class
        public void SaveSettings()
        {
            SettingsManager.Save(_appSettings);
            Log.Information("💾 Saving: FloatingWindow = {Mode}, FullscreenScale = {F}, FloatingScale = {S}",
                _appSettings.UseFloatingWindow,
                _appSettings.FullscreenFontScale,
                _appSettings.FloatingFontScale);
        }
        private async Task TestMqttConnection()
        {
            if (!MqttEnabled)
            {
                ShowMessage("MQTT is disabled. Enable it first to test the connection.");
                return;
            }

            if (string.IsNullOrWhiteSpace(MqttBrokerHost))
            {
                ShowMessage("Please enter a broker host.");
                return;
            }

            try
            {
                ShowMessage("Testing MQTT connection...");

                // Create a temporary test settings object
                var testSettings = new AppSettings
                {
                    MqttEnabled = true,
                    MqttBrokerHost = MqttBrokerHost,
                    MqttBrokerPort = MqttBrokerPort,
                    MqttUsername = MqttUsername,
                    MqttPassword = MqttPassword,
                    MqttClientId = MqttClientId + "_test",
                    MqttUseTls = MqttUseTls,
                    MqttTopicPrefix = MqttTopicPrefix
                };

                // Test connection using a temporary MQTT service instance
                using (var testClient = new MQTTnet.MqttFactory().CreateMqttClient())
                {
                    var optionsBuilder = new MQTTnet.Client.MqttClientOptionsBuilder()
                        .WithClientId(testSettings.MqttClientId)
                        .WithTcpServer(testSettings.MqttBrokerHost, testSettings.MqttBrokerPort)
                        .WithCleanSession();

                    if (!string.IsNullOrEmpty(testSettings.MqttUsername))
                    {
                        optionsBuilder.WithCredentials(testSettings.MqttUsername, testSettings.MqttPassword);
                    }

                    if (testSettings.MqttUseTls)
                    {
                        optionsBuilder.WithTls();
                    }

                    var options = optionsBuilder.Build();

                    // Try to connect with a timeout
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        var result = await testClient.ConnectAsync(options, cts.Token);

                        if (result.ResultCode == MQTTnet.Client.MqttClientConnectResultCode.Success)
                        {
                            ShowMessage("✓ MQTT connection successful!");
                            await testClient.DisconnectAsync();
                        }
                        else
                        {
                            ShowMessage($"✗ MQTT connection failed: {result.ResultCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"✗ MQTT connection error: {ex.Message}");
                Serilog.Log.Error(ex, "MQTT connection test failed");
            }
        }

        // Helper method to show messages (you might need to implement this based on your UI framework)
        private void ShowMessage(string message)
        {
            // You could use a MessageBox, toast notification, or update a status label
            // For now, just log it
            Serilog.Log.Information("MQTT Test: {Message}", message);

            // If you have a status message property, update it:
            // StatusMessage = message;
        }
        // Method to handle screen selection
        public void SelectScreen(Screen screen)
        {
            if (screen != null)
            {
                _appSettings.SelectedScreenId = screen.DeviceName;
                _appSettings.SelectedScreenBounds = screen.WpfBounds;
                ScreenChanged?.Invoke(screen);
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void RequestDisplayChange()
        {
            DisplayChangeRequested?.Invoke();
        }

        // Current font scale based on window mode
        // Replace the CurrentFontScale property in SettingsViewModel.cs
        private void UpdateFontSizePreview()
        {
            // Update preview text size based on current scale
            double baseFontSize = AppSettings.UseFloatingWindow
                ? AppSettings.DEFAULT_FLOATING_BASE * FloatingFontScale
                : AppSettings.DEFAULT_FULLSCREEN_BASE * FullscreenFontScale;

            FontSizePreview = baseFontSize;
            Log.Debug("Font size preview updated to: {Size} (scale: {Scale})",
                baseFontSize,
                IsFloatingWindowMode ? FloatingFontScale : FullscreenFontScale);
        }

        #endregion Private Methods

    }
} 