namespace SSMSPlusMacros.UI
{
    using Microsoft.VisualStudio.PlatformUI;

    using SSMSPlusCore.Di;

    /// <summary>
    /// Modal SSMS dialog hosting <see cref="MacrosControl"/>. DialogWindow (not Window) so that SSMS
    /// does not handle the keys pressed in the dialog, e.g. F5 while recording a shortcut.
    /// </summary>
    public partial class MacrosWindow : DialogWindow
    {
        public MacrosWindow()
        {
            InitializeComponent();
            var viewModel = ServiceLocator.GetRequiredService<MacrosWindowVM>();
            DataContext = viewModel;
            viewModel.Load();
        }
    }
}
