# Plan: Abstractions and design cleanup (Core, CLI, Desktop)

**Date:** 2026-09-18
**Branch:** feature/desktop-ui (continues the branch; one commit per step)
**Related requirements:** Merge behaviour (no overwrite, atomic output, file named in errors, progress and cancel, explicit file list), DUI-19, DUI-N7, DUI-N6
**Requirements commit:** c21caa7
**Related ADR:** [ADR-006](../adr/006-abstract-file-system-and-time.md)

## Goal

Make the Core, CLI and Desktop code testable in isolation and remove the design
problems found in the design review, without changing what users see except for
the new requirements above.

## Context

A design review of the code on this branch found these problems (IDs are used in
the steps below):

| ID | Problem |
|---|---|
| C1 | `PdfMerger` has six responsibilities: source discovery, output naming, orchestration, page merging, bookmark copying, saving. |
| C2 | It calls `Directory`, `Path`, `File` statics, `DateTime.Now`, and PDFsharp path overloads directly, so it cannot be unit-tested. |
| C3 | The output naming rule exists twice (format string and exclusion regex) and can drift. |
| C4 | Two merges in the same second silently overwrite the first output. |
| C5 | Failures use framework exceptions; a corrupt PDF and "no PDFs" are both `InvalidOperationException`, and the corrupt-file message does not name the file. |
| C6 | `MergeOptions` cannot carry an explicit file list, which DUI-17 and DUI-18 need. |
| C7 | No progress or cancellation. |
| C8 | Operations have no shared contract. |
| D1 | `MergeViewModel` depends on the concrete `PdfMerger`. |
| D2 | `MergeViewModel` calls `Directory.Exists`. |
| D3 | `MergeViewModel` also formats and launches results. |
| D4 | `ISettingsStore.Current` is shared mutable state, callers must remember `Save()`, and the store reads a file in its constructor with `File.*` statics. |
| D5 | The sidebar keeps two ListBox selections in sync with null-guard logic. |
| D6 | The "catch everything and show the message" policy is repeated in the ViewModel and the CLI. |
| D7 | `PageViewModel.IconGlyph` puts a view concern in a ViewModel. |
| L1 | `MergeCommand` creates `PdfMerger` itself, writes to `Console`, and has no tests. |

Verified before planning: PDFsharp reads and writes `Stream`s and works with
`MockFileSystem`; `System.CommandLine` accepts an `InvocationConfiguration` with
injectable output and error writers; PDFsharp throws a plain
`InvalidOperationException` for a corrupt PDF (which is why C5 needs translation).

## Alternatives considered

| Option | Reason rejected |
|---|---|
| Own narrow `IPdfFileStore` interface | Chosen against by the project owner in favour of `System.IO.Abstractions`; see ADR-006. |
| Generic `IPdfOperation<TOptions,TResult>` now (C8) | One operation exists. A contract designed from one example is a guess. Deferred to the second operation. |
| Move `IconGlyph` to the View (D7) | Needs a key-to-glyph mapping layer for little gain; an icon on a navigation item is a normal ViewModel property. Kept, recorded as a decision. |
| Expose `MergeAsync` from Core | PDFsharp is synchronous; an async wrapper only hides `Task.Run` and misleads callers. Core stays synchronous with progress and cancellation, and the caller chooses the thread. This replaces the "async in Core" idea in the review. |
| Interfaces for `MergeOutputNamer` and `OutlineCopier` | They are deterministic and testable through their inputs. An interface would add indirection with no test benefit. |
| Keep two ListBoxes and patch selection (D5) | Already needed one patch after review; the model is the problem. |

## Chosen approach

### Core

```
ExoPdf.Core/
├── Operations/PdfMerger.cs         orchestration only
├── Merging/
│   ├── IPdfMerger.cs
│   ├── IMergeSourceFinder.cs / MergeSourceFinder.cs   discovery, ordering, exclusion
│   ├── MergeOutputNamer.cs         builds names and recognises them (one rule)
│   └── OutlineCopier.cs            internal, bookmark copying
├── Models/
│   ├── MergeOptions.cs             SourceFolderPath + optional Files
│   ├── MergeResult.cs
│   └── MergeProgress.cs            FilesCompleted, TotalFiles, CurrentFile
└── Errors/
    ├── ExoPdfException.cs
    ├── SourceFolderNotFoundException.cs
    ├── NoPdfFilesException.cs
    └── PdfUnreadableException.cs   FilePath, message names the file
```

```csharp
public interface IPdfMerger
{
    MergeResult Merge(MergeOptions options,
                      IProgress<MergeProgress>? progress = null,
                      CancellationToken cancellationToken = default);
}

public interface IMergeSourceFinder
{
    IReadOnlyList<string> Find(string folderPath);
}

public sealed class PdfMerger(IFileSystem fileSystem, IMergeSourceFinder finder, MergeOutputNamer namer) : IPdfMerger
public sealed class MergeSourceFinder(IFileSystem fileSystem, MergeOutputNamer namer) : IMergeSourceFinder
public sealed class MergeOutputNamer(IFileSystem fileSystem, TimeProvider timeProvider)
```

- `MergeOutputNamer` owns the `Merge_<Folder>_<yyyyMMddHHmmss>.pdf` rule in one
  place: it creates the next free output path (advancing the timestamp by one
  second while the name exists) and answers "is this file one of our outputs?" for
  the finder (C3, C4).
- `Merge` opens inputs with `fileSystem.File.OpenRead`, writes to a temporary file
  next to the output, then moves it into place. Failure or cancellation deletes
  the temporary file, so no partial output remains (requirements).
- `Merge` checks `cancellationToken` before each file and before saving, and
  reports progress after each file. Cancellation throws
  `OperationCanceledException`, which is not an `ExoPdfException`.
- PDFsharp and I/O failures are translated inside `Merge`:
  `PdfUnreadableException(filePath)` for a file that cannot be read as a PDF, and
  `SourceFolderNotFoundException`, `NoPdfFilesException` for the others. Unexpected
  exceptions propagate unchanged.
- When `MergeOptions.Files` is set, those files are merged in the given order and
  the finder is not used; output still goes to `SourceFolderPath`.

### CLI

- `MergeCommand.Build(IPdfMerger merger)` writes through
  `parseResult.InvocationConfiguration.Output/Error`, catches `ExoPdfException`
  only (message to stderr, exit code 1), and `Program` is the composition root.
- Tests run the command with an `InvocationConfiguration` and a fake `IPdfMerger`.

### Desktop

- `MergeViewModel(IPdfMerger, IMergeSourceFinder, IFolderPicker, ISettingsService, ...)`.
  Folder existence is decided by the finder (`SourceFolderNotFoundException`), so
  the ViewModel uses no `System.IO` (D2).
- `MergeResultViewModel` (summary text, output path, Open and Show in folder
  commands, shell errors) is extracted from `MergeViewModel` (D3).
- The merge runs in `Task.Run` with a `Progress<MergeProgress>` created on the UI
  thread and a `CancellationTokenSource`. The view shows a determinate progress
  bar and a Cancel button while busy; cancelling shows "Merge cancelled" (DUI-19).
- `ExoPdfException` is displayed as the error message. Any other exception is
  handled by an `App.DispatcherUnhandledException` handler that shows a generic
  dialog (DUI-N7). The catch-everything blocks in the ViewModel are removed (D6).
- Settings (D4): `AppSettings` becomes an immutable record.
  `ISettingsStore` only loads and saves (uses `IFileSystem`, no I/O in the
  constructor). `ISettingsService` exposes `Current` and
  `Update(Func<AppSettings, AppSettings>)`, which replaces the value and saves it,
  so a caller cannot forget to save. ViewModels use `ISettingsService`.
- Navigation (D5): `MainViewModel` keeps one `CurrentPage`; a `SelectPage` command
  sets it, and each `PageViewModel` exposes `IsSelected`. The sidebar entries are
  radio-style buttons bound one way to `IsSelected`. There is no second
  selection, so the null-guard logic goes away. Keyboard arrow navigation must be
  checked; if it does not work acceptably, the two-ListBox version stays.

### Tests

- Core: `MockFileSystem` and `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`);
  PDFs are built in memory. A small set of tests uses the real `FileSystem` on a
  temp folder to cover the real path.
- ViewModels and CLI: hand-written fakes of `IPdfMerger` and `IMergeSourceFinder`.
  No PDFs are needed.
- Settings store: `MockFileSystem`.

## Steps

Each step is one commit. The solution builds and all tests pass after each one.

1. **Inject file system and time (C1, C2, C3, C4).** Add `System.IO.Abstractions`
   and, in Tests, its `TestingHelpers` and `Microsoft.Extensions.TimeProvider.Testing`.
   Extract `MergeOutputNamer`, `MergeSourceFinder`, `OutlineCopier`. Read and write
   through streams, atomic save, no overwrite. Update composition roots and call
   sites mechanically. Move Core tests to `MockFileSystem`; keep a few real-disk tests.
2. **Interfaces and CLI (D1, D2, L1).** Add `IPdfMerger` and `IMergeSourceFinder`.
   ViewModels and `MergeCommand` depend on them. Remove `Directory.Exists` from the
   ViewModel. `Program` becomes the CLI composition root. ViewModel tests use fakes;
   add CLI tests.
3. **Domain errors (C5, D6).** Add the exception types and the translation in
   `Merge`. Frontends catch `ExoPdfException`. Add the Desktop global handler
   (DUI-N7). Tests for each error, including the file name in the message.
4. **Options, progress, cancellation (C6, C7).** Add `MergeOptions.Files`,
   `MergeProgress` and cancellation to Core, with tests.
5. **Desktop merge UX and result view model (D3).** Progress bar, Cancel button,
   `MergeResultViewModel`. ViewModel tests.
6. **Settings (D4).** Immutable `AppSettings`, `ISettingsStore`, `ISettingsService`.
   Migrate `SettingsViewModel`, `MergeViewModel`, `App`. Tests on `MockFileSystem`.
7. **Navigation (D5).** Single-selection model and radio-style sidebar; check
   keyboard navigation and screen-reader names in the running app.
8. **Docs.** Update `docs/architecture.md` (interfaces, folder trees, adding-an-operation
   checklist, "through interfaces" wording).
9. **Verify.** `dotnet test`; run the app (merge, cancel, corrupt file, theme, keyboard
   navigation); `/review` and `/security-review`; fix findings.

## Deferred, with reasons

- **C8 shared operation contract:** revisit when the second operation is added.
- **D7 icon in the ViewModel:** kept on purpose (see alternatives).

## Out of scope

- New PDF operations.
- Any frontend that uses `MergeOptions.Files` (DUI-17, DUI-18).
- Changes to the CLI's command-line syntax.
