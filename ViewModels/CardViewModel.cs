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
                if (_isVisible != value)
                {
                    _isVisible = value;

                    if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                    {
                        NotifyCardVisibilityChanged();
                    }
                    else
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(NotifyCardVisibilityChanged);
                    }

                    OnPropertyChanged();
                }
            }
        }


        // Add this method to the CardViewModel class:

        private void NotifyCardVisibilityChanged()
        {
            try
            {
                // Find the MainViewModel
                if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    Log.Debug("{CardType}: Notifying MainViewModel about visibility change", this.GetType().Name);
                    mainVm.RefreshLayout(false);
                }
                else
                {
                    Log.Warning("{CardType}: Cannot notify MainViewModel - not found", this.GetType().Name);

                    // Schedule a retry after a short delay
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => {
                        if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVmRetry)
                        {
                            Log.Debug("{CardType}: Successfully found MainViewModel on retry", this.GetType().Name);
                            mainVmRetry.RefreshLayout(false);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in NotifyCardVisibilityChanged");
            }
        }

        protected CardViewModel(string title)
        {
            _title = title;
            _isVisible = true;
        }
    }
}