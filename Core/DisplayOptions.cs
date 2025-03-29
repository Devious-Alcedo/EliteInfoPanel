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

        private List<Flag> visibleFlags = new();

        #endregion Private Fields


        #region Public Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

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

        public List<Flag> VisibleFlags
        {
            get => visibleFlags;
            set
            {
                visibleFlags = value;
                OnPropertyChanged(nameof(VisibleFlags));
            }
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
       

        #endregion Public Properties

        #region Protected Methods

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion Protected Methods
    }
}
