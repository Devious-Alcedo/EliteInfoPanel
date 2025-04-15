using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;

namespace EliteInfoPanel.ViewModels
{
    public class SummaryItemViewModel : ViewModelBase
    {
        public string Tag { get; set; }
        public string Content { get; set; }
        public Brush Foreground { get; set; }
        public PackIconKind Icon { get; set; }

        public SummaryItemViewModel(string tag, string content, Brush foreground, PackIconKind icon)
        {
            Tag = tag;
            Content = content;
            Foreground = foreground;
            Icon = icon;
        }
        public SummaryItemViewModel(string content, Brush foreground, PackIconKind icon)
        {
            Content = content;
            Foreground = foreground;
            Icon = icon;
        }

    }
}
