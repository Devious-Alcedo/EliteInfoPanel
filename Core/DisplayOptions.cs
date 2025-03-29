using System.Collections.Generic;
using System.ComponentModel;

namespace EliteInfoPanel.Core
{
    public class DisplayOptions : INotifyPropertyChanged
    {
        #region Private Fields

        private bool showBackpack;
        private bool showCargo;
        private bool showCommanderName;
        private bool showFuelLevel;
        private bool showRoute;
        private bool showShipInfo;
        private bool enableDynamicViewMode;
        private List<Flag> visibleFlags = new();

        private bool showFlag_ShieldsUp;
        private bool showFlag_Supercruise;
        private bool showFlag_HardpointsDeployed;
        private bool showFlag_SilentRunning;
        private bool showFlag_Docked;
        private bool showFlag_CargoScoopDeployed;
        private bool showFlag_FlightAssistOff;
        private bool showFlag_NightVision;
        private bool showFlag_Overheating;
        private bool showFlag_LowFuel;
        private bool showFlag_MassLocked;
        private bool showFlag_LandingGear;

        #endregion

        #region Public Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Public Properties
        // Example additional properties
        public bool ShowWhenSupercruise { get; set; }
        public bool ShowWhenDocked { get; set; }
        public bool ShowWhenInSRV { get; set; }
        public bool ShowWhenOnFoot { get; set; }
        public bool ShowWhenInFighter { get; set; }

        public bool ShowBackpack
        {
            get => showBackpack;
            set { showBackpack = value; OnPropertyChanged(nameof(ShowBackpack)); }
        }

        public bool ShowCargo
        {
            get => showCargo;
            set { showCargo = value; OnPropertyChanged(nameof(ShowCargo)); }
        }

        public bool ShowCommanderName
        {
            get => showCommanderName;
            set { showCommanderName = value; OnPropertyChanged(nameof(ShowCommanderName)); }
        }

        public bool ShowFCMaterials { get; set; } = true;

        public bool ShowFuelLevel
        {
            get => showFuelLevel;
            set { showFuelLevel = value; OnPropertyChanged(nameof(ShowFuelLevel)); }
        }

        public bool ShowRoute
        {
            get => showRoute;
            set { showRoute = value; OnPropertyChanged(nameof(ShowRoute)); }
        }

        public bool ShowShipInfo
        {
            get => showShipInfo;
            set { showShipInfo = value; OnPropertyChanged(nameof(ShowShipInfo)); }
        }

        public bool EnableDynamicViewMode
        {
            get => enableDynamicViewMode;
            set { enableDynamicViewMode = value; OnPropertyChanged(nameof(EnableDynamicViewMode)); }
        }

        public List<Flag> VisibleFlags
        {
            get => visibleFlags;
            set { visibleFlags = value; OnPropertyChanged(nameof(VisibleFlags)); }
        }

        public bool ShowFlag_ShieldsUp
        {
            get => showFlag_ShieldsUp;
            set { showFlag_ShieldsUp = value; OnPropertyChanged(nameof(ShowFlag_ShieldsUp)); }
        }

        public bool ShowFlag_Supercruise
        {
            get => showFlag_Supercruise;
            set { showFlag_Supercruise = value; OnPropertyChanged(nameof(ShowFlag_Supercruise)); }
        }

        public bool ShowFlag_HardpointsDeployed
        {
            get => showFlag_HardpointsDeployed;
            set { showFlag_HardpointsDeployed = value; OnPropertyChanged(nameof(ShowFlag_HardpointsDeployed)); }
        }

        public bool ShowFlag_SilentRunning
        {
            get => showFlag_SilentRunning;
            set { showFlag_SilentRunning = value; OnPropertyChanged(nameof(ShowFlag_SilentRunning)); }
        }

        public bool ShowFlag_Docked
        {
            get => showFlag_Docked;
            set { showFlag_Docked = value; OnPropertyChanged(nameof(ShowFlag_Docked)); }
        }

        public bool ShowFlag_CargoScoopDeployed
        {
            get => showFlag_CargoScoopDeployed;
            set { showFlag_CargoScoopDeployed = value; OnPropertyChanged(nameof(ShowFlag_CargoScoopDeployed)); }
        }

        public bool ShowFlag_FlightAssistOff
        {
            get => showFlag_FlightAssistOff;
            set { showFlag_FlightAssistOff = value; OnPropertyChanged(nameof(ShowFlag_FlightAssistOff)); }
        }

        public bool ShowFlag_NightVision
        {
            get => showFlag_NightVision;
            set { showFlag_NightVision = value; OnPropertyChanged(nameof(ShowFlag_NightVision)); }
        }

        public bool ShowFlag_Overheating
        {
            get => showFlag_Overheating;
            set { showFlag_Overheating = value; OnPropertyChanged(nameof(ShowFlag_Overheating)); }
        }

        public bool ShowFlag_LowFuel
        {
            get => showFlag_LowFuel;
            set { showFlag_LowFuel = value; OnPropertyChanged(nameof(ShowFlag_LowFuel)); }
        }

        public bool ShowFlag_MassLocked
        {
            get => showFlag_MassLocked;
            set { showFlag_MassLocked = value; OnPropertyChanged(nameof(ShowFlag_MassLocked)); }
        }

        public bool ShowFlag_LandingGear
        {
            get => showFlag_LandingGear;
            set { showFlag_LandingGear = value; OnPropertyChanged(nameof(ShowFlag_LandingGear)); }
        }

        #endregion

        #region Protected Methods

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion
    }
}
