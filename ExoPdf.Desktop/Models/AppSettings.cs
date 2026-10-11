namespace ExoPdf.Desktop.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// Desktop settings persisted between launches. Immutable: change them with
/// <c>ISettingsService.Update(s =&gt; s with { ... })</c>. Add new settings as properties
/// with defaults; older settings files that lack them keep working.
/// </summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Folders used in Merge, most recent first, capped by the ViewModel.</summary>
    public IReadOnlyList<string> RecentMergeFolders { get; init; } = [];
}
