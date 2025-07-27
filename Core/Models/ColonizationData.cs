using System;
using System.Collections.Generic;
using System.Linq;

namespace EliteInfoPanel.Core.Models
{
    public class ColonizationData
    {
        public long MarketID { get; set; }
        public double ConstructionProgress { get; set; }
        public bool ConstructionComplete { get; set; }
        public bool ConstructionFailed { get; set; }
        public List<ColonizationResource> ResourcesRequired { get; set; } = new List<ColonizationResource>();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public double CompletionPercentage => Math.Round(ConstructionProgress * 100, 1);

        public int CompletedResources => ResourcesRequired.Count(r => r.IsComplete);

        public int TotalResources => ResourcesRequired.Count;
    }

    public class ColonizationResource
    {
        public string Name { get; set; }
        public string Name_Localised { get; set; }
        public int RequiredAmount { get; set; }
        public int ProvidedAmount { get; set; }
        public int Payment { get; set; }

        public bool IsComplete => ProvidedAmount >= RequiredAmount;

        public double CompletionPercentage => Math.Min(100, Math.Round((double)ProvidedAmount / RequiredAmount * 100, 1));

        public int RemainingAmount => Math.Max(0, RequiredAmount - ProvidedAmount);

        public string DisplayName => !string.IsNullOrEmpty(Name_Localised) ? Name_Localised : Name;

        // Calculate total value by multiplying remaining amount by payment per unit
        public long RemainingValue => RemainingAmount * Payment;

        // The effective profit per ton
        public int ProfitPerUnit => Payment;

        public int StillNeeded => RequiredAmount - ProvidedAmount;
        public int ShipAmount { get; set; }
        public int CarrierAmount { get; set; }
        public int AvailableAmount => ShipAmount + CarrierAmount;
        public bool ReadyToDeliver => AvailableAmount >= StillNeeded && StillNeeded > 0;
        public double RewardPerUnit => Payment;
        public double ValueOfRemainingWork => StillNeeded * Payment;
    }
}