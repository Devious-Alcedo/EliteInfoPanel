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
        public string eventType { get; set; }  // 'event' in original JSON, renamed to avoid keyword collision
        public string Commander { get; set; }
        public string StarSystem { get; set; }
        public string Ship { get; set; }
        public string FuelLevel { get; set; }
        public string JumpDist { get; set; }
        // Add more fields as needed
    }
}
