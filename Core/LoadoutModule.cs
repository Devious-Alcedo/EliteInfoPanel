using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class LoadoutModule
    {
        #region Public Properties

        public int? AmmoInClip { get; set; }
        public int? AmmoInHopper { get; set; }
        public Engineering Engineering { get; set; }
        public int EngineerLevel { get; set; } = 0;
        public float Health { get; set; }
        public string? Item { get; set; }
        public string? ItemLocalised { get; set; }
        public bool On { get; set; }
        public int? Priority { get; set; }
        public string? Slot { get; set; }
        public int Class { get; set; }
        public string Rating { get; set; }
       
        #endregion Public Properties
    }


}
