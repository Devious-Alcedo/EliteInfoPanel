using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EliteInfoPanel.ViewModels
{
    public class SummaryItemViewModel : ViewModelBase
    {
        private string _content;
        private Brush _foreground;
        private PackIconKind? _icon;

        public string Key { get; }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public Brush Foreground
        {
            get => _foreground;
            set => SetProperty(ref _foreground, value);
        }

        public PackIconKind? Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public SummaryItemViewModel(string key, string content, Brush foreground = null, PackIconKind? icon = null)
        {
            Key = key;
            _content = content;
            _foreground = foreground ?? Brushes.White;
            _icon = icon;
        }
    }
}
