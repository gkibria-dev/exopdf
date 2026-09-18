# Architecture

## Solution structure

```
ExoPdf.slnx
├── ExoPdf.Core/          ← class library
├── ExoPdf.Cli/           ← console app
├── ExoPdf.Desktop/       ← WPF app
└── ExoPdf.Tests/         ← xUnit tests for Core and the Desktop ViewModels
```

See [ADR-001](adr/001-multi-project-layout.md) for the rationale.

### Dependency direction

```
ExoPdf.Cli ──→ ExoPdf.Core
ExoPdf.Desktop ──→ ExoPdf.Core
ExoPdf.Tests ──→ ExoPdf.Core, ExoPdf.Desktop
ExoPdf.Core ──→ (nothing in this solution)
```

Core has no reference to any UI project. Cli and Desktop are adapters that
translate user gestures into Core calls and display results.

---

## ExoPdf.Core

Contains all PDF logic. No dependency on `System.Console`, WPF, or any UI
framework.

```
ExoPdf.Core/
├── Operations/
│   └── PdfMerger.cs
└── Models/
    ├── MergeOptions.cs
    └── MergeResult.cs
```

**Naming conventions**
- Operation classes are named as agent nouns: `PdfMerger`, `PdfSplitter` — not
  `PdfMergeService` or `PdfMergeHelper`
- The folder is named `Operations/`, not `Services/`
- Each operation takes an options object and returns a result object — no
  primitive parameter lists, no console output
- An operation exposes anything a frontend needs to preview its work as a public
  method (for example `PdfMerger.GetSourceFiles`), and uses that same method
  itself, so a preview can never differ from what runs

**Package:** `PDFsharp` (MIT). See [ADR-002](adr/002-pdfsharp-library.md).

---

## ExoPdf.Cli

Thin adapter. Parses arguments, constructs an options object, calls the
corresponding Core operation, and writes the result to stdout.

```
ExoPdf.Cli/
└── Commands/
    └── MergeCommand.cs
```

Each PDF operation has one corresponding command class. Command classes contain
no PDF logic — only argument binding and output formatting.

**Package:** `System.CommandLine`. See [ADR-003](adr/003-system-commandline-cli.md).

---

## ExoPdf.Desktop

WPF application following MVVM. Views contain no logic; ViewModels contain no
WPF types and call Core operations directly.

```
ExoPdf.Desktop/
├── App.xaml(.cs)          composition root: DI container, theme, startup
├── MainWindow.xaml        shell: sidebar + content area
├── ViewModels/
│   ├── PageViewModel.cs   base for every navigable page
│   ├── MainViewModel.cs   sidebar entries and the current page
│   ├── MergeViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── MergeView.xaml
│   ├── SettingsView.xaml
│   └── Styles.xaml        shared styles built on the Fluent theme brushes
├── Services/              interfaces free of WPF types, plus implementations
│   ├── IFolderPicker      folder dialog
│   ├── IShellLauncher     open a file, show it in Explorer
│   ├── IThemeService      applies light/dark/system (only place using ThemeMode)
│   └── ISettingsStore     %AppData%\ExoPdf\settings.json
├── Models/AppSettings.cs
└── Behaviors/FolderDropBehavior.cs   drag-and-drop a folder onto a view
```

**Navigation.** `MainViewModel` receives every registered `PageViewModel` and
builds the sidebar from them. A page's `Placement` puts it with the operations
(`Main`) or pinned to the bottom (`Footer`, used by Settings). The content area
shows the current page; the View is chosen by the `DataTemplate` that maps the
ViewModel type to its View in `App.xaml`.

**Services.** ViewModels reach anything WPF-, OS- or file-system-specific through
the interfaces in `Services/`. Tests replace them with fakes.

**Settings.** `AppSettings` is one class persisted as JSON. New settings are new
properties with defaults; unknown or corrupt content falls back to defaults.

**Theme.** Built-in Fluent theme (`Application.ThemeMode`). See
[ADR-005](adr/005-builtin-fluent-theme.md).

**Packages:** WPF (inbox), `CommunityToolkit.Mvvm`,
`Microsoft.Extensions.DependencyInjection`.
See [ADR-004](adr/004-wpf-desktop-ui.md).

---

## Adding a new operation

1. Add `<OperationName>Options.cs` and `<OperationName>Result.cs` to `Core/Models/`
2. Add `Pdf<OperationName>.cs` to `Core/Operations/`
3. Add `<OperationName>Command.cs` to `Cli/Commands/`
4. Add `<OperationName>ViewModel.cs` (derived from `PageViewModel`) to
   `Desktop/ViewModels/` and `<OperationName>View.xaml` to `Desktop/Views/`
5. In `Desktop/App.xaml`, add a `DataTemplate` mapping the ViewModel to the View.
   In `Desktop/App.xaml.cs`, add `services.AddSingleton<PageViewModel, <OperationName>ViewModel>()`.
   The shell (`MainWindow`, `MainViewModel`) is not modified.
6. Add ViewModel tests to `ExoPdf.Tests`
7. Update `docs/requirements.md` and this file if structure changes
