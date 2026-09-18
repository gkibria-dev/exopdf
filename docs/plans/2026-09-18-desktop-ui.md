# Plan: Desktop UI (shell, Merge view, Settings)

**Date:** 2026-09-18
**Branch:** feature/desktop-ui
**Related requirements:** DUI-1 to DUI-23, DUI-N1 to DUI-N6, and the Merge ordering change in `docs/requirements.md`
**Requirements commit:** a1be034

## Goal

Replace the empty Desktop scaffold with an MVVM application: a navigation shell
that hosts operations, a working Merge view, and a Settings page.

## Context

- `ExoPdf.Desktop` contains only the template `App.xaml` and an empty
  `MainWindow.xaml`.
- `docs/requirements.md` now specifies the UI (DUI-*). Merge order becomes file-name
  order, and the project targets .NET 10.
- Core, Cli and Tests still target `net8.0`; only Desktop targets `net10.0-windows`.
- `PdfMerger.Merge` is synchronous and does not validate the folder.
- ADR-004 fixes WPF + CommunityToolkit.Mvvm. ADR-005 fixes the built-in Fluent theme.
- The development machine runs Windows 10, so the Fluent theme must be checked there.

## Alternatives considered

| Option | Reason rejected |
|---|---|
| Put ViewModels in a separate `ExoPdf.Desktop.ViewModels` project | Extra project for no current benefit. ViewModels stay in Desktop and reference no WPF types (DUI-N1); Tests references Desktop instead. |
| Hide `PdfMerger` behind an `IMerger` interface for ViewModel tests | `docs/architecture.md` has ViewModels call Core directly. Tests use real PDFs through the existing `PdfFixture`. |
| ViewModel-first navigation via a convention-based view locator | Implicit `DataTemplate`s are explicit and need no reflection. |
| WPF-UI or MahApps for styling | See ADR-005. |
| ViewModel builds its own file list by sorting `Directory.GetFiles` | Would duplicate the ordering rule, so the preview could differ from what merges. Core exposes one method used by both. |

## Chosen approach

### Structure

```
ExoPdf.Desktop/
├── App.xaml(.cs)          composition root (DI), theme + startup
├── MainWindow.xaml        shell: sidebar + content area
├── ViewModels/
│   ├── PageViewModel.cs   abstract base for a navigable page (title, icon glyph)
│   ├── MainViewModel.cs   nav items, selected page
│   ├── MergeViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── MergeView.xaml
│   └── SettingsView.xaml
├── Services/              no WPF types in the interfaces
│   ├── IFolderPicker / FolderPicker         (OpenFolderDialog)
│   ├── IShellLauncher / ShellLauncher       (open file, show in folder)
│   ├── IThemeService / ThemeService         (only place that touches ThemeMode)
│   └── ISettingsStore / JsonSettingsStore   (%AppData%\ExoPdf\settings.json)
├── Models/AppSettings.cs
└── Behaviors/FolderDropBehavior.cs          (drag-and-drop → command)
```

### Extension point (DUI-2)

`MainViewModel` receives `IEnumerable<PageViewModel>` from DI and builds the
sidebar from it. A new operation adds: a ViewModel, a View, one `DataTemplate`
line, and one DI registration in `App.xaml.cs`. `MainWindow` and `MainViewModel`
are not modified. Settings is a page but is pinned to the bottom of the sidebar.

### Merge view (DUI-10 to DUI-18)

- State: `SourceFolder`, `Files` (ordered list of file names), `IsBusy`,
  `Result`, `ErrorMessage`.
- Setting `SourceFolder` (browse or drop) calls `PdfMerger.GetSourceFiles`, a new
  Core method that returns the ordered file list. `Merge` uses the same method, so
  the preview always matches the output.
- `MergeCommand` is an async `[RelayCommand]`, enabled only when `Files` is
  non-empty and not busy. It runs `PdfMerger.Merge` on a background thread.
- Success shows the output path with "Open file" and "Show in folder". Failure
  shows the error message inline; the app does not crash.
- `Files` is an observable list of small item objects (not bare strings) and the
  folder is one input source among several, so per-file actions (reorder, remove,
  add individual files) can be added later without reshaping the view.

### Settings (DUI-20 to DUI-23)

- `AppSettings` holds `Theme` (System, Light, Dark) and `LastMergeFolder`.
- `JsonSettingsStore` reads and writes `%AppData%\ExoPdf\settings.json` with
  `System.Text.Json`. A missing or corrupt file returns defaults. Unknown
  properties are ignored, so new settings can be added without breaking old files.
  Saves write a temporary file and then replace the settings file.
- `SettingsViewModel` applies the theme immediately through `IThemeService`.
- The last folder is offered on the next launch only if it still exists.

### UX

- Left sidebar about 220 px wide, operation entries on top, Settings at the bottom,
  selected entry highlighted. Icons use the font list
  `Segoe Fluent Icons, Segoe MDL2 Assets` (the second is the Windows 10 fallback).
- Merge page: title, a folder card (path, Browse button, drop target with hint
  text), the file list with a count, a primary Merge button, then a result or
  error panel. An empty state explains what to do before a folder is chosen.
- Busy state: the button shows progress and the folder card is disabled.
- All controls have `AutomationProperties.Name`, a logical tab order, and access keys.

### Core changes

- Add `PdfMerger.GetSourceFiles(string folderPath)`: `*.pdf` files sorted by file
  name with `StringComparer.OrdinalIgnoreCase`, excluding files that match
  `Merge_<FolderName>_<14 digits>.pdf`. `Merge` uses it. This changes the CLI's
  merge order and exclusion too, which was approved.
- `Merge` and `GetSourceFiles` throw `DirectoryNotFoundException` for a missing
  folder, so every frontend gets a clear error.

## Steps

1. **Retarget to .NET 10.** Set `net10.0` in Core and Cli. Set Tests to
   `net10.0-windows` so it can reference Desktop. Build and run the existing 8
   tests to confirm the baseline.
2. **Core.** Add `GetSourceFiles`, use it in `Merge`, and add the missing-folder
   error. Tests: name order, case-insensitive order, previous merge output
   excluded, empty folder, missing folder, `GetSourceFiles` matches the merge order.
3. **Desktop scaffolding.** Add `Microsoft.Extensions.DependencyInjection`. Remove
   `StartupUri`; build the DI container in `App.OnStartup`. Add
   `<NoWarn>WPF0001</NoWarn>` to the Desktop project (ADR-005). Add the `Services`
   interfaces and implementations.
4. **Shell.** `PageViewModel`, `MainViewModel`, `MainWindow` with the sidebar and
   the `DataTemplate` mapping, plus theme resources and shared styles.
5. **Settings.** `AppSettings`, `JsonSettingsStore`, `ThemeService`,
   `SettingsViewModel`, `SettingsView`. Wire the theme at startup.
6. **Merge.** `MergeViewModel`, `MergeView`, `FolderDropBehavior`. Restore and
   persist the last folder.
7. **ViewModel tests** in `ExoPdf.Tests`, using fake services: file listing after
   folder selection, Merge disabled with no files, busy state, success result,
   failure message, settings load and save, corrupt settings file, theme applied on
   change, navigation lists pages and selection changes the content.
8. **Run the app** and check each requirement by hand: light and dark, system
   theme change while running, drag and drop, 100% to 200% scaling, keyboard-only
   use, and behaviour on Windows 10.
9. **Docs.** Update the Desktop section and folder tree in `docs/architecture.md`,
   and the "Adding a new operation" checklist.
10. `dotnet test`, then `/review` and `/security-review`, fix findings, commit, and
    open the PR linking this plan.

## Decisions made at approval

- **Merged output is excluded from the next merge.** The output file is saved in
  the source folder, so merging the same folder twice would include the previous
  `Merge_<Folder>_<timestamp>.pdf`. Approved: `GetSourceFiles` skips files that
  match `Merge_<FolderName>_<14 digits>.pdf`, for the CLI as well. Recorded in the
  Merge section of `requirements.md`.

## Out of scope

- Reordering, deselecting or adding individual files (DUI-17, DUI-18).
- New PDF operations.
- Page preview.
- Installer or packaging changes.
- A shared settings file for the CLI.
