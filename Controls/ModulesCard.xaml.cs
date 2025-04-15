using EliteInfoPanel.Core;
using EliteInfoPanel.ViewModels;
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
                        vm.CurrentPage = nextPage; // update AFTER fade-out
                    });
                };
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