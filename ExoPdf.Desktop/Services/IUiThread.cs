namespace ExoPdf.Desktop.Services;

/// <summary>Runs work on the UI thread, so a ViewModel can be updated from a background thread without referencing WPF.</summary>
public interface IUiThread
{
    /// <summary>Queues <paramref name="action"/> for the UI thread and returns without waiting. Actions run in the order they were posted.</summary>
    void Post(Action action);
}
