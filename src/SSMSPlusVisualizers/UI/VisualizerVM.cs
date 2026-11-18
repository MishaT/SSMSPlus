namespace SSMSPlusVisualizers.UI
{
    using SSMSPlusCore.Ui;

    using SSMSPlusVisualizers.Entities;

    public class VisualizerVM : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        private string _extension;
        public string Extension
        {
            get => _extension;
            set => SetField(ref _extension, value);
        }

        private string _application;
        public string Application
        {
            get => _application;
            set => SetField(ref _application, value);
        }

        private string _arguments;
        public string Arguments
        {
            get => _arguments;
            set => SetField(ref _arguments, value);
        }

        public static VisualizerVM From(VisualizerDefinition definition)
        {
            return new VisualizerVM
            {
                Name = definition.Name,
                Extension = definition.Extension,
                Application = definition.Application,
                Arguments = string.IsNullOrWhiteSpace(definition.Arguments) ? VisualizerDefinition.DefaultArguments : definition.Arguments,
            };
        }

        public VisualizerDefinition ToDefinition()
        {
            bool hasApplication = !string.IsNullOrWhiteSpace(Application);
            return new VisualizerDefinition
            {
                Name = (Name ?? string.Empty).Trim(),
                Extension = Services.VisualizerRunner.NormalizeExtension(Extension),
                Application = hasApplication ? Application.Trim().Trim('"') : null,
                // only used with an application
                Arguments = hasApplication && !string.IsNullOrWhiteSpace(Arguments) ? Arguments.Trim() : null,
            };
        }
    }
}
