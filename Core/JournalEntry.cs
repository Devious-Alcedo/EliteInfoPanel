using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class JournalEntry
    {
        #region Public Properties

        public string @event { get; set; }
        public string Name { get; set; }
        // Used for Commander name
        public string Ship { get; set; }

        public string Ship_Localised { get; set; }
        public string timestamp { get; set; }
        public string UserShipId { get; set; }
        public string UserShipName { get; set; }

        #endregion Public Properties

        // Used for Ship name
        // Removed conflicting property alias to fix JSON parsing error
    }


}
