using System.CommandLine;
using ExoPdf.Cli.Commands;

var rootCommand = new RootCommand("ExoPdf — PDF manipulation utility");
rootCommand.Add(MergeCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
