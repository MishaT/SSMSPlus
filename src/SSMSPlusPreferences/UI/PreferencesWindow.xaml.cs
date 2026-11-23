using Microsoft.VisualStudio.PlatformUI;
using SSMSPlusCore.Di;
using SSMSPlusCore.Ui;
using System.Windows.Automation.Peers;
using System.Windows.Input;

namespace SSMSPlusPreferences.UI
{
    /// <summary>The About dialog, modal over SSMS: Esc closes it and the focus goes back to SSMS.</summary>
    public partial class PreferencesWindow : DialogWindow
    {
        public PreferencesWindow()
        {
            InitializeComponent();
            var viewModel = ServiceLocator.GetRequiredService<PreferencesWindowVM>();
            this.DataContext = viewModel;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new EmptyAutomationPeer(this);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // handled here, otherwise SSMS takes Esc and activates another window
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            base.OnPreviewKeyDown(e);
        }
    }
}