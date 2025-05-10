using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EliteInfoPanel.Util
{
    public class EliteThemeManager
    {
        private static EliteThemeManager _instance;
        public static EliteThemeManager Instance => _instance ??= new EliteThemeManager();

        private readonly EliteHudColorExtractor _colorExtractor = new();
        private EliteHudColors _currentColors;

        public event Action<EliteHudColors> ColorsChanged;

        private EliteThemeManager()
        {
            LoadColors();

            // Monitor the graphics config file for changes
            var watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_colorExtractor.GraphicsConfigPath),
                Filter = "GraphicsConfiguration.xml",
                NotifyFilter = NotifyFilters.LastWrite
            };

            watcher.Changed += (s, e) =>
            {
                // Debounce changes
                _ = Task.Delay(500).ContinueWith(_ => ReloadColors());
            };

            watcher.EnableRaisingEvents = true;
        }

        public EliteHudColors GetCurrentColors() => _currentColors ??= _colorExtractor.ExtractColors();

        private void LoadColors()
        {
            _currentColors = _colorExtractor.ExtractColors();
            ApplyColorsToApplication();
        }

        private void ReloadColors()
        {
            LoadColors();
            ColorsChanged?.Invoke(_currentColors);
        }

        private void ApplyColorsToApplication()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var resources = App.Current.Resources;

                // Update your app's color resources
                resources["EliteHudMain"] = new SolidColorBrush(_currentColors.HudMain);
                resources["EliteHudSecondary"] = new SolidColorBrush(_currentColors.HudSecondary);
                resources["EliteHudText"] = new SolidColorBrush(_currentColors.HudText);
                resources["EliteHudBackground"] = new SolidColorBrush(_currentColors.HudBackground);
                resources["EliteHudWarning"] = new SolidColorBrush(_currentColors.HudWarning);
                resources["EliteHudSuccess"] = new SolidColorBrush(_currentColors.HudSuccess);

                // Update MaterialDesign theme colors if needed
                resources["PrimaryHueMidBrush"] = new SolidColorBrush(_currentColors.HudMain);
                resources["PrimaryHueDarkBrush"] = new SolidColorBrush(_currentColors.HudSecondary);
            });
        }
    }
}
