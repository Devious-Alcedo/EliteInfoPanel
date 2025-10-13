using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Text.RegularExpressions;
using Serilog;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        private void InferClassAndRatingFromItem(LoadoutModule module)
        {
            if (module == null || string.IsNullOrEmpty(module.Item))
                return;

            var match = Regex.Match(module.Item, @"_(size)?(?<class>\d+)_class(?<rating>\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups["class"].Value, out var classNum))
                {
                    module.Class = classNum;
                }
                module.Rating = match.Groups["rating"].Value;
            }
        }
        private bool BackpacksEqual(BackpackJson bp1, BackpackJson bp2)
        {
            if (bp1 == null && bp2 == null) return true;
            if (bp1 == null || bp2 == null) return false;

            return ItemListsEqual(bp1.Items, bp2.Items) &&
                   ItemListsEqual(bp1.Components, bp2.Components) &&
                   ItemListsEqual(bp1.Consumables, bp2.Consumables) &&
                   ItemListsEqual(bp1.Data, bp2.Data);
        }

        private T DeserializeJsonFile<T>(string filePath) where T : class
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (!File.Exists(filePath)) return null;

                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length == 0) return null;

                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(json))
                        return null;

                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (IOException)
                {
                    Thread.Sleep(50); // Retry briefly
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to deserialize {FilePath}: {Error}", filePath, ex.Message);
                    return null;
                }
            }

            return null;
        }

        private bool FCMaterialItemsEqual(List<FCMaterialsJson.MaterialItem> list1, List<FCMaterialsJson.MaterialItem> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            var dict1 = list1.ToDictionary(i => i.Name, i => (i.Stock, i.Demand, i.Price));

            foreach (var item in list2)
            {
                if (!dict1.TryGetValue(item.Name, out var values) ||
                    values.Stock != item.Stock ||
                    values.Demand != item.Demand ||
                    values.Price != item.Price)
                    return false;
            }

            return true;
        }

        private bool InventoriesEqual(List<CargoJson.CargoItem> inventory1, List<CargoJson.CargoItem> inventory2)
        {
            if (inventory1 == null && inventory2 == null) return true;
            if (inventory1 == null || inventory2 == null) return false;
            if (inventory1.Count != inventory2.Count) return false;

            var dict1 = inventory1.ToDictionary(i => i.Name, i => i.Count);

            foreach (var item in inventory2)
            {
                if (!dict1.TryGetValue(item.Name, out int count) || count != item.Count)
                    return false;
            }

            return true;
        }

        private bool ItemListsEqual(List<BackpackJson.BackpackItem> list1, List<BackpackJson.BackpackItem> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            var dict1 = list1.ToDictionary(i => i.Name, i => i.Count);

            foreach (var item in list2)
            {
                if (!dict1.TryGetValue(item.Name, out int count) || count != item.Count)
                    return false;
            }

            return true;
        }

        private bool JsonEquals<T>(T obj1, T obj2)
        {
            if (obj1 == null && obj2 == null) return true;
            if (obj1 == null || obj2 == null) return false;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string json1 = JsonSerializer.Serialize(obj1, options);
            string json2 = JsonSerializer.Serialize(obj2, options);

            return json1 == json2;
        }
    }
}
