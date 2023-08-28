using Microsoft.Build.Locator;
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
    await server.StartAsync(args);
}
else
{
    var path = args[0];
    var ns = args[1];
    var msbuildProperties = new Dictionary<string, string>();
    for (var i = 2; i < args.Length; i++)
    {
        var s = args[i].Split('=');
        if (s.Length >= 2)
        {
            msbuildProperties.Add(s[0], s[1]);
        }
    }

    var workspace = new CSharpWorkspace();
    await workspace.OpenSolutionAsync(path, msbuildProperties);
    workspace.SetExportNamespace(ns.Split(';').ToList());
    workspace.GenerateDocStdout();
}