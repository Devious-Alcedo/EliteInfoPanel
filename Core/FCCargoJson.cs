using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class FCCargoJson
    {
        #region Public Properties

        public List<FCCargoItem> Inventory { get; set; } = new();

        #endregion Public Properties

        #region Public Classes

        public class FCCargoItem
        {
            #region Public Properties

            public int Count { get; set; }
            public string Name { get; set; }
            public string Name_Localised { get; set; }

            #endregion Public Properties
        }

        #endregion Public Classes
    }
}
