using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class FCCargoJson
    {
        public List<FCCargoItem> Inventory { get; set; } = new();

        public class FCCargoItem
        {
            public string Name { get; set; }
            public string Name_Localised { get; set; }
            public int Count { get; set; }
        }
    }
}
