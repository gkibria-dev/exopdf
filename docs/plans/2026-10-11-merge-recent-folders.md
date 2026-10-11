# Plan: Replace last-folder auto-restore with a Recents list

**Date:** 2026-10-11
**Branch:** feature/merge-recent-folders
**Related requirements:** DUI-21, DUI-15, DUI-22
**Requirements commit:** f068b82 (DUI-21 is amended on this branch, not yet committed)

## Goal

Stop the Merge view from touching the file system automatically on launch, and let the
user reopen one of several recently used folders by explicit choice instead of just one,
auto-applied.

## Context

`LastMergeFolder` is a single remembered path that `MergeViewModel.InitializeAsync()`
reloads automatically after the window appears (DUI-21, DUI-25). The user reported that
an unreachable `LastMergeFolder` throws in the Desktop app. Reading `LoadFolderAsync`'s
`restoring` branch, an `ExoPdfException` (including `SourceFolderNotFoundException`) is
already caught and dropped quietly — but `MergeSourceFinder.Find` calls
`fileSystem.Directory.Exists` and `GetFiles` outside/around that narrow catch, so any
exception type other than `IOException`/`UnauthorizedAccessException`/`ExoPdfException`
(for example from a malformed or unreachable UNC path) is not wrapped and escapes
`LoadFolderAsync`'s catch, reaching the generic unhandled-error dialog (DUI-N7) instead of
being treated as "the folder is gone, say nothing."

Rather than widen that catch, we discussed the UX directly: auto-reopening a folder at
startup is a surprise the user doesn't need, and hardening it just keeps the surprise
alive with a narrower blast radius. The user preferred to drop automatic restore entirely
and replace it with an explicit "Recents" picker — convenience without the silent
file-system access at launch.

## Alternatives considered

| Option | Reason rejected |
|--------|----------------|
| Widen the `restoring` catch in `LoadFolderAsync` to `Exception` | Fixes the crash but keeps unexplained background file I/O at every launch, and keeps the single-folder design the user wants to move away from. |
| Keep single `LastMergeFolder`, just add a "Load last folder" button | Simplest change, but loses history beyond one folder; the user explicitly wants a small history, not just a safer version of the same single slot. |
| Store `RecentMergeFolders` but still auto-restore the newest one at launch | Removes the crash risk (same hardening either way) but keeps the silent-restore UX the user wants gone. |
| A custom flyout/popup control for the Recents list | More visual control, but needs either code-behind or a new attached behavior; this codebase keeps Views free of code-behind logic (DUI-N1) and already avoids that for simpler cases (`FolderDropBehavior`). A bindable `ComboBox` reaches the same result with no new behavior. |

## Chosen approach

Drop automatic restore. Replace the single remembered folder with a capped, explicit
Recents list in the Merge view.

- **`AppSettings`**: replace `string? LastMergeFolder` with
  `IReadOnlyList<string> RecentMergeFolders { get; init; } = []` (most recent first).
  Old settings files simply deserialize `RecentMergeFolders` to the default empty list
  (DUI-22 "missing properties fall back to defaults" already covers this — no migration
  code needed).
- **`MergeViewModel`**:
  - `InitializeAsync()` no longer calls `LoadFolderAsync`. It only copies
    `_settings.Current.RecentMergeFolders` into a new `ObservableCollection<string>
    RecentFolders` property, so nothing touches the file system at launch (closes the
    crash path by removing the triggering call, not by catching more).
  - `LoadFolderAsync` drops its `restoring` parameter — every call is now a direct user
    action (Browse, drop, or picking a Recent), so every call shows errors the same way
    (DUI-15). The quiet-drop branch is deleted.
  - `SelectFolderAsync` (Browse / drag-drop), on a successful listing, calls a new
    `RememberFolder(path)` helper instead of writing `LastMergeFolder` directly:
    it moves `path` to the front of `RecentMergeFolders` (case-insensitive de-dup, no
    other path normalization), caps the list at `MaxRecentFolders = 5`, saves it via
    `_settings.Update`, and refreshes `RecentFolders`.
  - A new `[RelayCommand(CanExecute = nameof(CanChangeFolder), AllowConcurrentExecutions
    = true)] OpenRecentFolderAsync(string path)` calls `LoadFolderAsync(path)`:
    - on success, calls `RememberFolder(path)` (refreshes its position to the front);
    - on failure, removes `path` from `RecentMergeFolders` and saves, so a stale shortcut
      self-heals instead of staying in the list forever. `ErrorMessage` is already set by
      `LoadFolderAsync`, same as a failed Browse/drop (DUI-15) — no silent failure.
  - `HasRecentFolders` (`RecentFolders.Count > 0`) is exposed for the view, with its
    `PropertyChanged` raised whenever `RecentFolders` is refreshed.
- **`MergeView.xaml`**: a `ComboBox` next to the "Browse…" button, `ItemsSource="{Binding
  RecentFolders}"`, `Visibility` bound to `HasRecentFolders`, `IsEnabled` bound to
  `CanChangeFolder`, and `SelectedItem` two-way bound to a new `SelectedRecentFolder`
  observable property on the ViewModel. A partial `OnSelectedRecentFolderChanged` hook
  (generated by `[ObservableProperty]`) resets the selection to `null` and fires
  `OpenRecentFolderCommand`, so no code-behind or new attached behavior is needed and
  picking the same folder twice in a row still works. Each item shows the full folder
  path (no shortening) — consistent with how `SourceFolder`'s tooltip already shows the
  full path; shortening is a separate, optional visual improvement, not required here.

## Steps

1. Requirements: amend DUI-21 to describe the Recents list and drop the auto-launch
   restore (done, uncommitted).
2. `AppSettings`: replace `LastMergeFolder` with `RecentMergeFolders`.
3. `MergeViewModel`: remove the `restoring` path, add `RecentFolders`,
   `SelectedRecentFolder`, `HasRecentFolders`, `RememberFolder`, and
   `OpenRecentFolderCommand`; update `InitializeAsync` and `SelectFolderAsync`.
4. `MergeView.xaml`: add the Recents `ComboBox`.
5. Tests in `ExoPdf.Tests`:
   - `JsonSettingsStoreTests` / `SettingsServiceTests`: rename `LastMergeFolder` cases to
     `RecentMergeFolders` (list round-trips, missing property falls back to `[]`).
   - `MergeViewModelTests`: replace the "start-up restore" block (the tests built around
     `InitializeAsync` calling `LoadFolderAsync`) with:
     - `Initialize` only populates `RecentFolders` from settings and touches the file
       system zero times (`_finder.FindCount == 0`), covering the original crash report
       directly — nothing runs at launch that could throw.
     - Selecting a folder (Browse/drop) puts it at the front of `RecentMergeFolders`,
       capped at 5, de-duplicated case-insensitively, and moves an existing entry to the
       front instead of duplicating it.
     - `OpenRecentFolderCommand` with a folder that still lists: loads it and keeps it at
       the front of Recents.
     - `OpenRecentFolderCommand` with a folder that no longer lists: shows the same error
       as a failed Browse (DUI-15) and removes it from `RecentMergeFolders`.
6. `dotnet test`, then `/review` and `/security-review`; fix findings.
7. Update `docs/architecture.md` only if something structural changed beyond the two
   files above (not expected).
8. Commit and open a PR that links this plan.

## Verification

- `dotnet test ExoPdf.Tests\ExoPdf.Tests.csproj` passes.
- Manual, in the Desktop app:
  - Merge a folder, restart the app: the window opens with nothing loaded and no delay;
    the folder appears in the Recents list.
  - Merge a few different folders: Recents shows up to 5, most recent first, no
    duplicates.
  - Delete or rename a folder that's in Recents, pick it from the list: the existing
    "folder not found" error appears and the entry disappears from Recents; the app does
    not crash.

## Out of scope

- Path normalization beyond case-insensitive de-dup (trailing slashes, short vs. long
  path forms, UNC vs. mapped drive).
- Shortening or eliding long paths in the Recents list.
- Reordering/removing individual Recents entries by hand (e.g. a "Clear" action).
- Any change to CLI behaviour; `LastMergeFolder`/`RecentMergeFolders` is Desktop-only.
