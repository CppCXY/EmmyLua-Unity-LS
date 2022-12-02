using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Events;
using unity.Lsp;
using unity.Services;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .MinimumLevel.Verbose()
    .CreateLogger();

if (args.Length < 2)
{
    var server = await LanguageServer.From(options =>
    {
        IObserver<WorkDoneProgressReport> workDone = null!;
        if (args.Length == 0)
        {
            options.WithOutput(Console.OpenStandardOutput()).WithInput(Console.OpenStandardInput());
        }
        else
        {
            int port = Int32.Parse(args[0]);
            var tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
            EndPoint endPoint = new IPEndPoint(ipAddress, port);
            tcpServer.Bind(endPoint);
            tcpServer.Listen(1);

            var languageClientSocket = tcpServer.Accept();

            var networkStream = new NetworkStream(languageClientSocket);
            options.WithOutput(networkStream).WithInput(networkStream);
            Log.Logger.Debug("net stream create !");
        }

        options
            .WithHandler<WorkspaceHandler>()
            .WithHandler<PullHandler>()
            .ConfigureLogging(
                x => x
                    .AddLanguageProtocolLogging()
                    .SetMinimumLevel(LogLevel.Debug)
            )
            .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
            .WithServices(
                services => { services.AddSingleton(new CSharpWorkspace()); }
            )
            .OnInitialize(
                async (server, request, token) =>
                {
                    var manager = server.WorkDoneManager.For(
                        request, new WorkDoneProgressBegin
                        {
                            Title = "EmmyLua Unity Server is starting...",
                            Percentage = 10,
                        }
                    );
                    workDone = manager;
                    var json = request.InitializationOptions as JObject;
                    Log.Logger.Debug("workspace initialize ...");
                    try
                    {
                        if (json?["sln"] != null)
                        {
                            var workspace = server.Services.GetService<CSharpWorkspace>();
                            if (workspace != null)
                            {
                                await workspace.OpenSolutionAsync(json.Value<string>("sln")!);
                                workspace.Server = server;
                                if (json["export"] != null)
                                {
                                    workspace.SetExportNamespace(json["export"]?.Values<string>().ToList()!);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Logger.Debug($"{e.Message} stacktrace: {e.StackTrace}");
                    }

                    manager.OnNext(
                        new WorkDoneProgressReport
                        {
                            Percentage = 20,
                            Message = "loading in process"
                        }
                    );
                }
            ).OnInitialized((server, request, response, token) =>
                {
                    workDone.OnNext(
                        new WorkDoneProgressReport
                        {
                            Percentage = 40,
                            Message = "loading unity project...",
                        }
                    );

                    var workspace = server.Services.GetService<CSharpWorkspace>();
                    if (workspace != null)
                    {
                        workspace.GenerateDoc();
                    }

                    Log.Logger.Debug("workspace completed...");
                    workDone.OnNext(
                        new WorkDoneProgressReport
                        {
                            Message = "loading done",
                            Percentage = 100,
                        }
                    );
                    workDone.OnCompleted();
                    return Task.CompletedTask;
                }
            );
    }).ConfigureAwait(false);
    await server.WaitForExit.ConfigureAwait(false);
}
else
{
    var path = args[0];
    var ns = args[1];
    var workspace = new CSharpWorkspace();
    await workspace.OpenSolutionAsync(path);
    workspace.SetExportNamespace(ns.Split(';').ToList());
    workspace.GenerateDocStdout();
}