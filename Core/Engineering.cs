using System.Collections.Generic;

namespace EliteInfoPanel.Core
{
    public class Engineering  //future use!
    {
        #region Public Properties

        public int BlueprintID { get; set; }
        public string BlueprintName { get; set; }
        public string Engineer { get; set; }
        public int EngineerID { get; set; }
        public int Level { get; set; }
        public List<Modifier> Modifiers { get; set; }
        public float Quality { get; set; }
        public string ExperimentalEffect { get; set; }
        public string ExperimentalEffect_Localised { get; set; }


        #endregion Public Properties
    }

    public class Modifier
    {
        #region Public Properties

        public string Label { get; set; }
        public int LessIsGood { get; set; }
        public float OriginalValue { get; set; }
        public float Value { get; set; }

        #endregion Public Properties
    }
}
