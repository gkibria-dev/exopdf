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
| A bindable `ComboBox` for the Recents list | Tried first (no code-behind needed), but the user found its look out of place next to "Browse…". Replaced with an icon button. |
| A `ToggleButton` + `Popup` pair, both bound to a view-model `bool`, for the icon button's dropdown | The standard code-behind-free WPF pattern for this, but has a well-known quirk: clicking the toggle button while its popup is open can close and immediately reopen it (the popup's own outside-click handling races the button's `Click`). |

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
  - `LoadFolderAsync` returns a `LoadOutcome { Loaded, Stale, Failed }` instead of
    `bool`, so a listing superseded by a newer pick (`Stale`) is never confused with an
    actual failure (`Failed`) — see Revisions below.
- **`MergeView.xaml`**: a small icon-only `Button` ("history" glyph from `IconFont`)
  next to "Browse…", visible only when `HasRecentFolders` and disabled with
  `CanChangeFolder`, matching Browse's own enabled state. It carries a `ContextMenu`
  (`ItemsSource="{Binding RecentFolders}"`) whose `MenuItem`s show each full path as
  their `Header` and run `OpenRecentFolderCommand` with that path as
  `CommandParameter`. A one-line code-behind handler opens the `ContextMenu` on a left
  click (`RecentButton.ContextMenu.IsOpen = true`), which DUI-N1 allows as a view-only
  mechanic — it replaces the originally-planned `ComboBox` (see Revisions).

## Revisions

Made after the plan below was first implemented and reviewed, before commit:

- **UI**: the Recents `ComboBox` originally planned looked out of place next to
  "Browse…"; replaced with an icon button + `ContextMenu` (see Alternatives and Chosen
  approach above). This removed the `SelectedRecentFolder` property and its
  `OnSelectedRecentFolderChanged` hook, which existed only to drive the `ComboBox`.
- **UI, second pass**: the icon button's `ContextMenu` rendered open but narrow, with
  the Recents list not visibly showing — most likely the Fluent `ThemeMode` (ADR-005,
  already flagged experimental) not fully templating `ContextMenu`/`MenuItem` yet.
  Replaced with a self-styled `Popup` (`Card` style `Border` + `ItemsControl`, each
  entry a plain full-width `Button`) that controls its own width and row rendering
  instead of relying on that template. Opening/closing it is two one-line code-behind
  handlers (`RecentButton_Click` opens it; each item's own `Click` closes it after
  running `OpenRecentFolderCommand`) — a plain `Button`, not a `ToggleButton`, so there
  is no toggle-vs-popup-autoclose flicker on a second click. Browse was also changed to
  match Recents' icon-only style (shared `IconButton` style in `Styles.xaml`); the two
  buttons keep their existing left-to-right order (Recent, then Browse), matching this
  view's own convention of putting the primary/accent action rightmost (see the
  Merge/Cancel pair below).
- **UI, third pass**: the `Popup` itself still showed a black rectangle behind the
  `Card` border's rounded corners — its host window is opaque by default. Fixed with
  `Popup.AllowsTransparency="True"`.
- **Behaviour**: a folder was originally remembered as soon as it listed successfully
  (Browse, drop, or a Recents pick), so just browsing to a folder — without merging it
  — added it to Recents. Changed so only a *successful merge* calls `RememberFolder`
  (moved from `SelectFolderAsync` into `MergeAsync`, right after a successful
  `_merger.Merge` call). `OpenRecentFolderAsync` no longer calls `RememberFolder` either
  — opening a Recents entry still loads it, but no longer reorders the list; only
  `ForgetFolder` on a failed reopen still applies, since that is pruning a dead entry,
  not "adding" one. DUI-21 is amended again to say this explicitly.
- **Code review** (`/review`) found two bugs in the first implementation, both fixed
  before commit:
  1. `OpenRecentFolderAsync` treated a stale (superseded) listing the same as a real
     failure, so racing two Recent-folder picks could wrongly forget a perfectly good
     folder. Fixed by giving `LoadFolderAsync` a three-way `LoadOutcome` instead of
     `bool`, so only `Failed` calls `ForgetFolder`; `Stale` does nothing.
  2. `AppSettings` is a record, so its generated `Equals` compares `RecentMergeFolders`
     by reference (`List<string>` has no value equality), which silently defeated
     `SettingsService.Update`'s "nothing is saved if the settings did not change" guard
     for every Recents change. Fixed locally in `RememberFolder`/`ForgetFolder`, which
     now skip `_settings.Update` when the computed list already equals
     (`SequenceEqual`) the current one, rather than changing `SettingsService`'s
     general-purpose equality check.

## Steps

1. Requirements: amend DUI-21 to describe the Recents list and drop the auto-launch
   restore (done, uncommitted).
2. `AppSettings`: replace `LastMergeFolder` with `RecentMergeFolders`.
3. `MergeViewModel`: remove the `restoring` path, add `RecentFolders`,
   `HasRecentFolders`, `RememberFolder`, and `OpenRecentFolderCommand`; update
   `InitializeAsync` and `SelectFolderAsync`.
4. `MergeView.xaml` / `MergeView.xaml.cs`: add the Recents icon button and its
   `ContextMenu`.
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
