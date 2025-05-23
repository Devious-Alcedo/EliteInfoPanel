﻿using EliteInfoPanel.Core;
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
        public bool ShowSummary { get; set; }
        public bool ShowFlags { get; set; }
        public bool ShowCargo { get; set; }
        public bool DevelopmentMode { get; set; } = false;
        public string DevelopmentJournalPath { get; set; } = "Journal.FAKEEVENTS.01.log";
        public bool ShowFleetCarrierCargoCard { get; set; } = true;
        // Add these properties to the AppSettings class
        public double FleetCarrierWindowWidth { get; set; } = 500;
        public double FleetCarrierWindowHeight { get; set; } = 600;
        public double FleetCarrierWindowLeft { get; set; } = 100;
        public double FleetCarrierWindowTop { get; set; } = 100;
        public double ColonizationWindowWidth { get; set; } = 500;
        public double ColonizationWindowHeight { get; set; } = 600;
        public double ColonizationWindowLeft { get; set; } = 100;
        public double ColonizationWindowTop { get; set; } = 100;
        public bool ShowBackpack { get; set; }
        public bool ShowRoute { get; set; }
         
        public bool ShowModules { get; set; } 
        public bool ShowColonisation { get; set; }

        // Floating window properties
        public bool UseFloatingWindow { get; set; } = false;
        public double FloatingWindowWidth { get; set; } = 800;
        public double FloatingWindowHeight { get; set; } = 600;
        public double FloatingWindowLeft { get; set; } = 100;
        public double FloatingWindowTop { get; set; } = 100;
        public bool AlwaysOnTop { get; set; } = true;
        public const double BaseFontSizeBase = 14;
        // Font scale properties (1.0 = default size)
        public double FullscreenFontScale { get; set; } = 1.0;
        public double FloatingFontScale { get; set; } = 1.0;

        // Default base font sizes
        public const double DEFAULT_FULLSCREEN_BASE = 14.0;
        public const double DEFAULT_FULLSCREEN_HEADER = 16.0;
        public const double DEFAULT_FULLSCREEN_SMALL = 12.0;

        public const double DEFAULT_FLOATING_BASE = 11.0;
        public const double DEFAULT_FLOATING_HEADER = 13.0;
        public const double DEFAULT_FLOATING_SMALL = 9.0;
    }
}