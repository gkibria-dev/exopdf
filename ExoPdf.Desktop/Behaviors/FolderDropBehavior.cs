using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ExoPdf.Desktop.Behaviors;

/// <summary>
/// Lets a view accept a dropped folder (or a file, in which case its folder is used)
/// and hands the path to a command, so the view needs no code-behind.
/// </summary>
public static class FolderDropBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(FolderDropBehavior),
        new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        element.AllowDrop = e.NewValue is not null;
        element.DragOver -= OnDragOver;
        element.Drop -= OnDrop;

        if (e.NewValue is not null)
        {
            element.DragOver += OnDragOver;
            element.Drop += OnDrop;
        }
    }

    // DragOver fires many times a second, so it must stay cheap: no file-system probing
    // here. Whether the dropped item is a usable folder is decided once, on Drop.
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var command = GetCommand((DependencyObject)sender);
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && command?.CanExecute(null) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        var command = GetCommand((DependencyObject)sender);
        if (TryGetFolder(e.Data) is { } folder && command?.CanExecute(folder) == true)
            command.Execute(folder);

        e.Handled = true;
    }

    private static string? TryGetFolder(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
            return null;

        var path = paths[0];
        if (Directory.Exists(path))
            return path;

        return File.Exists(path) ? Path.GetDirectoryName(path) : null;
    }
}
