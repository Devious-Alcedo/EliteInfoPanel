using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class CargoJson
    {
        public List<CargoItem> Inventory { get; set; }
        public class CargoItem
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public double Value { get; set; }
        }
    }
}
