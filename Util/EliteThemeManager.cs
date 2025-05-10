using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private string _graphicsConfigPath;
        private FileSystemWatcher _configWatcher;

        public event Action<EliteHudColors> ColorsChanged;

        private EliteThemeManager()
        {
            _graphicsConfigPath = FindEliteGraphicsConfigPath();

            // Only proceed if we found the config file
            if (!string.IsNullOrEmpty(_graphicsConfigPath))
            {
                // Update the color extractor with the found path if needed
                // Note: This assumes EliteHudColorExtractor has a way to set the path
                // If it doesn't, you may need to modify this approach

                LoadColors();
                SetupFileWatcher();
            }
            else
            {
                Log.Warning("Could not find Elite Dangerous graphics configuration file");
                // Load default colors or handle this case as appropriate
                _currentColors = _colorExtractor.ExtractColors();
            }
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

        private string FindEliteGraphicsConfigPath()
        {
            try
            {
                // First, try to find the running EliteDangerous64.exe process
                var eliteProcess = Process.GetProcesses()
                    .FirstOrDefault(p =>
                        p.ProcessName.Contains("EliteDangerous64", StringComparison.OrdinalIgnoreCase) ||
                        p.ProcessName.Contains("EDLaunch", StringComparison.OrdinalIgnoreCase));

                if (eliteProcess != null)
                {
                    string executablePath = eliteProcess.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        // Navigate from the executable path to the graphics config
                        // Example: C:\...\Elite Dangerous\Products\elite-dangerous-odyssey-64\EliteDangerous64.exe
                        // We need: C:\...\Elite Dangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml

                        var directory = Path.GetDirectoryName(executablePath);
                        var graphicsConfigPath = Path.Combine(directory, "GraphicsConfiguration.xml");

                        Log.Information("Found Elite process at: {Path}", executablePath);
                        Log.Information("Looking for graphics config at: {ConfigPath}", graphicsConfigPath);

                        if (File.Exists(graphicsConfigPath))
                        {
                            Log.Information("Found GraphicsConfiguration.xml at: {Path}", graphicsConfigPath);
                            return graphicsConfigPath;
                        }
                    }
                }

                // Fallback: Check common installation locations
                var commonPaths = new string[]
                {
                    // Steam locations
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        @"Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    
                    // Epic Games Store location
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        @"Epic Games\EliteDangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    
                    // Frontier Store location  
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Frontier\EDLaunch\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    
                    // Legacy path (pre-Odyssey)
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-64\GraphicsConfiguration.xml"),
                    
                    // User AppData location (sometimes used)
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Frontier Developments\Elite Dangerous\Options\Graphics\GraphicsConfiguration.xml")
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        Log.Information("Found GraphicsConfiguration.xml at fallback location: {Path}", path);
                        return path;
                    }
                }

                Log.Warning("Could not find GraphicsConfiguration.xml in any expected location");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error finding Elite graphics config path");
                return string.Empty;
            }
        }

        private void SetupFileWatcher()
        {
            try
            {
                if (string.IsNullOrEmpty(_graphicsConfigPath) || !File.Exists(_graphicsConfigPath))
                {
                    Log.Warning("Cannot setup file watcher - graphics config not found");
                    return;
                }

                var directory = Path.GetDirectoryName(_graphicsConfigPath);
                if (!Directory.Exists(directory))
                {
                    Log.Warning("Elite graphics config directory does not exist: {Path}", directory);
                    return;
                }

                _configWatcher = new FileSystemWatcher
                {
                    Path = directory,
                    Filter = "GraphicsConfiguration.xml",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                // Debounce changes to avoid multiple reloads
                var timer = new System.Timers.Timer(1000) { AutoReset = false };
                timer.Elapsed += (s, e) => ReloadColors();

                _configWatcher.Changed += (s, e) =>
                {
                    timer.Stop();
                    timer.Start();
                };

                _configWatcher.EnableRaisingEvents = true;
                Log.Information("Set up Elite graphics config file watcher");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set up graphics config watcher");
            }
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

        // Add a dispose method to clean up the file watcher
        public void Dispose()
        {
            _configWatcher?.Dispose();
        }
    }
}