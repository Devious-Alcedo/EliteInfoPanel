using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    public static class ShipNameHelper
    {
        public static readonly Dictionary<string, string> ShipNameMap = new()
        {
            ["sidewinder"] = "Sidewinder",
            ["eagle"] = "Eagle",
            ["hauler"] = "Hauler",
            ["adder"] = "Adder",
            ["viper"] = "Viper MkIII",
            ["cobramkiii"] = "Cobra MkIII",
            ["type6"] = "Type-6 Transporter",
            ["dolphin"] = "Dolphin",
            ["type7"] = "Type-7 Transporter",
            ["asp"] = "Asp Explorer",
            ["vulture"] = "Vulture",
            ["empire_trader"] = "Imperial Clipper",
            ["federation_dropship"] = "Federal Dropship",
            ["orca"] = "Orca",
            ["type9"] = "Type-9 Heavy",
            ["python"] = "Python",
            ["belugaliner"] = "Beluga Liner",
            ["ferdelance"] = "Fer-de-Lance",
            ["anaconda"] = "Anaconda",
            ["federation_corvette"] = "Federal Corvette",
            ["cutter"] = "Imperial Cutter",
            ["diamondback"] = "Diamondback Scout",
            ["empire_courier"] = "Imperial Courier",
            ["diamondbackxl"] = "Diamondback Explorer",
            ["empire_eagle"] = "Imperial Eagle",
            ["federation_dropship_mkii"] = "Federal Assault Ship",
            ["federation_gunship"] = "Federal Gunship",
            ["viper_mkiv"] = "Viper MkIV",
            ["cobramkiv"] = "Cobra MkIV",
            ["independant_trader"] = "Keelback",
            ["asp_scout"] = "Asp Scout",
            ["type9_military"] = "Type-10 Defender",
            ["krait_mkii"] = "Krait MkII",
            ["typex"] = "Alliance Chieftain",
            ["typex_2"] = "Alliance Crusader",
            ["typex_3"] = "Alliance Challenger",
            ["krait_light"] = "Krait Phantom",
            ["mamba"] = "Mamba",
            ["python_nx"] = "Python MkII",
            ["type8"] = "Type-8 Transporter"
        
        };

        public static string GetLocalisedName(string internalName)
        {
            return ShipNameMap.TryGetValue(internalName.ToLowerInvariant(), out var name)
                ? name
                : internalName;
        }
    }
}
