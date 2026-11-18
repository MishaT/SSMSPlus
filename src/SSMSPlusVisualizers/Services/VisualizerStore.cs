namespace SSMSPlusVisualizers.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Json;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.App;

    using SSMSPlusVisualizers.Entities;

    /// <summary>The "Show As" visualizers, persisted in visualizers.json in the SSMS Plus working directory.</summary>
    public class VisualizerStore
    {
        /// <summary>The number of "Show As" menu entries.</summary>
        public const int MaxVisualizers = 10;

        private readonly ILogger<VisualizerStore> _logger;
        private readonly string _filePath;
        private List<VisualizerDefinition> _visualizers;

        public VisualizerStore(IWorkingDirProvider workingDirProvider, ILogger<VisualizerStore> logger)
        {
            _logger = logger;
            _filePath = Path.Combine(workingDirProvider.GetWorkingDir(), "visualizers.json");
        }

        /// <summary>Read on first use; the defaults until the user saves a list (even empty).</summary>
        public IReadOnlyList<VisualizerDefinition> Visualizers => _visualizers ?? (_visualizers = Load());

        public void Save(IEnumerable<VisualizerDefinition> visualizers)
        {
            var settings = new VisualizerSettings { Visualizers = visualizers.Select(v => v.Clone()).ToList() };
            var tempPath = _filePath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            using (var stream = File.Create(tempPath))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, System.Text.Encoding.UTF8, ownsStream: false, indent: true))
            {
                new DataContractJsonSerializer(typeof(VisualizerSettings)).WriteObject(writer, settings);
            }

            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, null);
            else
                File.Move(tempPath, _filePath);

            _visualizers = settings.Visualizers;
        }

        private List<VisualizerDefinition> Load()
        {
            if (!File.Exists(_filePath))
                return VisualizerDefinition.Defaults();

            try
            {
                using (var stream = File.OpenRead(_filePath))
                {
                    var settings = (VisualizerSettings)new DataContractJsonSerializer(typeof(VisualizerSettings)).ReadObject(stream);
                    return (settings?.Visualizers ?? new List<VisualizerDefinition>()).Where(v => v != null).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read the visualizers from {FilePath}, using the defaults", _filePath);
                return VisualizerDefinition.Defaults();
            }
        }
    }
}
