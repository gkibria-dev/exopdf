using System.IO.Abstractions;
using ExoPdf.Cli;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Operations;

// Composition root.
var fileSystem = new FileSystem();
var namer = new MergeOutputNamer(fileSystem, TimeProvider.System);
var merger = new PdfMerger(fileSystem, new MergeSourceFinder(fileSystem, namer), namer);

return CliApp.Run(args, merger);
