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
    public class SettingsViewModel : ViewModelBase
    {
        #region Private Fields

       
        private AppSettings _appSettings;
        private int _fontSize = 14;
        private double _fontSizePreview = 14;

        #endregion Private Fields

        #region Public Constructors
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
        public ObservableCollection<CardViewModel> AvailableCards { get; } = new();

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