using System.Collections.Generic;

namespace EliteInfoPanel.Core
{
    public class Engineering
    {
        public string Engineer { get; set; }
        public int EngineerID { get; set; }
        public int BlueprintID { get; set; }
        public string BlueprintName { get; set; }
        public int Level { get; set; }
        public float Quality { get; set; }
        public List<Modifier> Modifiers { get; set; }
    }

    public class Modifier
    {
        public string Label { get; set; }
        public float Value { get; set; }
        public float OriginalValue { get; set; }
        public int LessIsGood { get; set; }
    }
}
