# ADR-002: PDFsharp as the PDF library

## Status
Accepted

## Context

The project needs a .NET library for reading, manipulating, and writing PDF files.
The primary requirements are: page merging, bookmark (outline) manipulation, and
an open-source license compatible with a public GitHub repository.

Candidates evaluated:

| Library | License | Notes |
|---|---|---|
| **PDFsharp** | MIT | Mature, well-documented, active maintenance under empira |
| iTextSharp / iText 7 | AGPL / commercial | AGPL requires derivative works to also be open source; commercial license is paid |
| PdfPig | Apache 2.0 | Newer, read-focused; write and outline support less mature |
| Aspose.PDF | Commercial | Paid; not suitable for a free public tool |

## Decision

Use **PDFsharp** (v6.x, MIT licence).

The MIT licence is the deciding factor for a public repository — no licence
compliance burden for users or contributors. PDFsharp also has the most complete
API for the operations planned (page import, outline/bookmark trees,
document metadata).

## Consequences

- No licence friction for public distribution or forks
- PDFsharp does not support all PDF features (e.g. heavily encrypted files,
  some advanced form fields) — acceptable given the tool's scope
- If an operation is ever needed that PDFsharp cannot handle, this ADR should
  be revisited and a new ADR written to supersede it
