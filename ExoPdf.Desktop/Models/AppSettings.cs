namespace ExoPdf.Desktop.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// Desktop settings persisted between launches. Add new settings as properties with
/// defaults; older settings files that lack them keep working.
/// </summary>
public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string? LastMergeFolder { get; set; }
}
