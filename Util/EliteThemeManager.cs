using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
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
            Log.Information("EliteThemeManager: Initializing...");
            _graphicsConfigPath = FindEliteGraphicsConfigPath();

            // Only proceed if we found the config file
            if (!string.IsNullOrEmpty(_graphicsConfigPath))
            {
                Log.Information("EliteThemeManager: Found config at {Path}", _graphicsConfigPath);
                LoadColors();
                SetupFileWatcher();
            }
            else
            {
                Log.Warning("EliteThemeManager: Could not find Elite Dangerous graphics configuration file, using defaults");
                _currentColors = _colorExtractor.ExtractColors();
                LogCurrentColors("Default");
            }
            Log.Information("MainWindow: Theme manager initialized");
        }

        public EliteHudColors GetCurrentColors()
        {
            if (_currentColors == null)
            {
                Log.Information("EliteThemeManager: Getting colors on demand...");
                _currentColors = _colorExtractor.ExtractColors();
                LogCurrentColors("On-Demand");
            }
            return _currentColors;
        }

        private void LoadColors()
        {
            Log.Information("EliteThemeManager: Loading colors from config...");
            _currentColors = _colorExtractor.ExtractColors();
            LogCurrentColors("Loaded");
            ApplyColorsToApplication();
        }

        private void ReloadColors()
        {
            Log.Information("EliteThemeManager: Reloading colors due to file change...");
            LoadColors();
            ColorsChanged?.Invoke(_currentColors);
        }

        private void LogCurrentColors(string prefix)
        {
            if (_currentColors == null)
            {
                Log.Warning("EliteThemeManager: Current colors are null");
                return;
            }

            Log.Information("EliteThemeManager: {Prefix} colors:", prefix);
            Log.Information("  HudMain: {Color}", _currentColors.HudMain);
            Log.Information("  HudSecondary: {Color}", _currentColors.HudSecondary);
            Log.Information("  HudText: {Color}", _currentColors.HudText);
            Log.Information("  HudBackground: {Color}", _currentColors.HudBackground);
            Log.Information("  HudWarning: {Color}", _currentColors.HudWarning);
            Log.Information("  HudSuccess: {Color}", _currentColors.HudSuccess);
        }

        private string FindEliteGraphicsConfigPath()
        {
            Log.Information("EliteThemeManager: Searching for Elite graphics config...");
            
            try
            {
                // First, try to find the running EliteDangerous64.exe process
                var eliteProcess = Process.GetProcesses()
                    .FirstOrDefault(p =>
                        p.ProcessName.Contains("EliteDangerous64", StringComparison.OrdinalIgnoreCase) ||
                        p.ProcessName.Contains("EDLaunch", StringComparison.OrdinalIgnoreCase));

                if (eliteProcess != null)
                {
                    Log.Information("EliteThemeManager: Found Elite process: {ProcessName}", eliteProcess.ProcessName);
                    
                    string executablePath = null;
                    try
                    {
                        executablePath = eliteProcess.MainModule?.FileName;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "EliteThemeManager: Could not access process module path");
                    }

                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        var directory = Path.GetDirectoryName(executablePath);
                        var graphicsConfigPath = Path.Combine(directory, "GraphicsConfiguration.xml");

                        if (File.Exists(graphicsConfigPath))
                        {
                            Log.Information("EliteThemeManager: Found config at: {Path}", graphicsConfigPath);
                            return graphicsConfigPath;
                        }
                    }
                }

                // Fallback: Check common installation locations
                Log.Information("EliteThemeManager: Checking common install locations...");
                var commonPaths = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        @"Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        @"Epic Games\EliteDangerous\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Frontier\EDLaunch\Products\elite-dangerous-odyssey-64\GraphicsConfiguration.xml"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Frontier Developments\Elite Dangerous\Options\Graphics\GraphicsConfiguration.xml")
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        Log.Information("EliteThemeManager: Found config at fallback location: {Path}", path);
                        return path;
                    }
                }

                Log.Warning("EliteThemeManager: Could not find GraphicsConfiguration.xml in any expected location");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EliteThemeManager: Error finding Elite graphics config path");
                return string.Empty;
            }
        }

        private void SetupFileWatcher()
        {
            try
            {
                if (string.IsNullOrEmpty(_graphicsConfigPath) || !File.Exists(_graphicsConfigPath))
                {
                    Log.Warning("EliteThemeManager: Cannot setup file watcher - graphics config not found");
                    return;
                }

                var directory = Path.GetDirectoryName(_graphicsConfigPath);
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
                    Log.Information("EliteThemeManager: Graphics config file changed");
                    timer.Stop();
                    timer.Start();
                };

                _configWatcher.EnableRaisingEvents = true;
                Log.Information("EliteThemeManager: File watcher set up successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EliteThemeManager: Failed to set up graphics config watcher");
            }
        }

        private void ApplyColorsToApplication()
        {
            try
            {
                Log.Information("EliteThemeManager: Applying colors to application...");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var resources = Application.Current.Resources;

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

                    Log.Information("EliteThemeManager: Colors applied successfully");
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EliteThemeManager: Error applying colors to application");
            }
        }

        // Add a dispose method to clean up the file watcher
        public void Dispose()
        {
            Log.Information("EliteThemeManager: Disposing...");
            _configWatcher?.Dispose();
        }
    }
}