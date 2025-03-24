using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class FCMaterialsJson
    {
        public List<MaterialItem> Materials { get; set; } = new();

        public class MaterialItem
        {
            public string Name { get; set; }
            public string Name_Localised { get; set; }
            public string Category { get; set; }
            public int Count { get; set; }
        }
    }
}
