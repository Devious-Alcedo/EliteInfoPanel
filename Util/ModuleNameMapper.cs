using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Serilog;
namespace EliteInfoPanel.Util
{
    public static class ModuleNameMapper
    {
        private static Dictionary<string, string> _map;

        static ModuleNameMapper()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ModuleNameMap.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            else
            {
                _map = new Dictionary<string, string>();
            }
        }

        public static string GetFriendlyName(string internalName)
        {
            if (string.IsNullOrWhiteSpace(internalName)) return "(Unknown)";
            return _map.TryGetValue(internalName, out var friendly) ? friendly : internalName;
            Log.Information("Module name not found: {internalName}", internalName);
        }
    }
}
