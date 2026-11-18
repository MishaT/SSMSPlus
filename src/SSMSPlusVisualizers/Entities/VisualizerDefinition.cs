namespace SSMSPlusVisualizers.Entities
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>A "Show As" entry of the results grid: the cell is saved with <see cref="Extension"/> and opened.</summary>
    [DataContract]
    public class VisualizerDefinition
    {
        public const string FilePlaceholder = "{file}";
        public const string DefaultArguments = "\"{file}\"";

        [DataMember(Order = 1)]
        public string Name { get; set; }

        /// <summary>With the dot, for example ".png".</summary>
        [DataMember(Order = 2)]
        public string Extension { get; set; }

        /// <summary>Optional: the application opening the file. Empty: the Windows default for the extension.</summary>
        [DataMember(Order = 3)]
        public string Application { get; set; }

        /// <summary>The arguments of <see cref="Application"/>, "{file}" is replaced by the file path.</summary>
        [DataMember(Order = 4)]
        public string Arguments { get; set; }

        public VisualizerDefinition Clone()
        {
            return (VisualizerDefinition)MemberwiseClone();
        }

        public static List<VisualizerDefinition> Defaults()
        {
            return new List<VisualizerDefinition>
            {
                new VisualizerDefinition { Name = "Picture", Extension = ".png" },
                new VisualizerDefinition { Name = "Text", Extension = ".txt" },
                new VisualizerDefinition { Name = "JSON", Extension = ".json" },
                new VisualizerDefinition { Name = "XML", Extension = ".xml" },
                new VisualizerDefinition { Name = "PDF", Extension = ".pdf" },
            };
        }
    }

    [DataContract]
    public class VisualizerSettings
    {
        [DataMember(Order = 1)]
        public List<VisualizerDefinition> Visualizers { get; set; } = new List<VisualizerDefinition>();
    }
}
