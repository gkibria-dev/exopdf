# ADR-003: System.CommandLine for the CLI frontend

## Status
Accepted

## Context

The current CLI reads a folder path interactively via `Console.ReadLine()`. This
makes the tool impossible to script or integrate into pipelines. As more operations
are added, the tool needs proper subcommand routing and argument parsing.

Candidates evaluated:

| Library | Notes |
|---|---|
| **System.CommandLine** | Microsoft's official library; subcommands, options, help generation, tab completion |
| Spectre.Console | Excellent terminal UX (tables, progress bars, colours); argument parsing is secondary |
| CommandLineParser | Mature, attribute-based; no subcommand routing |
| Cocona | Convention-based, minimal boilerplate; less adoption |

## Decision

Use **System.CommandLine** for argument parsing and subcommand routing in
`PdfUtil.Cli`.

It is Microsoft's strategic investment for .NET CLI tooling, has first-class
subcommand support (each PDF operation becomes a subcommand), and generates
`--help` output automatically. Usage becomes:

```
pdfutil merge <folder>
pdfutil split <file> --pages 1-3
```

Spectre.Console may be added alongside System.CommandLine for output formatting
(progress bars, styled tables) if the UX warrants it — the two are complementary,
not competing.

## Consequences

- Tool becomes scriptable and pipeline-friendly
- Interactive prompts are replaced by named arguments — a breaking change from
  the current behaviour, accepted as the right direction
- System.CommandLine has been in preview for an extended period; the API is
  stable in practice but carries a nominal risk of breaking changes before GA
