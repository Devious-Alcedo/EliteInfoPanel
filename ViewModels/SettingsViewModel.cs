using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using WpfScreenHelper;

namespace EliteInfoPanel.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private AppSettings _appSettings;
        public AppSettings AppSettings => _appSettings;
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

        // Commands
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand CancelCommand { get; set; }
        public RelayCommand ChangeDisplayCommand { get; set; }

        // Event for screen change notification
        public event Action<Screen> ScreenChanged;

        public SettingsViewModel(AppSettings settings)
        {
            _appSettings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Initialize commands
            SaveCommand = new RelayCommand(_ => SaveSettings());
            CancelCommand = new RelayCommand(_ => { /* Will be handled by view */ });
            ChangeDisplayCommand = new RelayCommand(_ => RequestDisplayChange());
        }

        private void SaveSettings()
        {
            SettingsManager.Save(_appSettings);
            System.Diagnostics.Debug.WriteLine("Settings saved");
        }

        private void RequestDisplayChange()
        {
            // This will be handled by the view
            System.Diagnostics.Debug.WriteLine("Display change requested");
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