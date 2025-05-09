using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace EliteInfoPanel.ViewModels
{
    public class SummaryItemViewModel : ViewModelBase
    {
        public string Tag { get; set; }
        public string Key { get; }
        private string _content;
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value); // This must raise PropertyChanged
        }
        private int _fontSize = 14;
        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }
        public ObservableCollection<EliteRankInfo> EliteRanks { get; } = new();
        private bool _pulse;
        public bool Pulse
        {
            get => _pulse;
            set => SetProperty(ref _pulse, value);
        }
        private Brush _foreground;
        public Brush Foreground
        {
            get => _foreground;
            set => SetProperty(ref _foreground, value);
        }

        public bool IsCommander
        {
            get { return Tag == "Commander"; }
        }
        public PackIconKind Icon { get; set; }

        public SummaryItemViewModel(string tag, string content, Brush foreground, PackIconKind icon)
        {
            Tag = tag;
            Content = content;
            Foreground = foreground;
            Icon = icon;
        }
      
    
    }
    // Simple class to represent an Elite rank
    public class EliteRankInfo
    {
        public string RankName { get; set; }
        public string IconPath { get; set; }

        public EliteRankInfo(string rankName, string iconPath)
        {
            RankName = rankName;
            IconPath = iconPath;
        }
    }
}
