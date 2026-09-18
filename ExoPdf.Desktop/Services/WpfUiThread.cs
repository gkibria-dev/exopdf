using System.Windows;

namespace ExoPdf.Desktop.Services;

public sealed class WpfUiThread : IUiThread
{
    // A null dispatcher means the application is shutting down; a late report is dropped.
    public void Post(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
}
