// CardViewModel.cs
using EliteInfoPanel.Util;
using Serilog;
using System.Windows;

namespace EliteInfoPanel.ViewModels
{
    public class CardViewModel : ViewModelBase
    {
        private string _title;
        private bool _isVisible;
        private bool _isUserEnabled = true;
        protected bool _contextVisible = true;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private double _fontSize = 14;
        public virtual double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public string CardName { get; set; }

        public bool IsUserEnabled
        {
            get => _isUserEnabled;
            set
            {
                if (SetProperty(ref _isUserEnabled, value))
                {
                    Log.Debug("CardViewModel [{0}]: IsUserEnabled set to {1}",
                        this.GetType().Name, value);
                    UpdateIsVisible();
                }
            }
        }

        public void SetContextVisibility(bool contextVisible)
        {
            if (_contextVisible != contextVisible)
            {
                Log.Debug("CardViewModel [{0}]: ContextVisibility set to {1}",
                    this.GetType().Name, contextVisible);
                _contextVisible = contextVisible;
                UpdateIsVisible();
            }
        }

        private void UpdateIsVisible()
        {
            bool newVisibility = _isUserEnabled && _contextVisible;

            // Only update if changing
            if (IsVisible != newVisibility)
            {
                Log.Debug("CardViewModel [{0}]: Final visibility changing to {1} (UserEnabled={2}, ContextVisible={3})",
                    this.GetType().Name, newVisibility, _isUserEnabled, _contextVisible);
                IsVisible = newVisibility;
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            private set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;

                    EventAggregator.Instance.Publish(new CardVisibilityChangedEvent
                    {
                        CardName = this.GetType().Name,
                        IsVisible = value,
                        RequiresLayoutRefresh = false
                    });

                    OnPropertyChanged();
                }
            }
        }

        protected CardViewModel(string title)
        {
            _title = title;
            _isVisible = true;
        }
    }
}