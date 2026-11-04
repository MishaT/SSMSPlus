namespace SSMSPlusWarnings.UI
{
    using System.Windows;
    using System.Windows.Controls;

    public partial class LongExecutionControl : UserControl
    {
        public LongExecutionControl()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if ((DataContext as LongExecutionVM)?.Save() != false)
                Window.GetWindow(this)?.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
