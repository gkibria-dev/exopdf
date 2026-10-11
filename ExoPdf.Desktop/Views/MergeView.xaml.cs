using System.Windows;
using System.Windows.Controls;

namespace ExoPdf.Desktop.Views;

public partial class MergeView : UserControl
{
    public MergeView()
    {
        InitializeComponent();
    }

    // View-only mechanics: a plain Button (not ToggleButton) opening/closing the Popup
    // avoids the classic WPF toggle-vs-popup-autoclose flicker on a second click.
    private void RecentButton_Click(object sender, RoutedEventArgs e) => RecentPopup.IsOpen = true;

    private void RecentItem_Click(object sender, RoutedEventArgs e) => RecentPopup.IsOpen = false;
}
