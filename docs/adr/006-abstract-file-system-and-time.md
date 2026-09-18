# ADR-006: Abstract the file system and the clock in Core

## Status
Accepted

## Context

`PdfMerger` reads folders and files through the `Directory`, `Path` and `File`
statics, decides output names from `DateTime.Now`, and writes the output straight
to disk. As a result:

- Ordering, previous-output exclusion and naming (pure logic) can only be tested
  through real temp folders, and time cannot be controlled.
- The Desktop ViewModel needed `Directory.Exists`, and the tests for it needed
  real PDFs on disk. ViewModels were meant to be free of such dependencies.
- The plan for the Desktop UI rejected an `IMerger` interface so that ViewModels
  would "call Core directly". That decision made ViewModel tests integration tests.

Options for file-system access:

| Option | Notes |
|---|---|
| **`System.IO.Abstractions`** | Widely used, MIT. `IFileSystem` mirrors `System.IO`; the `.TestingHelpers` package provides `MockFileSystem`. Streams work with PDFsharp, so tests can run in memory. |
| Own narrow interface (`IPdfFileStore`) | Fewer members, but every new operation extends it, and it needs its own in-memory fake. |
| No abstraction; keep temp-folder tests | Simple, but leaves the design problems above. |

For time: `TimeProvider` (built into .NET 8+, with `FakeTimeProvider` in
`Microsoft.Extensions.TimeProvider.Testing`).

## Decision

1. Core depends on `IFileSystem` and `TimeProvider`, injected through
   constructors. Composition roots (CLI `Program`, Desktop `App`) supply
   `FileSystem` and `TimeProvider.System`.
2. Core exposes small interfaces to its consumers: `IPdfMerger` and
   `IMergeSourceFinder`. ViewModels and CLI commands depend on those, not on
   concrete classes. This supersedes the "call Core directly, no `IMerger`" choice
   in the Desktop UI plan.
3. Only real external boundaries get an abstraction. Deterministic collaborators
   (`MergeOutputNamer`, `OutlineCopier`) stay concrete classes.
4. Core reports expected failures with its own exception types (`ExoPdfException`
   and subclasses) so frontends do not depend on framework exception types or
   message text.
5. A generic `IPdfOperation<TOptions, TResult>` is not introduced yet. With one
   operation it would be a guess; add it when the second operation arrives.

## Consequences

- Core unit tests run in memory (`MockFileSystem`, `FakeTimeProvider`). A few
  tests still use the real disk to cover the real `FileSystem` path.
- ViewModel and CLI tests use hand-written fakes of `IPdfMerger` and
  `IMergeSourceFinder` and need no PDFs.
- `System.IO.Abstractions` is one more dependency in Core, with the release
  cadence of an external package.
- Each operation adds a few small classes and one interface. The cost is accepted
  because operations are expected to multiply.
- `docs/architecture.md` changes from "ViewModels call Core operations directly"
  to "ViewModels call Core through interfaces".
