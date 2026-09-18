# Plan: Multi-project solution restructure

**Date:** 2026-09-18
**Branch:** refactor/multi-project-layout
**Related requirements:** docs/requirements.md — Frontends section
**Related ADR:** docs/adr/001-multi-project-layout.md

## Goal

Split the current single-project console app into three projects: `ExoPdf.Core`,
`ExoPdf.Cli`, and `ExoPdf.Desktop`, as described in ADR-001.

## Context

All PDF logic currently lives in `ExoPdf/Program.cs` alongside console I/O.
The planned desktop GUI and additional operations make this untenable. Core must
be isolated before new operations or the desktop frontend are added.

## Alternatives considered

| Option | Reason rejected |
|---|---|
| Keep single project, add folders | Console I/O and PDF logic remain coupled; Desktop project would need to reference a console app |
| Use `src/` subfolder for all projects | Adds a nesting level with no benefit at this project size |

## Chosen approach

Flat layout at solution root. Delete the old `ExoPdf` project. Create three new
projects in their own folders. Move existing PDF logic into Core; wire Cli up to
it. Desktop is a placeholder only — no operations connected yet.

## Steps

### 1 — Create `ExoPdf.Core` (class library, net8.0)

- Add `PDFsharp` package reference
- Create `Models/MergeOptions.cs`
  ```csharp
  public class MergeOptions
  {
      public required string SourceFolderPath { get; init; }
  }
  ```
- Create `Models/MergeResult.cs`
  ```csharp
  public class MergeResult
  {
      public required string OutputFilePath { get; init; }
      public int FilesMerged { get; init; }
      public int TotalPages { get; init; }
  }
  ```
- Create `Operations/PdfMerger.cs`
  - Public method: `MergeResult Merge(MergeOptions options)`
  - Move all PDF logic from current `Program.cs` here
  - Output file path computed inside Core (same naming convention as today)

### 2 — Create `ExoPdf.Cli` (console app, net8.0)

- Add project reference to `ExoPdf.Core`
- Add `System.CommandLine` package
- Create `Commands/MergeCommand.cs` — binds the `<folder>` argument, calls
  `PdfMerger.Merge()`, prints the result path
- `Program.cs` — sets up the root command with `merge` subcommand, invokes it

CLI shape:
```
pdfutil merge <folder>
```

### 3 — Create `ExoPdf.Desktop` (WPF, net8.0-windows) — placeholder only

- Add project reference to `ExoPdf.Core`
- Add `CommunityToolkit.Mvvm` package
- Default WPF template: `App.xaml`, `MainWindow.xaml`
- No operations wired up yet — placeholder for future work

### 4 — Remove old `ExoPdf` project

- Remove `ExoPdf/ExoPdf.csproj` and `ExoPdf/Program.cs` from the solution
- Delete the `ExoPdf/` folder

### 5 — Update solution file

- Add all three new projects to `ExoPdf.sln`
- Verify `dotnet build ExoPdf.sln` passes cleanly

### 6 — Update `CLAUDE.md` and `docs/architecture.md`

- Update the Architecture section in `CLAUDE.md` to reflect the new structure
- Mark "Current state" in `architecture.md` as resolved — target structure is now live

## Out of scope

- Implementing any new PDF operation
- Wiring up any Desktop UI beyond the placeholder
- Adding tests (separate task)
- GitHub repo setup (separate task)
