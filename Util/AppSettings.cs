using EliteInfoPanel.Core;
using System;
using System.Windows;

namespace EliteInfoPanel.Util
{
    public class AppSettings
    {
        // Existing properties
        public DisplayOptions DisplayOptions { get; set; } = new DisplayOptions();
        public string? LastOptionsScreenId { get; set; }
        public string? SelectedScreenId { get; set; }
        public Rect? SelectedScreenBounds { get; set; }

        // New floating window properties
        public bool UseFloatingWindow { get; set; } = false;
        public double FloatingWindowWidth { get; set; } = 800;
        public double FloatingWindowHeight { get; set; } = 600;
        public double FloatingWindowLeft { get; set; } = 100;
        public double FloatingWindowTop { get; set; } = 100;
        public bool AlwaysOnTop { get; set; } = true;
    }
}