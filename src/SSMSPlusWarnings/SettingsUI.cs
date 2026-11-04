namespace SSMSPlusWarnings
{
    using System.ComponentModel.Design;

    using Microsoft.VisualStudio.Shell;

    using SSMSPlusCore.Integration;

    using SSMSPlusWarnings.UI;

    /// <summary>SSMS Plus > Settings > Action Guard... and Long Executions...</summary>
    public class SettingsUI
    {
        public const int ActionGuardCommandId = 3101;
        public const int LongExecutionsCommandId = 3102;

        private readonly PackageProvider _packageProvider;

        public SettingsUI(PackageProvider packageProvider)
        {
            _packageProvider = packageProvider;
        }

        public void Register()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // DialogWindow: modal on the SSMS main window
            _packageProvider.CommandService.AddCommand(new MenuCommand((s, e) => new ActionGuardWindow().ShowModal(), new CommandID(MenuHelper.CommandSet, ActionGuardCommandId)));
            _packageProvider.CommandService.AddCommand(new MenuCommand((s, e) => new LongExecutionWindow().ShowModal(), new CommandID(MenuHelper.CommandSet, LongExecutionsCommandId)));
        }
    }
}
