# ExoPdf

PDF manipulation utility for .NET — available as a CLI and a desktop GUI.

## Operations

| Operation | Description |
|---|---|
| **Merge** | Merge all PDF files in a folder into one, preserving bookmarks |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

## Build

```bash
dotnet build ExoPdf.slnx
```

## CLI usage

```bash
# Merge all PDFs in a folder
dotnet run --project ExoPdf.Cli -- merge <folder>
```

The merged file is saved in the source folder as `Merge_<FolderName>_<timestamp>.pdf`.

Each source file becomes a top-level bookmark in the output. Existing bookmarks
within a source file are preserved as children.

## Desktop

Run the WPF desktop application:

```bash
dotnet run --project ExoPdf.Desktop
```

## License

[MIT](LICENSE)
