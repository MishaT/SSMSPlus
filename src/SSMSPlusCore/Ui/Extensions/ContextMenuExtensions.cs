namespace SSMSPlusCore.Ui.Extensions
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;

    public static class ContextMenuExtensions
    {
        public static bool GetOpenOnClick(ButtonBase button)
        {
            return (bool)button.GetValue(OpenOnClickProperty);
        }

        public static void SetOpenOnClick(ButtonBase button, bool value)
        {
            button.SetValue(OpenOnClickProperty, value);
        }

        /// <summary>
        /// Opens the ContextMenu of the button below it on left click (drop down button).
        /// </summary>
        public static readonly DependencyProperty OpenOnClickProperty =
            DependencyProperty.RegisterAttached("OpenOnClick", typeof(bool), typeof(ContextMenuExtensions), new PropertyMetadata(false, OnOpenOnClickChanged));

        private static void OnOpenOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ButtonBase button)
            {
                button.Click -= Button_Click;
                if ((bool)e.NewValue)
                {
                    button.Click += Button_Click;
                }
            }
        }

        private static void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = (ButtonBase)sender;
            var menu = button.ContextMenu;
            if (menu == null)
                return;

            menu.DataContext = button.DataContext;
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
