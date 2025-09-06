using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

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
                Log.Information("CommodityMapper loaded {Count} mappings from {Path}", map.Count, path);
            }
            else
            {
                map = new();
                Log.Warning("CommodityMapper: mapping file not found at {Path}", path);
            }
        }

        public static string GetDisplayName(string internalName)
        {
            if (internalName == null)
                return string.Empty;

            // Case-insensitive lookup
            var match = map.FirstOrDefault(kvp =>
                kvp.Key.Equals(internalName, StringComparison.OrdinalIgnoreCase));

            string result = string.IsNullOrEmpty(match.Key) ? internalName : match.Value;
            
            // Debug logging for specific commodities we're having trouble with
            if (internalName.Contains("water", StringComparison.OrdinalIgnoreCase) ||
                internalName.Contains("oxygen", StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug("CommodityMapper: '{InternalName}' → '{DisplayName}' (found match: {HasMatch})",
                    internalName, result, !string.IsNullOrEmpty(match.Key));
            }
            
            return result;
        }
    }

}
