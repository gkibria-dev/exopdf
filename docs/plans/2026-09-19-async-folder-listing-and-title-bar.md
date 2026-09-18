# Plan: Asynchronous folder listing and a themed title bar

**Date:** 2026-09-19
**Branch:** fix/async-folder-listing-and-title-bar (stacked on `feature/desktop-ui`, PR #1)
**Related requirements:** DUI-25, DUI-4, DUI-N3
**Requirements commit:** f52baf1
**Related ADR:** [ADR-005](../adr/005-builtin-fluent-theme.md) (addendum in step 5)

## Goal

Close the two gaps listed as "not done" in PR #1: listing a folder must not block
the window, and the title bar must match the theme on Windows 10.

## Context

- `MergeViewModel` lists files with the synchronous `IMergeSourceFinder.Find`. It runs
  in the constructor (restoring the last folder, before the window is shown) and on
  every Browse, drop and reload, all on the UI thread. A slow or unreachable network
  share freezes startup and the window.
- The built-in Fluent theme (`ThemeMode`) darkens window contents but not the native
  title bar on Windows 10, which stays white in dark mode.
- Core is synchronous by design (ADR-006): the caller chooses the thread.
- The development machine runs Windows 10 (build 19045). There is no Windows 11
  machine to test on.

## Alternatives considered

| Option | Reason rejected |
|---|---|
| Make `IMergeSourceFinder` async (`FindAsync`) | `System.IO.Abstractions` and the Directory APIs are synchronous, so it would only be `Task.Run` inside Core. ADR-006 keeps Core synchronous. |
| Start the restore from the ViewModel constructor without awaiting | Exceptions would go unobserved and tests could not wait for it. Startup work is awaited from `App` instead. |
| Custom window chrome (`WindowChrome`, own caption buttons) | Large change with risk to snapping, accessibility and DPI behaviour, for a colour difference. |
| Wait for Windows 11 only | The developer machine is Windows 10; the gap is visible today. |
| WPF-UI for a themed window | Rejected earlier in ADR-005. |

## Chosen approach

### Asynchronous listing (DUI-25)

- `PageViewModel` gets `virtual Task InitializeAsync()` (default: completed).
  `MainViewModel.InitializeAsync()` awaits every page's. `App.OnStartup` shows the
  window, then awaits `MainViewModel.InitializeAsync()`. `OnStartup` becomes
  `async void`; an exception then reaches the existing unexpected-error dialog.
- `MergeViewModel`:
  - The constructor no longer touches the file system. `InitializeAsync` restores the
    last folder (and silently drops it if it cannot be listed, as today).
  - Choosing a folder (`Browse`, `SelectFolder`) becomes an asynchronous command.
    It sets `SourceFolder`, clears `Files`, sets `IsLoadingFiles`, runs
    `finder.Find` with `Task.Run`, then fills `Files`.
  - Each listing has its own `CancellationTokenSource`. Starting a new listing
    cancels the previous one, and a result is applied only if it is still the latest,
    so the latest choice wins (DUI-25).
  - The last folder is saved only after a listing of it succeeded and is still current.
  - `IsLoadingFiles` disables Merge. Browse and drop stay enabled so a slow folder can
    be replaced.
- View: while loading, the file card shows "Reading the folder…" and an indeterminate
  bar instead of the empty-state text.
- The finder stays synchronous (ADR-006).

### Themed title bar (DUI-4)

- `Services/DwmTitleBar` (internal, P/Invoke): `DwmSetWindowAttribute` with
  `DWMWA_USE_IMMERSIVE_DARK_MODE` (attribute 20; 19 on older Windows 10 builds).
  Failures are ignored, so an unsupported system simply keeps the light title bar.
- `TitleBarTheme.IsDark(AppTheme theme, bool systemPrefersDark)` is a pure function and
  is unit tested.
- `ThemeService` (still the only place touching theme APIs) applies the title bar to
  every open window when the theme changes, and to windows created later
  (`SourceInitialized`). For "Use system setting" it reads
  `AppsUseLightTheme` from the registry and re-applies when Windows reports a
  preference change (`SystemEvents.UserPreferenceChanged`).
- `IThemeService` is unchanged, so ViewModels and their tests are unaffected.

## Steps

Each step is one commit; the solution builds and tests pass after each.

1. **Baseline.** Before changing code, record the problem in the running app: point
   `LastMergeFolder` at an unreachable UNC path and measure how long the window takes
   to appear and whether it responds. Take a screenshot of the light title bar in dark
   theme.
2. **Title bar.** `TitleBarTheme` with tests, `DwmTitleBar`, `ThemeService` changes.
   Check in the app: light, dark, system; change the Windows theme while the app runs
   (registry value plus a `WM_SETTINGCHANGE` broadcast).
3. **Async listing plumbing.** `InitializeAsync` on `PageViewModel` and
   `MainViewModel`; `App.OnStartup`. Tests.
4. **Async listing in `MergeViewModel`** and the loading state in the view. The fake
   finder gains a gate so tests can hold a listing open. Tests: loading flag, latest
   wins, stale result ignored, error while loading, restore after startup, save only
   after success, Merge disabled while loading. Repeat the baseline measurement: the
   window appears at once and stays responsive.
5. **Docs.** `docs/architecture.md`, ADR-005 addendum (title bar), and the outcome
   section of this plan.
6. `dotnet test`, `/code-review`, `/security-review`, fix findings.

## Risks and how they are checked

- **The title bar attribute may not work on every Windows 10 build.** Attribute 20
  needs build 18985 or later, 19 works on 1809 to 1909. Both are tried; failure is
  silent. Checked on build 19045 only.
- **Windows 11 cannot be tested here.** `ThemeMode` already themes the caption there,
  so the extra call should change nothing; this is stated in the PR as unverified.
- **Live system-theme change may not reach the window contents on Windows 10.** The
  title bar is checked separately from the contents; if the contents do not follow
  either, that is recorded as a known limit of `ThemeMode.System`.
- **`async void OnStartup`.** Errors after `await` go to the dispatcher's unhandled
  exception handler, which shows the dialog and, if no window exists, exits.

## Out of scope

- Windows 11 verification.
- Custom window chrome.
- An asynchronous Core API.
- Showing more than the file list while loading (for example a file count estimate).
