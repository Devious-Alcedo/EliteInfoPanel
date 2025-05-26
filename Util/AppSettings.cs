using EliteInfoPanel.Core;
using System;
using System.Windows;

namespace EliteInfoPanel.Util
{
    public class AppSettings
    {
        // MQTT Configuration
        public bool MqttEnabled { get; set; } = false;
        public string MqttBrokerHost { get; set; } = "localhost";
        public int MqttBrokerPort { get; set; } = 1883;
        public string MqttUsername { get; set; } = "";
        public string MqttPassword { get; set; } = "";
        public string MqttClientId { get; set; } = "EliteInfoPanel";
        public string MqttTopicPrefix { get; set; } = "elite/status";
        public bool MqttUseTls { get; set; } = false;
        public int MqttQosLevel { get; set; } = 0; // 0 = At most once, 1 = At least once, 2 = Exactly once
        public bool MqttRetainMessages { get; set; } = true;
        public int MqttPublishIntervalMs { get; set; } = 1000; // Rate limiting
        public bool MqttPublishOnlyChanges { get; set; } = true; // Only publish when flags change



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