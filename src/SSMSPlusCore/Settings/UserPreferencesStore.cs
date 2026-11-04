namespace SSMSPlusCore.Settings
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Json;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.App;

    /// <summary>The user preferences, persisted in preferences.json in the SSMS Plus working directory.</summary>
    public class UserPreferencesStore
    {
        private readonly ILogger<UserPreferencesStore> _logger;
        private readonly string _filePath;
        private UserPreferences _preferences;

        public UserPreferencesStore(IWorkingDirProvider workingDirProvider, ILogger<UserPreferencesStore> logger)
        {
            _logger = logger;
            _filePath = Path.Combine(workingDirProvider.GetWorkingDir(), "preferences.json");
        }

        /// <summary>The current preferences, read from the file on first use. Do not modify, use <see cref="Save"/>.</summary>
        public UserPreferences Preferences => _preferences ?? (_preferences = Load());

        public void Save(UserPreferences preferences)
        {
            var tempPath = _filePath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            using (var stream = File.Create(tempPath))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, System.Text.Encoding.UTF8, ownsStream: false, indent: true))
            {
                new DataContractJsonSerializer(typeof(UserPreferences)).WriteObject(writer, preferences);
            }

            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, null);
            else
                File.Move(tempPath, _filePath);

            _preferences = preferences.Clone();
        }

        private UserPreferences Load()
        {
            if (!File.Exists(_filePath))
                return new UserPreferences();

            try
            {
                using (var stream = File.OpenRead(_filePath))
                {
                    return (UserPreferences)new DataContractJsonSerializer(typeof(UserPreferences)).ReadObject(stream) ?? new UserPreferences();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read the preferences from {FilePath}, using the defaults", _filePath);
                return new UserPreferences();
            }
        }
    }
}
