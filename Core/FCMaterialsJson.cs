using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class FCMaterialsJson
    {
        #region Public Properties

        public List<MaterialItem> Items { get; set; } = new();

        public List<MaterialItem> Materials => Items.Select(i =>
        {
            i.Category = "FCMaterial";
            return i;
        }).ToList();

        #endregion Public Properties

        #region Public Classes

        public class MaterialItem
        {
            #region Public Properties

            public string Category { get; set; }
            public int Count => Stock;
            public int Demand { get; set; }
            public int id { get; set; }
            public string Name { get; set; }
            public string Name_Localised { get; set; }
            public int Price { get; set; }
            public int Stock { get; set; }

            #endregion Public Properties

            // for display reuse
        }

        #endregion Public Classes
    }
}
