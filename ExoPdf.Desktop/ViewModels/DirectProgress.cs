namespace ExoPdf.Desktop.ViewModels;

/// <summary>
/// An <see cref="IProgress{T}"/> that calls its handler immediately on the reporting
/// thread. Unlike <see cref="Progress{T}"/> it does not post to a synchronization
/// context, so reports are delivered in order and can be tested without a UI thread.
/// </summary>
internal sealed class DirectProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
