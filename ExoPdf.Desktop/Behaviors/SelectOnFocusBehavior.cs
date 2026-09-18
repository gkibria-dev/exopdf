using System.Windows;
using System.Windows.Controls.Primitives;

namespace ExoPdf.Desktop.Behaviors;

/// <summary>
/// Checks a toggle button as soon as it receives keyboard focus. WPF only does this for
/// radio buttons that share a parent panel; the sidebar's entries live in separate
/// lists, so without this the arrow keys would move focus without selecting the page.
/// </summary>
public static class SelectOnFocusBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SelectOnFocusBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton button)
            return;

        button.GotKeyboardFocus -= OnGotKeyboardFocus;
        if ((bool)e.NewValue)
            button.GotKeyboardFocus += OnGotKeyboardFocus;
    }

    private static void OnGotKeyboardFocus(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: not true } button)
            button.IsChecked = true;
    }
}
