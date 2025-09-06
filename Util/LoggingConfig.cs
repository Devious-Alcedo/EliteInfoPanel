using Serilog.Sinks.File;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    public static class LoggingConfig
    {
        #region Public Fields

        public static string? logFileFullPath;
        private static LogLevel _currentLevel = LogLevel.Information;

        #endregion Public Fields

        #region Public Methods

        public static void Configure(LogLevel logLevel)
        {
            _currentLevel = logLevel;
            var filePathHook = new CaptureFilePathHook();
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folderPath = Path.Combine(appDataFolder, "EliteInfoPanel");
            var logFilePath = Path.Combine(folderPath, "EliteInfoPanel_Log.log");

            var level = logLevel switch
            {
                LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
                LogLevel.Information => Serilog.Events.LogEventLevel.Information,
                LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
                LogLevel.Error => Serilog.Events.LogEventLevel.Error,
                LogLevel.Fatal => Serilog.Events.LogEventLevel.Fatal,
                _ => Serilog.Events.LogEventLevel.Information
            };

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(logFilePath,
                              rollingInterval: RollingInterval.Day,
                              fileSizeLimitBytes: 10_000_000,
                              retainedFileCountLimit: 1,
                              hooks: filePathHook,
                              rollOnFileSizeLimit: true)
                .CreateLogger();

            logFileFullPath = filePathHook.Path;
        }

        public static void ReloadLogLevel(LogLevel logLevel)
        {
            if (logLevel != _currentLevel)
            {
                Configure(logLevel);
            }
        }


        #endregion Public Methods
    }


    internal class CaptureFilePathHook : FileLifecycleHooks
    {
        #region Public Properties

        public string? Path { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
        {
            Path = path;
            return base.OnFileOpened(path, underlyingStream, encoding);
        }

        #endregion Public Methods
    }
}
