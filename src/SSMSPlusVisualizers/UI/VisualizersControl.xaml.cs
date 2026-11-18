namespace SSMSPlusVisualizers.UI
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;

    using Microsoft.Win32;

    public partial class VisualizersControl : UserControl
    {
        public VisualizersControl()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue is VisualizersWindowVM viewModel)
                    viewModel.BrowseForApplication = BrowseForApplication;
            };
        }

        private string BrowseForApplication(string current)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Application opening the file",
                Filter = "Applications (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*",
                CheckFileExists = true,
            };

            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(current ?? string.Empty);
                if (File.Exists(expanded))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(expanded);
                    dialog.FileName = Path.GetFileName(expanded);
                }
            }
            catch (ArgumentException)
            {
                // not a path
            }

            return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FileName : null;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
