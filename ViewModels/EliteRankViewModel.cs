using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.ViewModels
{
    public class EliteRankViewModel : ViewModelBase
    {
        private string _rankType;
        private string _iconPath;
        private int _fontSize = 10;

        public string RankType
        {
            get => _rankType;
            set => SetProperty(ref _rankType, value);
        }

        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public EliteRankViewModel(string rankType, string iconPath)
        {
            _rankType = rankType;
            _iconPath = iconPath;
        }
    }
}
