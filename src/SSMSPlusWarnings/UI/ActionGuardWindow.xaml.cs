namespace SSMSPlusWarnings.UI
{
    using Microsoft.VisualStudio.PlatformUI;

    using SSMSPlusCore.Di;

    public partial class ActionGuardWindow : DialogWindow
    {
        public ActionGuardWindow()
        {
            InitializeComponent();
            DataContext = ServiceLocator.GetRequiredService<ActionGuardVM>();
        }
    }
}
