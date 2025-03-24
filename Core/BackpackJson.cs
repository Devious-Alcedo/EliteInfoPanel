using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class BackpackJson
    {
        public List<BackpackItem> Inventory { get; set; } = new();

        public class BackpackItem
        {
            public string Name { get; set; }
            public string Name_Localised { get; set; } // optional localized name
            public string Category { get; set; } // e.g., Component, Data, etc.
            public int Count { get; set; }
        }
    }
}
