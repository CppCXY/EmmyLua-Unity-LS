using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Serilog;
using Serilog.Events;
using unity.Lsp;
using unity.core;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .MinimumLevel.Verbose()
    .CreateLogger();

MSBuildLocator.RegisterDefaults();

if (args.Length < 2)
{
    var server = new Server();
    await server.Start(args);
}
else
{
    var path = args[0];
    var ns = args[1];
    var workspace = new CSharpWorkspace();
    await workspace.OpenSolutionAsync(path, new Dictionary<string, string>());
    workspace.SetExportNamespace(ns.Split(';').ToList());
    workspace.GenerateDocStdout();
}