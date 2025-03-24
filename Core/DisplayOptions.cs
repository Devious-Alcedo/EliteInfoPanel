using System.ComponentModel;

namespace EliteInfoPanel.Core
{
    public class DisplayOptions : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool showCommanderName;
        public bool ShowCommanderName
        {
            get => showCommanderName;
            set { showCommanderName = value; OnPropertyChanged(nameof(ShowCommanderName)); }
        }

        private bool showShipInfo;
        public bool ShowShipInfo
        {
            get => showShipInfo;
            set { showShipInfo = value; OnPropertyChanged(nameof(ShowShipInfo)); }
        }

        private bool showFuelLevel;
        public bool ShowFuelLevel
        {
            get => showFuelLevel;
            set { showFuelLevel = value; OnPropertyChanged(nameof(ShowFuelLevel)); }
        }

        private bool showRoute;
        public bool ShowRoute
        {
            get => showRoute;
            set { showRoute = value; OnPropertyChanged(nameof(ShowRoute)); }
        }

        private bool showCargo;
        public bool ShowCargo
        {
            get => showCargo;
            set { showCargo = value; OnPropertyChanged(nameof(ShowCargo)); }
        }

        private bool showBackpack;
        public bool ShowBackpack
        {
            get => showBackpack;
            set { showBackpack = value; OnPropertyChanged(nameof(ShowBackpack)); }
        }

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
