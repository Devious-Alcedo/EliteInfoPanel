using System.Collections.Generic;

namespace EliteInfoPanel.Util
{
    public static class ModuleSlotHelper
    {
        private static readonly Dictionary<string, string> SlotMap = new()
        {
            ["Slot01_Size5"] = "Power Plant",
            ["Slot02_Size5"] = "Thrusters",
            ["Slot03_Size4"] = "FSD",
            ["Slot04_Size4"] = "Life Support",
            ["Slot05_Size4"] = "Power Distributor",
            ["Slot06_Size3"] = "Sensors",
            ["Slot07_Size4"] = "Fuel Tank",
            // Add more mappings as needed
        };

        public static string GetFriendlySlotName(string internalSlot)
        {
            if (string.IsNullOrWhiteSpace(internalSlot))
                return "(Unknown Slot)";

            return SlotMap.TryGetValue(internalSlot, out var friendly)
                ? friendly
                : internalSlot;
        }
    }
}
