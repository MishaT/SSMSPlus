namespace SSMSPlusVisualizers.UI
{
    using Microsoft.VisualStudio.PlatformUI;

    using SSMSPlusCore.Di;

    public partial class VisualizersWindow : DialogWindow
    {
        public VisualizersWindow()
        {
            InitializeComponent();
            var viewModel = ServiceLocator.GetRequiredService<VisualizersWindowVM>();
            DataContext = viewModel;
            viewModel.Load();
        }
    }
}
