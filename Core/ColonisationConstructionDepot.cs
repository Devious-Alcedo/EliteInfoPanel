using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core
{
    public class ColonisationConstructionDepot
    {
        public DateTime Timestamp { get; set; }
        public long MarketID { get; set; }
        public double ConstructionProgress { get; set; }
        public bool ConstructionComplete { get; set; }
        public bool ConstructionFailed { get; set; }
        public List<Resource> ResourcesRequired { get; set; }
    }

    public class Resource
    {
        public string Name { get; set; }
        public string NameLocalised { get; set; }
        public int RequiredAmount { get; set; }
        public int ProvidedAmount { get; set; }
        public int Payment { get; set; }
    }
}
