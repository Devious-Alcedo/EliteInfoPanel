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
        private AppSettings _appSettings;
        public AppSettings AppSettings => _appSettings;
        public event Action DisplayChangeRequested;
        private double _fontSizePreview = 14;
        public double FontSizePreview
        {
            get => _fontSizePreview;
            set => SetProperty(ref _fontSizePreview, value);
        }
        private int _fontSize = 14;
        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        // Font scale properties
        public double FloatingFontScale
        {
            get => _appSettings.FloatingFontScale;
            set
            {
                if (_appSettings.FloatingFontScale != value)
                {
                    _appSettings.FloatingFontScale = value;
                    OnPropertyChanged();
                }
            }
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
                }
            }
        }


        // Font scale bounds
        public double MinFontScale => 0.7;
        public double MaxFontScale => 1.5;
        public double FontScaleStep => 0.05;

        // Current font scale based on window mode
        public double CurrentFontScale
        {
            get => IsFloatingWindowMode ? FloatingFontScale : FullscreenFontScale;
            set
            {
                if (IsFloatingWindowMode)
                    FloatingFontScale = value;
                else
                    FullscreenFontScale = value;

                // 💡 Calculate the font size first
                double baseFontSize = AppSettings.UseFloatingWindow
                    ? AppSettings.DEFAULT_FLOATING_BASE * FloatingFontScale
                    : AppSettings.DEFAULT_FULLSCREEN_BASE * FullscreenFontScale;

                // ✅ Now it's safe to use it
                if (Application.Current.MainWindow is MainWindow mw && mw.DataContext is MainViewModel vm)
                {
                    foreach (var card in vm.Cards)
                    {
                        card.FontSize = baseFontSize;
                    }
                }

                // Optional: update app resources too (if you still use them anywhere)
                Application.Current.Resources["BaseFontSize"] = baseFontSize;
                Application.Current.Resources["HeaderFontSize"] = baseFontSize + 4;
                Application.Current.Resources["SmallFontSize"] = baseFontSize - 2;

                OnPropertyChanged();
                FontSizeChanged?.Invoke();
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
                }
            }
        }

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

        // Commands
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand CancelCommand { get; set; }
        public RelayCommand ChangeDisplayCommand { get; set; }

        private void RequestDisplayChange()
        {
            DisplayChangeRequested?.Invoke();
        }

        // Event for screen change notification
        public event Action<Screen> ScreenChanged;

        // Event for font size change
        public event Action FontSizeChanged;

        public SettingsViewModel(AppSettings settings)
        {
            _appSettings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Initialize default font scales if they're 0
            if (_appSettings.FullscreenFontScale <= 0)
                _appSettings.FullscreenFontScale = 1.0;

            if (_appSettings.FloatingFontScale <= 0)
                _appSettings.FloatingFontScale = 1.0;

            // Initialize commands
            SaveCommand = new RelayCommand(_ => SaveSettings());
            CancelCommand = new RelayCommand(_ => { /* Will be handled by view */ });
            ChangeDisplayCommand = new RelayCommand(_ => RequestDisplayChange());
        }

        public void SaveSettings()
        {
            SettingsManager.Save(_appSettings);
            Log.Information("💾 Saving: FloatingWindow = {Mode}, FullscreenScale = {F}, FloatingScale = {S}",
                _appSettings.UseFloatingWindow,
                _appSettings.FullscreenFontScale,
                _appSettings.FloatingFontScale);


            // Notify that font size might have changed
            // FontSizeChanged?.Invoke();
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
    }
}