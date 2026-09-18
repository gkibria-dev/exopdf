# ADR-001: Multi-project solution layout

## Status
Accepted

## Context

The tool started as a single console app (`ExoPdf/Program.cs`). Two planned
expansions make that structure untenable:

1. Multiple PDF operations (merge, split, extract, watermark, etc.)
2. Two UI frontends: a CLI and a desktop GUI

Without separating concerns, PDF logic, console I/O, and (eventually) UI code
will become entangled. Adding a desktop UI to a console project means either
duplicating the PDF logic or coupling the GUI to console-specific code.

## Decision

Split the solution into three projects:

```
ExoPdf.sln
├── ExoPdf.Core      ← class library: all PDF operations, no UI dependencies
├── ExoPdf.Cli       ← console app, references Core
└── ExoPdf.Desktop   ← WPF app, references Core
```

**Dependency rule:** Core has no knowledge of its callers. Cli and Desktop are
thin adapters that translate user gestures into Core calls and display results.

```
Cli ──→ Core
Desktop ──→ Core
Core ──→ (nothing in this solution)
```

## Consequences

- PDF logic is testable in isolation (no console or UI dependency required)
- Both frontends share identical behaviour — they call the same Core code
- A third frontend (web API, PowerShell module, etc.) can be added by referencing
  Core without touching existing projects
- Slightly more solution overhead for what is currently a small tool — accepted
  as the right investment given the stated roadmap
