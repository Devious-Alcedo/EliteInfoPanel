using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class StatusJson
    {
        public string Fuel { get; set; }  // Needs parsing
        public string LegalState { get; set; }
        public string GameMode { get; set; }
        public string Ship { get; set; }
    }
}
