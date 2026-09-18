# Architecture

## Solution structure

```
ExoPdf.slnx
├── ExoPdf.Core/          ← class library
├── ExoPdf.Cli/           ← console app
├── ExoPdf.Desktop/       ← WPF app
└── ExoPdf.Tests/         ← xUnit tests for Core, the CLI and the Desktop ViewModels
```

See [ADR-001](adr/001-multi-project-layout.md) for the rationale.

### Dependency direction

```
ExoPdf.Cli ──→ ExoPdf.Core
ExoPdf.Desktop ──→ ExoPdf.Core
ExoPdf.Tests ──→ ExoPdf.Core, ExoPdf.Cli, ExoPdf.Desktop
ExoPdf.Core ──→ (nothing in this solution)
```

Core has no reference to any UI project. Cli and Desktop are adapters that
translate user gestures into Core calls and display results.

### Principles

- **Abstract real boundaries, not everything.** Core reaches the file system through
  `IFileSystem` (`System.IO.Abstractions`) and the clock through `TimeProvider`,
  both injected. Deterministic collaborators (`MergeOutputNamer`, `OutlineCopier`)
  are plain classes. See [ADR-006](adr/006-abstract-file-system-and-time.md).
- **Frontends depend on Core interfaces** (`IPdfMerger`, `IMergeSourceFinder`), never
  on concrete Core classes, so ViewModels and commands are tested with fakes.
- **Expected failures are typed.** Core throws `ExoPdfException` subclasses with
  messages fit for the user. Frontends show those and nothing else; any other
  exception is a bug and reaches the frontend's last-resort handler.
- **Composition roots wire everything.** `ExoPdf.Cli/Program.cs` and
  `ExoPdf.Desktop/App.xaml.cs` are the only places that create concrete
  implementations.
- **Core is synchronous.** Long operations take an `IProgress<T>` and a
  `CancellationToken`; the caller chooses the thread.

---

## ExoPdf.Core

Contains all PDF logic. No dependency on `System.Console`, WPF, or any UI
framework.

```
ExoPdf.Core/
├── Operations/
│   └── PdfMerger.cs                 orchestration only
├── Merging/
│   ├── IPdfMerger.cs
│   ├── IMergeSourceFinder.cs
│   ├── MergeSourceFinder.cs         discovery, ordering, previous-output exclusion
│   ├── MergeOutputNamer.cs          creates and recognises output names (one rule)
│   └── OutlineCopier.cs             internal: bookmark copying
├── Models/
│   ├── MergeOptions.cs              source folder, optional explicit file list
│   ├── MergeResult.cs
│   └── MergeProgress.cs
└── Errors/
    ├── ExoPdfException.cs           base: an expected failure with a user-facing message
    ├── SourceFolderNotFoundException.cs
    ├── NoPdfFilesException.cs
    ├── PdfUnreadableException.cs    names the file
    └── OutputWriteException.cs
```

**Naming conventions**
- Operation classes are named as agent nouns: `PdfMerger`, `PdfSplitter` — not
  `PdfMergeService` or `PdfMergeHelper`
- The folder is named `Operations/`, not `Services/`
- Each operation takes an options object and returns a result object — no
  primitive parameter lists, no console output
- An operation exposes anything a frontend needs to preview its work through its own
  small interface (for example `IMergeSourceFinder`), and uses that same class
  itself, so a preview can never differ from what runs

**How a merge works**
- Files are found and ordered by `MergeSourceFinder` (file name, case-insensitive,
  previous outputs excluded), or supplied as an explicit list in `MergeOptions.Files`.
- `MergeOutputNamer` picks an output name that does not exist yet.
- Each file is read through `IFileSystem`. A file that cannot be read, or that has no
  pages, becomes `PdfUnreadableException`. Only reading is translated this way:
  a failure in our own bookmark code is a bug and is not reported as a bad file.
- The output is written to a temporary file and moved into place, so a failure or a
  cancellation leaves no partial file. Failing to delete the temporary file never
  hides the original error.
- Progress is reported after each file; cancellation is checked before each file
  and before writing.

**Packages:** `PDFsharp` (MIT, see [ADR-002](adr/002-pdfsharp-library.md)),
`System.IO.Abstractions`.

---

## ExoPdf.Cli

Thin adapter. Parses arguments, constructs an options object, calls the
corresponding Core operation, and writes the result through the invocation
configuration's output and error writers (so tests can capture them).

```
ExoPdf.Cli/
├── Program.cs               composition root
├── CliApp.cs                builds the command tree; last-resort error handling
└── Commands/
    └── MergeCommand.cs
```

Each PDF operation has one corresponding command class. Command classes contain
no PDF logic — only argument binding and output formatting.

| Outcome | Exit code | Output |
|---|---|---|
| Success | 0 | Summary and output path on stdout |
| Expected failure (`ExoPdfException`) | 1 | Message on stderr |
| Unexpected failure | 2 | `Unexpected error (<type>): <message>` on stderr |
| Interrupted (Ctrl+C) | 130 | `Merge cancelled.` on stderr; no partial file |

**Package:** `System.CommandLine`. See [ADR-003](adr/003-system-commandline-cli.md).

---

## ExoPdf.Desktop

WPF application following MVVM. Views contain no logic; ViewModels contain no
WPF types and depend on Core interfaces and on the interfaces in `Services/`.

```
ExoPdf.Desktop/
├── App.xaml(.cs)          composition root: DI container, theme, startup,
│                          unexpected-error dialog
├── MainWindow.xaml        shell: sidebar + content area
├── ViewModels/
│   ├── PageViewModel.cs   base for every navigable page
│   ├── MainViewModel.cs   the current page and the sidebar entries
│   ├── MergeViewModel.cs
│   ├── MergeResultViewModel.cs   summary, Open, Show in folder
│   ├── MergeFileItem.cs
│   ├── SettingsViewModel.cs
│   └── DirectProgress.cs  IProgress that calls its handler on the reporting thread
├── Views/
│   ├── MergeView.xaml
│   ├── SettingsView.xaml
│   └── Styles.xaml        shared styles built on the Fluent theme brushes
├── Services/              interfaces free of WPF types, plus implementations
│   ├── IFolderPicker      folder dialog
│   ├── IShellLauncher     open a file, show it in Explorer (throws ShellLaunchException)
│   ├── IThemeService      applies light/dark/system (only place using ThemeMode)
│   ├── ISettingsStore     load and save the settings document (JsonSettingsStore)
│   └── ISettingsService   current settings; Update(s => s with { ... }) saves them
├── Models/AppSettings.cs  immutable record
└── Behaviors/
    ├── FolderDropBehavior.cs      drop a folder onto a view
    └── SelectOnFocusBehavior.cs   arrow keys select sidebar entries
```

**Navigation.** `MainViewModel` receives every registered `PageViewModel`. A page's
`Placement` puts it with the operations (`Main`) or pinned to the bottom (`Footer`,
used by Settings). `CurrentPage` is the single source of truth; each page's
`IsSelected` follows it, and the sidebar entries (radio buttons) bind to it two-way;
only a selection changes the page. The content area shows the current page;
the View is chosen by the `DataTemplate` that maps the ViewModel type to its View
in `App.xaml`. The sidebar is one Tab stop and the arrow keys move between entries.

**Merge view.** Selecting a folder lists the files through `IMergeSourceFinder`.
The merge is given exactly that list (`MergeOptions.Files`), so it merges what the
user was shown. Merging runs on a background thread with a `Progress`/`CancellationToken`: the view
shows "Merged n of N", a determinate bar and a Cancel button. Cancelling shows a
neutral notice, not an error. A successful merge produces a `MergeResultViewModel`.

**Errors.** ViewModels show `ExoPdfException` messages inline. Anything else reaches
`App.DispatcherUnhandledException`, which shows a generic dialog.

**Settings.** `AppSettings` is an immutable record persisted as JSON in
`%AppData%\ExoPdf\settings.json`. New settings are new properties with defaults;
unknown or corrupt content falls back to defaults. `ISettingsService` loads lazily
and saves on every change, so callers cannot forget to save.

**Theme.** Built-in Fluent theme (`Application.ThemeMode`). See
[ADR-005](adr/005-builtin-fluent-theme.md).

**Packages:** WPF (inbox), `CommunityToolkit.Mvvm`,
`Microsoft.Extensions.DependencyInjection`, `System.IO.Abstractions`.
See [ADR-004](adr/004-wpf-desktop-ui.md).

---

## Testing

| Layer | How it is tested |
|---|---|
| Core | In memory: `MockFileSystem`, `FakeTimeProvider`, PDFs built in memory. A few tests use the real disk. |
| CLI | Commands run with an `InvocationConfiguration` and a fake `IPdfMerger`. |
| ViewModels | Hand-written fakes of every interface; no disk, no PDFs. |
| Settings store | `MockFileSystem`. |
| WPF classes (`ThemeService`, `FolderPicker`, `ShellLauncher`, views) | Not unit tested; checked by running the app. |

---

## Adding a new operation

See [How to add a PDF operation](how-to/add-a-pdf-operation.md).

When a second operation exists, consider extracting a shared contract from the two
(`IPdfOperation<TOptions, TResult>`). It was deliberately not designed from one
example.
