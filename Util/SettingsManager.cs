using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    public static class SettingsManager
    {
        #region Private Fields

        private const string SettingsFile = "settings.json";

        #endregion Private Fields

        #region Public Methods

        public static AppSettings Load()
        {
            try
            {
                var files = EliteInfoPanel.App.Services?.GetService(typeof(EliteInfoPanel.Core.Services.IGameFilesService)) as EliteInfoPanel.Core.Services.IGameFilesService;
                if (files != null)
                {
                    return files.ReadAppDataJson<AppSettings>(SettingsFile) ?? new AppSettings();
                }

                // Fallback before DI is initialized
                var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EliteInfoPanel", SettingsFile);
                if (!File.Exists(defaultPath)) return new AppSettings();
                var json = File.ReadAllText(defaultPath);
                return string.IsNullOrWhiteSpace(json) ? new AppSettings() : (JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings());
            }
            catch { return new AppSettings(); }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                var files = EliteInfoPanel.App.Services?.GetService(typeof(EliteInfoPanel.Core.Services.IGameFilesService)) as EliteInfoPanel.Core.Services.IGameFilesService;
                if (files != null)
                {
                    files.WriteAppDataJson(SettingsFile, settings);
                }
                else
                {
                    var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EliteInfoPanel", SettingsFile);
                    var dir = Path.GetDirectoryName(defaultPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(defaultPath, json);
                }
            }
            catch { }
        }

        #endregion Public Methods
    }
}
