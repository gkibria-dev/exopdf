using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExoPdf.Core.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Desktop.ViewModels;

/// <summary>What the user sees after a successful merge, and what they can do with the output.</summary>
public partial class MergeResultViewModel : ObservableObject
{
    private readonly MergeResult _result;
    private readonly IShellLauncher _shell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLaunchError))]
    private string? _launchError;

    public MergeResultViewModel(MergeResult result, IShellLauncher shell)
    {
        _result = result;
        _shell = shell;
    }

    public string OutputFilePath => _result.OutputFilePath;

    public string Summary =>
        $"Merged {Count(_result.FilesMerged, "file")} ({Count(_result.TotalPages, "page")})";

    public bool HasLaunchError => !string.IsNullOrEmpty(LaunchError);

    [RelayCommand]
    private void Open() => Launch(() => _shell.OpenFile(OutputFilePath));

    [RelayCommand]
    private void ShowInFolder() => Launch(() => _shell.ShowInFolder(OutputFilePath));

    private void Launch(Action action)
    {
        LaunchError = null;

        try
        {
            action();
        }
        catch (ShellLaunchException ex)
        {
            LaunchError = ex.Message;
        }
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
