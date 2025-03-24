using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class JournalEntry
    {
        public string timestamp { get; set; }
        public string @event { get; set; }
        public string Commander { get; set; }
        public string Ship { get; set; }
        public string Ship_Localised { get; set; }

        public string Event => @event;
    }

}
