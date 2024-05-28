using CommandLine;
using EmmyLua.Unity.Generator;
using Microsoft.Build.Locator;

Console.WriteLine("Try Location MSBuild ...");
MSBuildLocator.RegisterDefaults();

Parser.Default
    .ParseArguments<GenerateOptions>(args)
    .WithParsed<GenerateOptions>(o =>
    {
        var docGenerator = new CSharpDocGenerator(o);
        var exitCode = docGenerator.Run();
        Environment.Exit(exitCode.GetAwaiter().GetResult());
    });