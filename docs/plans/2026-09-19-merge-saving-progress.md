# Plan: Show the saving phase of a merge

**Date:** 2026-09-19
**Branch:** fix/merge-saving-progress
**Related requirements:** DUI-26, DUI-13, DUI-19, DUI-N3
**Requirements commit:** 4bb5baa (DUI-26 is added on this branch, not yet committed)

## Goal

Make sure a large merge never looks finished or hung while the output is still being
written, and prove that the merge and folder listing run off the UI thread.

## Context

The user asked whether the UI can block or hang when hundreds of PDFs are merged, and
suggested progress indication as a nice-to-have. An audit of `ExoPdf.Desktop` and
`ExoPdf.Core` found that this already exists:

- `MergeViewModel` runs `PdfMerger.Merge` and `IMergeSourceFinder.Find` in `Task.Run`.
- The view has a determinate progress bar, "Merged N of M: file" text and a Cancel button.
- DUI-13, DUI-19, DUI-25 and DUI-N3 already require this.
- No Core call runs on the UI thread, and nothing else does work that grows with file count.

Three gaps remain:

1. `PdfMerger` reports only after each file. After the last file the bar is full and reads
   "Merged 300 of 300" while `PdfDocument.Save` is still running, the longest silent
   stretch of a large merge.
2. Cancel is ignored during the save (the token is last checked before `Write`), but the
   Cancel button stays enabled.
3. No test shows that the work runs off the calling thread; the existing tests only imply
   it through gates.

4. Progress is handled on the merge thread (`DirectProgress<T>`). That works today only
   because WPF bindings marshal `PropertyChanged` for plain properties. Raising
   `CanExecuteChanged` from that thread, which DUI-26 needs to disable Cancel, throws a
   cross-thread exception in WPF. The ViewModel must not depend on implicit marshalling.

Core is synchronous by design (ADR-006): the caller chooses the thread. This was checked
against System.IO.Abstractions 22.2.0 (by reflection over `IDirectory` and `IFile`):
`IDirectory` has no async members at all (`GetFiles`, `EnumerateFiles` and the rest are
synchronous), and `IFile` has only whole-file `ReadAllTextAsync`/`ReadAllBytesAsync`/
`WriteAll…Async`/`AppendAll…Async` calls. PdfSharp's `PdfReader.Open(Stream)` and `Save`
are synchronous too, and the parsing is CPU-bound. So there is no async API to call, and
`Task.Run` in the ViewModel is the right way to keep the UI thread free.

## Alternatives considered

| Option | Reason rejected |
|--------|----------------|
| Use the async members of System.IO.Abstractions instead of `Task.Run` | `IDirectory` has none, so folder listing cannot use it. `IFile` has only whole-file read/write, which does not fit PdfSharp's `Stream` API and would not make the CPU-bound parsing asynchronous. |
| Replace `DirectProgress<T>` with `Progress<T>` | Captures the UI context correctly, but xunit has no synchronization context, so reports would arrive on the thread pool, unordered, and tests would need waits. |
| Marshal only the saving-stage report | Needs the same dispatch abstraction as marshalling everything, and leaves two different paths. |
| Per-page progress and cancel inside `MergeFile` | Helps only a single huge file, and `PdfReader.Open` cannot be cancelled anyway. Larger API change for little gain. |
| Make Core async (`MergeAsync`) | Contradicts the recorded decision that Core stays synchronous. |
| Move the `Directory.Exists`/`File.Exists` probes in `FolderDropBehavior` and `FolderPicker` off the UI thread | Single path, unrelated to file count, only stalls for an unreachable network path. Documented as a known limit; a separate change if wanted. |
| Make the whole progress bar indeterminate | Loses the determinate per-file progress that exists today. |

## Chosen approach

Keep the existing design and make the saving phase visible.

- Core: add `MergeStage { Merging, Saving }`. `MergeProgress` gets a trailing `Stage`
  parameter that defaults to `Merging`, so existing calls and tests still compile.
- `PdfMerger.Merge` reports one `MergeProgress(total, total, "", MergeStage.Saving)`
  after the last file, before the write.
- Desktop: a new `IUiThread` service (`void Post(Action action)`) in `Services/`, so the
  ViewModel can hand work to the UI thread without referencing WPF types (DUI-N1).
  `WpfUiThread` implements it with `Dispatcher.BeginInvoke` on the application dispatcher
  and is registered in `App.xaml.cs`. Tests use a synchronous fake.
- `MergeViewModel` takes `IUiThread`. Every progress report is posted through it, so
  `OnProgress` always runs on the UI thread and can safely change command state. Reports
  posted before the merge finishes are queued ahead of its completion, so their order is kept.
  `OnProgress` ignores a report that arrives when `IsBusy` is already false.
- `MergeViewModel`: a new `IsSaving` property with `NotifyCanExecuteChangedFor(CancelCommand)`.
  In the Saving stage `ProgressText` is "Saving merged file…" and Cancel is disabled
  (`CanCancel` = `IsBusy && !IsSaving`). `IsSaving` is reset when the merge ends.
- `MergeView.xaml`: the progress bar's `IsIndeterminate` is bound to `IsSaving`.
- `DirectProgress<T>` stays: it hands each report to the posting delegate on the merge
  thread, in order.

## Steps

1. Requirements: add DUI-26 and extend the Merge progress bullet (done, uncommitted).
2. Implement `MergeStage`, the `MergeProgress` change, the `PdfMerger` report,
   `IUiThread` and `WpfUiThread` (registered in `App.xaml.cs`), the `MergeViewModel` state
   and the `MergeView.xaml` binding. Update the places that construct `MergeViewModel`.
3. Tests in `ExoPdf.Tests`:
   - `PdfMergerTests`: the Saving report comes last, after the per-file reports, and is
     not sent when the merge fails or is cancelled before it.
   - `MergeViewModelTests`: the Saving stage sets `IsSaving`, changes `ProgressText` and
     disables `CancelCommand`; all are reset when the merge ends. Use `FakePdfMerger` and its `Gate`.
   - `MergeViewModelTests`: the merger and the finder run on a thread other than the caller's.
   - `MergeViewModelTests`: with a fake `IUiThread` that queues its actions, progress
     reports do not change ViewModel state until the queue is run, and they are applied in
     order (proves state is only touched through the UI thread). A report that arrives
     after the merge has ended is ignored.
   - A small `WpfUiThread` test is not planned: it is a one-line wrapper over the dispatcher.
     It is covered by the manual check under Verification.
4. `dotnet test`, then `/review` and `/security-review`; fix findings.
5. Update `docs/architecture.md` (start-up, listing and background merge section) for the Saving stage.
6. Commit and open a PR that links this plan.

## Verification

- `dotnet test ExoPdf.Tests\ExoPdf.Tests.csproj` passes.
- Manual, in the Desktop app, with about 500 generated PDFs in a temp folder:
  - While merging, drag the window and change the theme in Settings; the window stays responsive.
  - At the end the bar becomes indeterminate with "Saving merged file…" and Cancel is
    disabled, with no cross-thread exception.
  - Cancelling mid-merge shows "Merge cancelled" and leaves no `.tmp` or output file.

## Out of scope

- Per-page progress, and cancelling inside a single file.
- An asynchronous Core API.
- Moving the UNC path probes or the settings save off the UI thread.
- Batching `SetFiles` for thousands of files.
