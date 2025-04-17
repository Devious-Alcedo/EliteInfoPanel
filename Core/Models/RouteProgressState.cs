using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Core.Models
{
    public class RouteProgressState
    {
        public string? LastKnownSystem { get; set; }
        public List<string> CompletedSystems { get; set; } = new();
    }
}
