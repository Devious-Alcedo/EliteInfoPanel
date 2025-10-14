using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class GameFilesService : IGameFilesService
    {
        private readonly string _gamePath;
        private readonly string _appDataRoot;

        public GameFilesService(string gamePath)
        {
            _gamePath = gamePath;
            _appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EliteInfoPanel");
        }

        public string PathFor(string fileName) => System.IO.Path.Combine(_gamePath, fileName);

        public T? ReadJson<T>(string fileName) where T : class
        {
            var path = PathFor(fileName);
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length == 0) return null;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public void WriteJson<T>(string fileName, T value)
        {
            var path = PathFor(fileName);
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public string? LatestJournal() => Directory
            .GetFiles(_gamePath, "Journal.*.log")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        public string AppDataPathFor(string fileName)
        {
            return Path.Combine(_appDataRoot, fileName);
        }

        public T? ReadAppDataJson<T>(string fileName) where T : class
        {
            var path = AppDataPathFor(fileName);
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length == 0) return null;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public void WriteAppDataJson<T>(string fileName, T value)
        {
            var path = AppDataPathFor(fileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }
}
