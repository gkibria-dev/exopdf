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
- .NET 8 runtime (or self-contained executable)

## Frontends

| Frontend | Description |
|---|---|
| CLI (`PdfUtil.Cli`) | Subcommand-based; scriptable and pipeline-friendly |
| Desktop (`PdfUtil.Desktop`) | WPF GUI for interactive use |

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
- Files are merged in filesystem order
- Each source file produces a top-level bookmark named after the file
  (without the `.pdf` extension), pointing to its first page in the merged output
- If a source file contains bookmarks, they are copied as children under that
  file's top-level bookmark, with page references remapped to their position in
  the merged output
- Bookmarks without a valid page destination (e.g. URI actions) are silently
  skipped

**CLI usage**
```
pdfutil merge <folder>
```

---

## Non-goals

- Creating PDFs from scratch
- Converting other file formats to PDF
- OCR or text extraction
- Form filling or annotation
- Cross-platform support (macOS, Linux)
