using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class BackpackJson
    {
        public List<BackpackItem> Items { get; set; } = new();
        public List<BackpackItem> Components { get; set; } = new();
        public List<BackpackItem> Consumables { get; set; } = new();
        public List<BackpackItem> Data { get; set; } = new();

        // Combine all item types into one unified list
        public List<BackpackItem> Inventory =>
            Items.Concat(Components).Concat(Consumables).Concat(Data)
                 .Select(i =>
                 {
                     // Add category if missing (optional)
                     if (string.IsNullOrEmpty(i.Category))
                     {
                         if (Items.Contains(i)) i.Category = "Items";
                         else if (Components.Contains(i)) i.Category = "Components";
                         else if (Consumables.Contains(i)) i.Category = "Consumables";
                         else if (Data.Contains(i)) i.Category = "Data";
                     }
                     return i;
                 }).ToList();

        public class BackpackItem
        {
            public string Name { get; set; }
            public string Name_Localised { get; set; } // optional localized name
            public string Category { get; set; } // Will be set dynamically if not provided
            public int Count { get; set; }
        }
    }

}
