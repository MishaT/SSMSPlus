namespace SSMSPlusMacros.Services
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Json;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.App;

    using SSMSPlusMacros.Entities;

    /// <summary>Current macros, persisted in macros.json in the SSMS Plus working directory.</summary>
    public class MacroStore
    {
        public const int MaxMacros = 10;

        private readonly ILogger<MacroStore> _logger;
        private readonly string _filePath;

        public MacroSettings Settings { get; private set; } = new MacroSettings();

        public MacroStore(IWorkingDirProvider workingDirProvider, ILogger<MacroStore> logger)
        {
            _logger = logger;
            _filePath = Path.Combine(workingDirProvider.GetWorkingDir(), "macros.json");
        }

        public void Load()
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                using (var stream = File.OpenRead(_filePath))
                {
                    var settings = (MacroSettings)new DataContractJsonSerializer(typeof(MacroSettings)).ReadObject(stream);
                    settings.Macros = settings.Macros ?? new System.Collections.Generic.List<MacroDefinition>();
                    settings.DisplacedBindings = settings.DisplacedBindings ?? new System.Collections.Generic.List<DisplacedBinding>();
                    Settings = settings;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read the macros from {FilePath}", _filePath);
            }
        }

        public void Save(MacroSettings settings)
        {
            var tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, System.Text.Encoding.UTF8, ownsStream: false, indent: true))
            {
                new DataContractJsonSerializer(typeof(MacroSettings)).WriteObject(writer, settings);
            }

            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, null);
            else
                File.Move(tempPath, _filePath);

            Settings = settings;
        }
    }
}
