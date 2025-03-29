using EliteInfoPanel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel
{
    public class AppSettings
    {
        public string? SelectedScreenId { get; set; }
        public string? LastOptionsScreenId { get; set; }

        public DisplayOptions DisplayOptions { get; set; } = new DisplayOptions();
    }

}
