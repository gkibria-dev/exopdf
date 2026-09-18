# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 8 console utility that merges all PDF files in a given folder into a single output PDF. Uses the [PDFsharp](https://github.com/empira/PDFsharp) library (v6.2.4).

## Commands

```powershell
# Build
dotnet build PdfUtil.sln

# Run
dotnet run --project PdfUtil\PdfUtil.csproj

# Publish self-contained executable
dotnet publish PdfUtil\PdfUtil.csproj -c Release -r win-x64 --self-contained
```

## Architecture

Single-file console app (`PdfUtil/Program.cs`):

- Prompts user for a folder path at runtime
- Scans the folder for `*.pdf` files
- Opens each PDF in `Import` mode via `PdfReader.Open` and copies all pages into a single `PdfDocument`
- Saves the merged result as `Merge_<FolderName>_<yyyyMMddHHmmss>.pdf` inside the same source folder

The PDF version of the output is set to match the last-processed input file's version (each file overwrites `outputPDFDocument.Version`).
