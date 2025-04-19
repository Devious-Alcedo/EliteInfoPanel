using System.Collections.Generic;

namespace EliteInfoPanel.Util
{
    public static class StarIconMapper
    {
        public static readonly Dictionary<string, string> StarClassToIconPath = new()
        {
            ["O"] = "Assets/Stars/O.png",
            ["B"] = "Assets/Stars/B.png",
            ["A"] = "Assets/Stars/A.png",
            ["F"] = "Assets/Stars/F.png",
            ["G"] = "Assets/Stars/G.png",
            ["K"] = "Assets/Stars/K.png",
            ["M"] = "Assets/Stars/M.png",
            ["L"] = "Assets/Stars/L.png",
            ["T"] = "Assets/Stars/T.png",
            ["Y"] = "Assets/Stars/Y.png",
            ["TTS"] = "Assets/Stars/L.png",
            ["Ae"] = "Assets/Stars/Ae.png",
            ["C"] = "Assets/Stars/C.png",
            ["CJ"] = "Assets/Stars/CJ.png",
            ["CN"] = "Assets/Stars/CN.png",
            ["MS"] = "Assets/Stars/MS.png",
            ["S"] = "Assets/Stars/S.png",
            ["White Dwarf (D)"] = "Assets/Stars/WhiteDwarf.png",
            ["Neutron Star"] = "Assets/Stars/Neutron.png",
            ["Black Hole"] = "Assets/Stars/BlackHole.png",
            ["W"] = "Assets/Stars/WRS.png",      // W class fallback
            ["WR"] = "Assets/Stars/WRS.png",
            ["WRC"] = "Assets/Stars/WRC.png",
            ["WRN"] = "Assets/Stars/WRN.png",
            ["WRNC"] = "Assets/Stars/WRNC.png",
            ["WRO"] = "Assets/Stars/WRO.png",
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
