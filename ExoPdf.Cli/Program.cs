using System.IO.Abstractions;
using ExoPdf.Cli;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Operations;

// Composition root.
var fileSystem = new FileSystem();
var namer = new MergeOutputNamer(fileSystem, TimeProvider.System);
var merger = new PdfMerger(fileSystem, new MergeSourceFinder(fileSystem, namer), namer);

// Ctrl+C cancels the merge cleanly instead of killing the process mid-write.
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return CliApp.Run(args, merger, cancellationToken: cancellation.Token);
