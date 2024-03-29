﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;

namespace unity.core;

public class CSharpWorkspace
{
    private List<string> _exportNamespace = new()
    {
        "UnityEngine"
    };
    private List<Compilation> _compilations = new();

    public ILanguageServer? Server { get; set; }

    public async Task OpenSolutionAsync(string path, Dictionary<string, string> msbuildProperties)
    {
        var workspace = MSBuildWorkspace.Create(msbuildProperties);
        Log.Logger.Debug("open solution ...");
        var solution = await workspace.OpenSolutionAsync(path);
        foreach (var diagnostic in workspace.Diagnostics)
        {
            Log.Logger.Debug(diagnostic.ToString() ?? string.Empty);
        }

        Log.Logger.Debug("open solution completion , start assembly ...");

        var projectCompilationList = new List<Compilation>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(CancellationToken.None);
            if (compilation != null) projectCompilationList.Add(compilation);
        }

        _compilations = projectCompilationList;
    }

    public void SetExportNamespace(List<string> exportNamespace)
    {
        _exportNamespace = exportNamespace;
    }

    public void GenerateDoc()
    {
        var generate = new GenerateJsonApi(Server!);
        try
        {
            generate.Begin();
            foreach (var symbol in from compilation in _compilations
                     let finder = new CustomSymbolFinder()
                     select CustomSymbolFinder.GetAllSymbols(compilation, _exportNamespace)
                     into symbols
                     from symbol in symbols.Where(symbol => symbol is { DeclaredAccessibility: Accessibility.Public })
                     select symbol)
            {
                generate.WriteClass(symbol);
            }

            generate.SendAllClass();
        }
        catch (Exception e)
        {
            Log.Logger.Error($"message: {e.Message}\n stacktrace: {e.StackTrace}");
        }
        finally
        {
            generate.Finish();
        }
    }

    public void GenerateDocStdout()
    {
        var generate = new GenerateJsonApi(Server!);
        try
        {
            foreach (var symbol in from compilation in _compilations
                     let finder = new CustomSymbolFinder()
                     select CustomSymbolFinder.GetAllSymbols(compilation, _exportNamespace)
                     into symbols
                     from symbol in symbols.Where(
                         symbol => symbol is { DeclaredAccessibility: Accessibility.Public })
                     select symbol)
            {
                generate.WriteClass(symbol);
            }

            generate.Output();
        }
        catch (Exception e)
        {
            Log.Logger.Error($"message: {e.Message}\n stacktrace: {e.StackTrace}");
        }
    }
}