namespace SSMSPlusWarnings.UI
{
    using System.Windows;
    using System.Windows.Controls;

    public partial class ActionGuardControl : UserControl
    {
        public ActionGuardControl()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if ((DataContext as ActionGuardVM)?.Save() != false)
                Window.GetWindow(this)?.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
