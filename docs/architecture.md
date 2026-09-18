# Architecture

## Solution structure

```
ExoPdf.slnx
├── ExoPdf.Core/          ← class library
├── ExoPdf.Cli/           ← console app
└── ExoPdf.Desktop/       ← WPF app
```

See [ADR-001](adr/001-multi-project-layout.md) for the rationale.

### Dependency direction

```
ExoPdf.Cli ──→ ExoPdf.Core
ExoPdf.Desktop ──→ ExoPdf.Core
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

WPF application. Each operation has a ViewModel and a View. ViewModels call Core
operations directly; Views contain no business logic.

```
ExoPdf.Desktop/
├── ViewModels/
│   └── MergeViewModel.cs
└── Views/
    └── MergeView.xaml
```

**Packages:** WPF (inbox), `CommunityToolkit.Mvvm`.
See [ADR-004](adr/004-wpf-desktop-ui.md).

---

## Adding a new operation

1. Add `<OperationName>Options.cs` and `<OperationName>Result.cs` to `Core/Models/`
2. Add `Pdf<OperationName>.cs` to `Core/Operations/`
3. Add `<OperationName>Command.cs` to `Cli/Commands/`
4. Add `<OperationName>ViewModel.cs` and `<OperationName>View.xaml` to `Desktop/`
5. Update `docs/requirements.md` and this file if structure changes
