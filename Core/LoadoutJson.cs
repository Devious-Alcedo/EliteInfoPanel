using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    namespace EliteInfoPanel.Core
    {
        public class LoadoutJson
        {
            public string Ship { get; set; }
            public string ShipName { get; set; }
            public string ShipIdent { get; set; }
            public List<Module> Modules { get; set; }

            public class Module
            {
                public string Slot { get; set; }
                public string Item { get; set; }
                public string ItemLocalised { get; set; }
                public double Health { get; set; }
            }
        }
    }

}
