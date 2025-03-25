using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class StatusJson
    {
        public FuelInfo Fuel { get; set; }
         public long Balance { get; set; }
        public string ShipType { get; set; }

        public class FuelInfo
        {
            public float FuelMain { get; set; }
            public float FuelReservoir { get; set; }
        }
    }


}
