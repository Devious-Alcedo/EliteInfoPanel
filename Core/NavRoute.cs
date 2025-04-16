using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class NavRouteJson
    {
        public List<NavRouteSystem> Route { get; set; } = new();

        public class NavRouteSystem
        {
            public string StarSystem { get; set; }
            public long SystemAddress { get; set; }
            public double[] StarPos { get; set; }
            public string StarClass { get; set; }

            public float Distance { get; set; }
        }
    }

}
