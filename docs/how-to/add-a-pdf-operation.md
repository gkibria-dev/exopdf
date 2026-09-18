# How to add a PDF operation

Goal: add a new operation (for example Split) to Core, the CLI and the Desktop app.

Before you start, document the operation in `docs/requirements.md` and follow
`docs/agentic-workflow.md` (branch, docs, plan, approval).

## Core

1. Add `<OperationName>Options.cs` and `<OperationName>Result.cs` to
   `ExoPdf.Core/Models/`.
2. Add an interface `I<Operation>` and the class `Pdf<OperationName>` to
   `ExoPdf.Core/`. Take `IFileSystem` (and `TimeProvider` if you need the time) in the
   constructor. Do not use `System.IO` statics or `DateTime`.
3. Throw an `ExoPdfException` subclass for each failure a user can cause. Put the
   file or folder name in the message.
4. If the operation can be slow, accept `IProgress<T>` and a `CancellationToken`.
   Check the token between units of work and write output to a temporary file that
   is moved into place at the end.

## CLI

5. Add `<OperationName>Command.cs` to `ExoPdf.Cli/Commands/`. Write through
   `parseResult.InvocationConfiguration.Output` and `.Error`. Catch
   `ExoPdfException` only.
6. Add the command in `ExoPdf.Cli/CliApp.cs`.
7. Create the operation in `ExoPdf.Cli/Program.cs`.

## Desktop

8. Add `<OperationName>ViewModel.cs` to `ExoPdf.Desktop/ViewModels/`, derived from
   `PageViewModel`. Depend on the Core interface and on `ExoPdf.Desktop.Services`
   interfaces only.
9. Add `<OperationName>View.xaml` to `ExoPdf.Desktop/Views/`.
10. In `ExoPdf.Desktop/App.xaml`, add a `DataTemplate` that maps the ViewModel to the
    View.
11. In `ExoPdf.Desktop/App.xaml.cs`, register the Core classes and add
    `services.AddSingleton<PageViewModel, <OperationName>ViewModel>()`.

## Tests

12. Test the Core class in `ExoPdf.Tests` with `MockFileSystem` and `FakeTimeProvider`.
13. Test the command with a fake of the Core interface and an
    `InvocationConfiguration`.
14. Test the ViewModel with fakes of the Core interface and the services.

## Finish

15. Update `docs/requirements.md` and `docs/architecture.md` if the structure changed.
16. Run `dotnet test`.
