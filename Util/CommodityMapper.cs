using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    public static class CommodityMapper
    {
        private static Dictionary<string, string> map;

        static CommodityMapper()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "commodity_mapping.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                map = new();
            }
        }

        public static string GetDisplayName(string internalName)
        {
            if (internalName == null)
                return string.Empty;

            // Case-insensitive lookup
            var match = map.FirstOrDefault(kvp =>
                kvp.Key.Equals(internalName, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrEmpty(match.Key) ? internalName : match.Value;
        }
    }

}
