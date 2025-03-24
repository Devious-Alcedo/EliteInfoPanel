using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class FCMaterialsJson
    {
        public List<MaterialItem> Items { get; set; } = new();

        public List<MaterialItem> Materials => Items.Select(i =>
        {
            i.Category = "FCMaterial";
            return i;
        }).ToList();

        public class MaterialItem
        {
            public int id { get; set; }
            public string Name { get; set; }
            public string Name_Localised { get; set; }
            public int Price { get; set; }
            public int Stock { get; set; }
            public int Demand { get; set; }
            public string Category { get; set; }
            public int Count => Stock; // for display reuse
        }
    }
}
