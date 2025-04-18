using EliteInfoPanel.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace EliteInfoPanel.Controls
{
    public partial class RouteCard : UserControl
    {
        public RouteCard()
        {
            InitializeComponent();
            this.Loaded += RouteCard_Loaded;
            this.SizeChanged += RouteCard_SizeChanged;
        }

        private void RouteCard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.DataContext is RouteViewModel vm)
            {
                vm.AvailableHeight = e.NewSize.Height;
            }
        }

        private void RouteCard_Loaded(object sender, RoutedEventArgs e)
        {
            // Force one refresh after initial render to account for proper height
            Dispatcher.BeginInvoke(() =>
            {
                if (this.DataContext is RouteViewModel vm)
                {
                    vm.AvailableHeight = this.ActualHeight;
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}