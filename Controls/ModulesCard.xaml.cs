using EliteInfoPanel.Core;
using EliteInfoPanel.ViewModels;
using Serilog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace EliteInfoPanel.Controls
{
    public partial class ModulesCard : UserControl
    {
        public ModulesCard()
        {
            InitializeComponent();

            // Attach fade handler to VM when it's bound
            this.DataContextChanged += ModulesCard_DataContextChanged;
        }

        private void ModulesCard_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ModulesViewModel vm)
            {
                vm.OnRequestPageFade = async (int nextPage) =>
                {
                    await FadeAndSwap(() =>
                    {
                        if (vm.CurrentPage != nextPage)
                            vm.CurrentPage = nextPage;
                    });
                };

            }
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAvailableHeight();
        }
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAvailableHeight();
        }

        private void UpdateAvailableHeight()
        {
            if (DataContext is ModulesViewModel vm && modulesScroll != null)
            {
                double newHeight = modulesScroll.ActualHeight;

                // Account for padding/margins (e.g., TextBlock title, spacing, etc.)
                const double verticalMarginBuffer = 16; // Adjust based on your layout

                double adjustedHeight = Math.Max(0, newHeight - verticalMarginBuffer);

                if (Math.Abs(adjustedHeight - vm.AvailableHeight) > 1)
                {
                    vm.AvailableHeight = adjustedHeight;
                    System.Diagnostics.Debug.WriteLine($"✅ AvailableHeight set to {vm.AvailableHeight} (adjusted from {newHeight})");

                    vm.UpdateModules(); // Trigger pagination refresh
                }
            }
        }


        private async Task FadeAndSwap(Action updateAction)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

            modulesContent.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(300);

            updateAction?.Invoke();

            modulesContent.BeginAnimation(OpacityProperty, fadeIn);
        }
    }


}