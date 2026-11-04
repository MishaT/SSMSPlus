namespace SSMSPlusWarnings.UI
{
    using Microsoft.VisualStudio.PlatformUI;

    using SSMSPlusCore.Di;

    public partial class LongExecutionWindow : DialogWindow
    {
        public LongExecutionWindow()
        {
            InitializeComponent();
            DataContext = ServiceLocator.GetRequiredService<LongExecutionVM>();
        }
    }
}
