# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ExoPdf** — a Windows utility for common PDF manipulation tasks. Available as a CLI (`ExoPdf.Cli`) and a desktop GUI (`ExoPdf.Desktop`). All PDF logic lives in a shared `ExoPdf.Core` library.

## Commands

```powershell
# Build
dotnet build ExoPdf.sln

# Run CLI
dotnet run --project ExoPdf.Cli\ExoPdf.Cli.csproj -- merge <folder>

# Run Desktop
dotnet run --project ExoPdf.Desktop\ExoPdf.Desktop.csproj

# Publish CLI self-contained executable
dotnet publish ExoPdf.Cli\ExoPdf.Cli.csproj -c Release -r win-x64 --self-contained
```

## Architecture

Three-project solution. See [docs/architecture.md](docs/architecture.md) for full detail.

- **`ExoPdf.Core`** — all PDF logic; no UI dependencies
- **`ExoPdf.Cli`** — CLI adapter using `System.CommandLine`
- **`ExoPdf.Desktop`** — WPF GUI using `CommunityToolkit.Mvvm`

## Docs

- [Development workflow](docs/agentic-workflow.md) — branch, plan, implement, review, PR cycle
- [Requirements](docs/requirements.md) — goals, operations, non-goals
- [Architecture](docs/architecture.md) — solution structure, layer responsibilities, naming conventions
- [ADRs](docs/adr/) — one file per significant decision
- Plans: `docs/plans/` — one file per feature/fix, created before implementation
