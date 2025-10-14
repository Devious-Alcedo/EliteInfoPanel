using System;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class SettingsStorage : ISettingsStorage
    {
        private readonly IGameFilesService _files;
        public SettingsStorage(IGameFilesService files) => _files = files;

        public T Load<T>(string fileName) where T : class, new()
        {
            return _files.ReadAppDataJson<T>(fileName) ?? new T();
        }

        public void Save<T>(string fileName, T instance)
        {
            _files.WriteAppDataJson(fileName, instance);
        }
    }
}
