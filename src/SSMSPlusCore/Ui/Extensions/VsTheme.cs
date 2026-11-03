namespace SSMSPlusCore.Ui.Extensions
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;

    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// Applies the SSMS themed control styles (VsResourceKeys) as implicit styles of the element.
    /// Styles are looked up at runtime so that the UI still loads outside SSMS (Demo project).
    /// </summary>
    public static class VsTheme
    {
        private static Tuple<Type, object>[] GetThemedStyles()
        {
            return new[]
            {
                Tuple.Create(typeof(Button), VsResourceKeys.ButtonStyleKey),
                Tuple.Create(typeof(CheckBox), VsResourceKeys.CheckBoxStyleKey),
                Tuple.Create(typeof(TextBox), VsResourceKeys.TextBoxStyleKey),
                Tuple.Create(typeof(ScrollBar), VsResourceKeys.ScrollBarStyleKey),
            };
        }

        public static bool GetUseVsTheme(FrameworkElement element)
        {
            return (bool)element.GetValue(UseVsThemeProperty);
        }

        public static void SetUseVsTheme(FrameworkElement element, bool value)
        {
            element.SetValue(UseVsThemeProperty, value);
        }

        public static readonly DependencyProperty UseVsThemeProperty =
            DependencyProperty.RegisterAttached("UseVsTheme", typeof(bool), typeof(VsTheme), new PropertyMetadata(false, OnUseVsThemeChanged));

        private static void OnUseVsThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                // wait for the XAML Resources of the element to be set, they would replace ours
                if (element.IsInitialized)
                    ApplyThemedStyles(element);
                else
                    element.Initialized += (s, a) => ApplyThemedStyles(element);
            }
        }

        private static void ApplyThemedStyles(FrameworkElement element)
        {
            var themedStyles = new ResourceDictionary();
            foreach (var themedStyle in GetThemedStyles())
            {
                // the keys are only set when running inside the VS shell
                if (themedStyle.Item2 != null && Application.Current?.TryFindResource(themedStyle.Item2) is Style vsStyle)
                {
                    themedStyles[themedStyle.Item1] = new Style(themedStyle.Item1, vsStyle);
                }
            }

            if (themedStyles.Count > 0)
            {
                element.Resources.MergedDictionaries.Add(themedStyles);
            }
        }
    }
}
