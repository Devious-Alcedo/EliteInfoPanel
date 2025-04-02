using EliteInfoPanel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EliteInfoPanel.Util
{
    public class AppSettings
    {
        #region Public Properties

        public DisplayOptions DisplayOptions { get; set; } = new DisplayOptions();
        public string? LastOptionsScreenId { get; set; }
        public string? SelectedScreenId { get; set; }
        public Rect? SelectedScreenBounds { get; set; }
        #endregion Public Properties
    }

}
