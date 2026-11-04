namespace SSMSPlusMacros.UI
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    using SSMSPlusMacros.Services;

    public partial class MacrosControl : UserControl
    {
        private MacrosWindowVM ViewModel => DataContext as MacrosWindowVM;

        public MacrosControl()
        {
            InitializeComponent();
        }

        /// <summary>Records the pressed keys as the shortcut of the selected macro.</summary>
        private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            // keep Tab / Shift+Tab for the focus navigation
            if (key == Key.Tab && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
                return;

            e.Handled = true;
            var macro = ViewModel?.SelectedMacro;
            if (macro == null)
                return;

            if (key == Key.Back && modifiers == ModifierKeys.None)
            {
                macro.Shortcut = null;
                return;
            }

            var shortcut = Shortcut.Format(key, modifiers);
            if (shortcut != null)
                macro.Shortcut = shortcut;
        }

        private void MatchingCommands_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.SelectedMatchingCommand != null)
                ViewModel.AddCommandCmd.Execute(null);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
