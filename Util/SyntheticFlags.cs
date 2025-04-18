using EliteInfoPanel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    public static class SyntheticFlags
    {
        public static readonly Flag HudInCombatMode = (Flag)(1u << 31 | 1u << 30);
        public static readonly Flag Docking = (Flag)(1u << 13 | 1u << 8 | 1u << 7 | 1u << 6 | 1u << 5 | 1u << 3 | 1u << 1);

        public static IEnumerable<Flag> All => new[] { HudInCombatMode, Docking };
    }

}
