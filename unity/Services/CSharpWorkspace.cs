using Microsoft.CodeAnalysis;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using unity.Util;

namespace unity.Services;

public class CSharpWorkspace
{
    private readonly MSBuildWorkspace _workspace;
    private Compilation? _compilation;
    private Dictionary<string, SyntaxTree> _treeMap;
    private List<string> _exportNamespace;

    public CSharpWorkspace()
    {
        MSBuildLocator.RegisterDefaults();
        _workspace = MSBuildWorkspace.Create();
        _treeMap = new Dictionary<string, SyntaxTree>();
        _exportNamespace = new List<string>()
        {
            "UnityEngine"
        };
    }

    public ILanguageServer? Server { get; set; }

    public async Task<bool> OpenSolutionAsync(string path)
    {
        Log.Logger.Debug("open solution ...");
        var solution = await _workspace.OpenSolutionAsync(path);
        Log.Logger.Debug("open solution completion , start assembly ...");
        
        var project = solution?.Projects.FirstOrDefault(it => it?.Name == "Assembly-CSharp", null);

        if (project != null)
        {
            _compilation = await project.GetCompilationAsync(CancellationToken.None);
            if (_compilation != null)
            {
                foreach (var tree in _compilation.SyntaxTrees)
                {
                    _treeMap.Add(tree.FilePath, tree);
                }
            }
        }

        return _compilation != null;
    }

    public void ApplyChange(string path)
    {
        var text = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(text, null, path);
        if (_treeMap.TryGetValue(path, out var oldTree))
        {
            _compilation = _compilation?.ReplaceSyntaxTree(oldTree, tree);
            _treeMap[path] = tree;
        }
        else
        {
            _compilation = _compilation?.AddSyntaxTrees(tree);
            _treeMap.Add(path, tree);
        }
    }

    public void ApplyDelete(string path)
    {
        if (_treeMap.TryGetValue(path, out var tree))
        {
            _compilation = _compilation?.RemoveSyntaxTrees(tree);
            _treeMap.Remove(path);
        }
    }

    public void SetExportNamespace(List<string> exportNamespace)
    {
        _exportNamespace = exportNamespace;
    }
    
    public void GenerateDoc()
    {
        if (_compilation == null)
        {
            return;
        }

        var finder = new CustomSymbolFinder();

        var symbols = finder.GetAllSymbols(_compilation, _exportNamespace);
        
        var generate = new GenerateJsonApi(Server!);
        try
        {
            generate.Begin();
            foreach (var symbol in symbols)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (symbol != null && symbol.DeclaredAccessibility == Accessibility.Public)
                {
                    generate.WriteClass(symbol);
                }
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
}