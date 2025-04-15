using System.Collections.ObjectModel;
using System.Linq;
using EliteInfoPanel.Core;
using MaterialDesignThemes.Wpf;

namespace EliteInfoPanel.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly GameStateService _gameState;
        private bool _isLoading = true;
        private bool _isHyperspaceJumping;
        private string _hyperspaceDestination;
        private string _hyperspaceStarClass;
        private SnackbarMessageQueue _toastQueue = new(System.TimeSpan.FromSeconds(3));

        public ObservableCollection<CardViewModel> Cards { get; } = new();

        // Individual card ViewModels
        public SummaryViewModel SummaryCard { get; }
        public CargoViewModel CargoCard { get; }
        public BackpackViewModel BackpackCard { get; }
        public RouteViewModel RouteCard { get; }
        public ModulesViewModel ModulesCard { get; }
        public FlagsViewModel FlagsCard { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsHyperspaceJumping
        {
            get => _isHyperspaceJumping;
            set => SetProperty(ref _isHyperspaceJumping, value);
        }

        public string HyperspaceDestination
        {
            get => _hyperspaceDestination;
            set => SetProperty(ref _hyperspaceDestination, value);
        }

        public string HyperspaceStarClass
        {
            get => _hyperspaceStarClass;
            set => SetProperty(ref _hyperspaceStarClass, value);
        }

        public SnackbarMessageQueue ToastQueue
        {
            get => _toastQueue;
            set => SetProperty(ref _toastQueue, value);
        }

        public RelayCommand OpenOptionsCommand { get; set; }
        public RelayCommand CloseCommand { get; }

        public MainViewModel(GameStateService gameState)
        {
            _gameState = gameState;

            // Initialize commands
            OpenOptionsCommand = new RelayCommand(_ => OpenOptions());
            CloseCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());

            // Initialize card ViewModels
            SummaryCard = new SummaryViewModel(gameState) { Title = "Summary" };
            CargoCard = new CargoViewModel(gameState) { Title = "Cargo" };
            BackpackCard = new BackpackViewModel(gameState) { Title = "Backpack" };
            RouteCard = new RouteViewModel(gameState) { Title = "Nav Route" };
            ModulesCard = new ModulesViewModel(gameState) { Title = "Ship Modules", ColumnSpan = 2 };
            FlagsCard = new FlagsViewModel(gameState) { Title = "Status Flags", ColumnSpan = 2 };

            // Add cards to collection
            Cards.Add(SummaryCard);
            Cards.Add(CargoCard);
            Cards.Add(BackpackCard);
            Cards.Add(RouteCard);
            Cards.Add(ModulesCard);
            Cards.Add(FlagsCard);
            UpdateLoadingState();
            // Subscribe to game state events
            _gameState.DataUpdated += OnGameStateUpdated;
            _gameState.HyperspaceJumping += OnHyperspaceJumping;

            // Initial update
            RefreshCardVisibility();
        }
        // Add to MainViewModel
        private bool IsEliteRunning()
        {
            var status = _gameState?.CurrentStatus;
            return status != null && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));
        }

        public void UpdateLoadingState()
        {
            IsLoading = !IsEliteRunning();
        }
        private void OnGameStateUpdated()
        {
            System.Diagnostics.Debug.WriteLine("GameState update received. Setting IsLoading = false");
            UpdateLoadingState();
            IsLoading = false;
            RefreshCardVisibility();
        }

        private void OnHyperspaceJumping(bool jumping, string systemName)
        {
            IsHyperspaceJumping = jumping;
            HyperspaceDestination = systemName;
            HyperspaceStarClass = _gameState.HyperspaceStarClass;
            RefreshCardVisibility();
        }
        protected void RunOnUiThread(Action action)
        {
            if (System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
            {
                // We're already on the UI thread
                action();
            }
            else
            {
                // We need to invoke the action on the UI thread
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(action);
            }
        }
        private void RefreshCardVisibility()
        {
            var status = _gameState.CurrentStatus;
            if (status == null) return;

            // Hide all cards during hyperspace jump
            if (IsHyperspaceJumping)
            {
                foreach (var card in Cards)
                {
                    card.IsVisible = false;
                }
                return;
            }

            // Determine overall visibility based on game state
            bool shouldShowPanels = !IsHyperspaceJumping && (
                status.Flags.HasFlag(Flag.Docked) ||
                status.Flags.HasFlag(Flag.Supercruise) ||
                status.Flags.HasFlag(Flag.InSRV) ||
                status.OnFoot ||
                status.Flags.HasFlag(Flag.InFighter) ||
                status.Flags.HasFlag(Flag.InMainShip));

            // Update individual card visibility
            SummaryCard.IsVisible = shouldShowPanels;
            CargoCard.IsVisible = shouldShowPanels && (_gameState.CurrentCargo?.Inventory?.Count ?? 0) > 0;
            BackpackCard.IsVisible = shouldShowPanels && status.OnFoot;
            RouteCard.IsVisible = shouldShowPanels && (_gameState.CurrentRoute?.Route?.Any() == true ||
                !string.IsNullOrWhiteSpace(_gameState.CurrentStatus?.Destination?.Name));
            ModulesCard.IsVisible = shouldShowPanels && status.Flags.HasFlag(Flag.InMainShip) &&
                                  !status.OnFoot &&
                                  !status.Flags.HasFlag(Flag.InSRV) &&
                                  !status.Flags.HasFlag(Flag.InFighter);
            FlagsCard.IsVisible = shouldShowPanels && _gameState.CurrentStatus != null;

            // Update column layout
            UpdateCardLayout();
        }

        private void UpdateCardLayout()
        {
            var visibleCards = Cards.Where(c => c.IsVisible).ToList();

            int currentColumn = 0;
            foreach (var card in visibleCards)
            {
                card.DisplayColumn = currentColumn;
                currentColumn += card.ColumnSpan;
            }
        }

        private void OpenOptions()
        {
            // This will be handled in the view
            System.Diagnostics.Debug.WriteLine("Open Options requested");
        }

        public void ShowToast(string message)
        {
            ToastQueue.Enqueue(message);
        }
    }
}