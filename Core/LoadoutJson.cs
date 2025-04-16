using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    namespace EliteInfoPanel.Core
    {
        public class LoadoutJson
        {
            #region Public Properties

            public FuelCapacityInfo FuelCapacity { get; set; }
            // Optionally:
            public float FuelMainCapacity => FuelCapacity?.Main ?? 0;
            public int CargoCapacity { get; set; }
            public float? HullHealth { get; set; }
            public List<LoadoutModule> Modules { get; set; }
            public string Ship { get; set; }
            public string ShipIdent { get; set; }
            public string ShipName { get; set; }

            #endregion Public Properties

            #region Public Classes

            public class FuelCapacityInfo
            {
                #region Public Properties

                public float Main { get; set; }
                public float Reserve { get; set; }

                #endregion Public Properties
            }

            #endregion Public Classes
        }

    }

}
