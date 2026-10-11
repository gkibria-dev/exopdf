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

To merge PDFs:

1. Open the **Merge** page (selected by default) and choose a source folder — click
   **Browse…**, or drag a folder onto the window.
2. Review the file list; PDFs merge in file-name order (A–Z).
3. Click **Merge**. A progress bar tracks the merge, then the save; **Cancel** stops it
   before saving starts.
4. On success, use **Open file** or **Show in folder** to find the result.

The folder icon next to Browse opens **Recents** — up to 5 folders you've merged
before, most recent first. It only lists folders you've actually merged, and drops one
automatically if it can no longer be found.

## License

[MIT](LICENSE)
