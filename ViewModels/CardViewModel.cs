// CardViewModel.cs
using Serilog;
using System.Windows;

namespace EliteInfoPanel.ViewModels
{
    public abstract class CardViewModel : ViewModelBase
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


        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    // Critical - Notify the main view model that a card's visibility has changed
                    Log.Information("{CardType}: IsVisible changed to {IsVisible}", this.GetType().Name, value);
                    NotifyCardVisibilityChanged();
                }
            }
        }

        // Add this method to the CardViewModel class:

        private void NotifyCardVisibilityChanged()
        {
            // Find the MainViewModel
            if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                Log.Information("{CardType}: Notifying MainViewModel about visibility change", this.GetType().Name);
                mainVm.RefreshLayout(false);
            }
            else
            {
                Log.Warning("{CardType}: Cannot notify MainViewModel - not found", this.GetType().Name);
            }
        }

        protected CardViewModel(string title)
        {
            _title = title;
            _isVisible = true;
        }
    }
}