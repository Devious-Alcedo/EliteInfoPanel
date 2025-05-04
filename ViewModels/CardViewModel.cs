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

        private bool _isUserEnabled = true;
        public bool IsUserEnabled
        {
            get => _isUserEnabled;
            set
            {
                if (SetProperty(ref _isUserEnabled, value))
                    UpdateIsVisible();
            }
        }

        protected bool _contextVisible = true;
        protected void SetContextVisibility(bool contextVisible)
        {
            if (_contextVisible != contextVisible)
            {
                _contextVisible = contextVisible;
                UpdateIsVisible();
            }
        }

        private void UpdateIsVisible()
        {
            IsVisible = _isUserEnabled && _contextVisible;
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;

                    // Publish event instead of directly calling method
                    EventAggregator.Instance.Publish(new CardVisibilityChangedEvent
                    {
                        CardName = this.GetType().Name,
                        IsVisible = value,
                        RequiresLayoutRefresh = false // Most visibility changes don't need full rebuild
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