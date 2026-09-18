using System.CommandLine;
using System.IO.Abstractions;
using ExoPdf.Cli.Commands;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Operations;

// Composition root.
var fileSystem = new FileSystem();
var namer = new MergeOutputNamer(fileSystem, TimeProvider.System);
var merger = new PdfMerger(fileSystem, new MergeSourceFinder(fileSystem, namer), namer);

var rootCommand = new RootCommand("ExoPdf — PDF manipulation utility");
rootCommand.Add(MergeCommand.Build(merger));

return await rootCommand.Parse(args).InvokeAsync();
