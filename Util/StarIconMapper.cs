using System.Collections.Generic;

namespace EliteInfoPanel.Util
{
    public static class StarIconMapper
    {
        public static readonly Dictionary<string, string> StarClassToIconPath = new()
        {
            ["O"] = "Assets/Stars/O.ico",
            ["B"] = "Assets/Stars/B.ico",
            ["A"] = "Assets/Stars/A.ico",
            ["F"] = "Assets/Stars/F.ico",
            ["G"] = "Assets/Stars/G.ico",
            ["K"] = "Assets/Stars/K.ico",
            ["M"] = "Assets/Stars/M.ico",
            ["L"] = "Assets/Stars/L.ico",
            ["T"] = "Assets/Stars/T.ico",
            ["Y"] = "Assets/Stars/Y.ico",
            ["TTS"] = "Assets/Stars/L.ico",
            ["Ae"] = "Assets/Stars/Ae.ico",
            ["C"] = "Assets/Stars/C.ico",
            ["CJ"] = "Assets/Stars/CJ.ico",
            ["CN"] = "Assets/Stars/CN.ico",
            ["MS"] = "Assets/Stars/MS.ico",
            ["S"] = "Assets/Stars/S.ico",
            ["White Dwarf (D)"] = "Assets/Stars/WhiteDwarf.ico",
            ["Neutron Star"] = "Assets/Stars/Neutron.ico",
            ["Black Hole"] = "Assets/Stars/BlackHole.ico",
            ["W"] = "Assets/Stars/WRS.ico",      // W class fallback
            ["WR"] = "Assets/Stars/WRS.ico",
            ["WRC"] = "Assets/Stars/WRC.ico",
            ["WRN"] = "Assets/Stars/WRN.ico",
            ["WRNC"] = "Assets/Stars/WRNC.ico",
            ["WRO"] = "Assets/Stars/WRO.ico",
        };

        public static string GetIconPath(string starClass)
        {
            if (string.IsNullOrWhiteSpace(starClass))
                return null;

            // Try exact match
            if (StarClassToIconPath.TryGetValue(starClass, out var path))
                return path;

            // Try first letter fallback for unknown variants (e.g., "K (Yellow-Orange)")
            var first = starClass.Split(' ', '(', '-')[0];
            if (StarClassToIconPath.TryGetValue(first, out var fallback))
                return fallback;

            return null;
        }
    }
}
