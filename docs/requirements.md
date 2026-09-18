# Requirements

## Goals

A Windows desktop utility for common PDF manipulation tasks. Usable both as a
command-line tool (for scripting and automation) and as a desktop GUI (for
interactive use). All operations share a common core — behaviour is identical
regardless of which frontend is used.

## Target users

Windows users who need to manipulate PDF files without a paid tool or an online
service.

## Platform

- Windows only
- .NET 10 runtime (or self-contained executable)

## Frontends

| Frontend | Description |
|---|---|
| CLI (`ExoPdf.Cli`) | Subcommand-based; scriptable and pipeline-friendly |
| Desktop (`ExoPdf.Desktop`) | WPF GUI for interactive use |

Both frontends are optional install targets — a user may use one or both.

## Operations

Operations are added on demand. Each new operation must be documented here before
implementation begins (see `docs/agentic-workflow.md`).

---

### Merge

Merge all PDF files in a folder into a single output PDF.

**Inputs**
- Source folder path — must exist and contain at least one `.pdf` file

**Outputs**
- A single merged PDF saved in the source folder, named:
  `Merge_<FolderName>_<yyyyMMddHHmmss>.pdf`

**Behaviour**
- Files are merged in ascending file-name order (case-insensitive, ordinal).
  This applies to every frontend. User-defined ordering is a planned extension
  and is not yet specified.
- Files that match this tool's own output name, `Merge_<FolderName>_<14 digits>.pdf`,
  are skipped, so merging the same folder again does not include the previous
  result. This applies to every frontend.
- If the source folder does not exist, the operation fails with a clear error
- Each source file produces a top-level bookmark named after the file
  (without the `.pdf` extension), pointing to its first page in the merged output
- If a source file contains bookmarks, they are copied as children under that
  file's top-level bookmark, with page references remapped to their position in
  the merged output
- Bookmarks without a valid page destination (e.g. URI actions) are silently
  skipped

**CLI usage**
```
exopdf merge <folder>
```

---

## Desktop UI

A modern desktop frontend that hosts every PDF operation. Merge is the only
operation today; more will be added, so the UI is a shell that operations plug
into.

Priority: **Must** = required for the first release of the UI.
**Should** = planned, may follow. **Later** = acknowledged, not designed yet.

### Application shell

| ID | Priority | Requirement |
|---|---|---|
| DUI-1 | Must | The window has a left navigation sidebar listing the available operations, plus a Settings entry. Only Merge is listed today. |
| DUI-2 | Must | Adding an operation requires only a new View, ViewModel and navigation entry. The shell is not modified. |
| DUI-3 | Must | The window follows the Windows 11 Fluent look, with light and dark themes. |

### Merge view

| ID | Priority | Requirement |
|---|---|---|
| DUI-10 | Must | The user selects a source folder with a browse dialog or by dragging a folder onto the view. |
| DUI-11 | Must | After a folder is selected, the view lists the PDF files that will be merged, in merge order, before anything runs. |
| DUI-12 | Must | The Merge action is disabled until the folder contains at least one PDF. |
| DUI-13 | Must | While merging, the view shows a busy state and the window stays responsive. |
| DUI-14 | Must | On success, the view shows the output file path with "Open file" and "Show in folder" actions. |
| DUI-15 | Must | Errors (no PDFs, unreadable or locked file, folder not found) appear as a clear message in the view. The application does not crash. |
| DUI-16 | Must | Merge behaviour, output naming and bookmarks are those defined under **Merge** above and come from `ExoPdf.Core`. The UI adds no PDF logic. |
| DUI-17 | Later | The user reorders or deselects files before merging. |
| DUI-18 | Later | The user adds individual files instead of, or in addition to, a folder. |

The file list (DUI-11) and the folder input (DUI-10) must be structured so
DUI-17 and DUI-18 can be added without redesigning the view.

### Settings

| ID | Priority | Requirement |
|---|---|---|
| DUI-20 | Must | The Settings page offers a theme choice: Follow system (default), Light, Dark. The change applies immediately. |
| DUI-21 | Must | The application remembers the last folder used in Merge and offers it again on the next launch. |
| DUI-22 | Must | Settings persist between launches in `%AppData%\ExoPdf\settings.json`. A missing or corrupt file falls back to defaults without an error. |
| DUI-23 | Should | The settings mechanism accepts new settings without a redesign, so future operations can add their own. |

Settings belong to the Desktop frontend only. The CLI has no settings file.

### Non-functional

| ID | Requirement |
|---|---|
| DUI-N1 | The UI follows the MVVM pattern using `CommunityToolkit.Mvvm`. Views contain no logic in code-behind beyond view-only concerns. ViewModels reference no WPF types, so they can be unit-tested. |
| DUI-N2 | ViewModels call Core operations directly (see `docs/architecture.md`). Core has no UI dependency. |
| DUI-N3 | Long-running work runs off the UI thread and reports completion or failure to the ViewModel. |
| DUI-N4 | Every action is reachable by keyboard. Controls have accessible names for screen readers. |
| DUI-N5 | The layout scales correctly at 100%–200% display scaling. |
| DUI-N6 | ViewModel logic is covered by unit tests in `ExoPdf.Tests`. |

---

## Non-goals

- Creating PDFs from scratch
- Converting other file formats to PDF
- OCR or text extraction
- Form filling or annotation
- Cross-platform support (macOS, Linux)
- Previewing or rendering PDF pages in the Desktop UI
