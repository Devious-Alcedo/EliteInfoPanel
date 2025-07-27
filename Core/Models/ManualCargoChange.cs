using System;

namespace EliteInfoPanel.Core.Models
{
    /// <summary>
    /// Represents a manual change made to carrier cargo quantities by the user
    /// </summary>
    public class ManualCargoChange
    {
        /// <summary>
        /// The name of the cargo item
        /// </summary>
        public string ItemName { get; set; }

        /// <summary>
        /// The manually set quantity by the user
        /// </summary>
        public int ManualQuantity { get; set; }

        /// <summary>
        /// The original quantity from the game before manual changes
        /// </summary>
        public int OriginalGameQuantity { get; set; }

        /// <summary>
        /// When this manual change was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Whether this manual change is still active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
