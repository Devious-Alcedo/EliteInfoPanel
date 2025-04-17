using EliteInfoPanel.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EliteInfoPanel.Controls
{
    /// <summary>
    /// Interaction logic for SummaryCard.xaml
    /// </summary>
    public partial class SummaryCard : UserControl
    {
        private readonly TagFilterConverter _tagFilterConverter = new();

        public SummaryCard()
        {
            InitializeComponent();
            Loaded += SummaryCard_Loaded;
        }

        private void SummaryCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (Resources["NormalItemsView"] is CollectionViewSource normalView)
            {
                normalView.Filter += (s, args) =>
                {
                    args.Accepted = (bool)_tagFilterConverter.Convert(
                        new object[] { args.Item },
                        typeof(bool),
                        "!CarrierJumpCountdown",
                        CultureInfo.CurrentCulture);
                };
            }

            if (Resources["CarrierCountdownView"] is CollectionViewSource countdownView)
            {
                countdownView.Filter += (s, args) =>
                {
                    args.Accepted = (bool)_tagFilterConverter.Convert(
                        new object[] { args.Item },
                        typeof(bool),
                        "CarrierJumpCountdown",
                        CultureInfo.CurrentCulture);
                };
            }
        }
    }
}
