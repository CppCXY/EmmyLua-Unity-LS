using Microsoft.CodeAnalysis;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace unity.Services;

public class CSharpWorkspace
{
    private readonly MSBuildWorkspace _workspace;
    private Compilation? _compilation;
    private Dictionary<string, SyntaxTree> _treeMap;

    
    public CSharpWorkspace()
    {
        MSBuildLocator.RegisterDefaults();
        _workspace = MSBuildWorkspace.Create();
        _treeMap = new Dictionary<string, SyntaxTree>();
    }

    public ILanguageServer? Server { get; set; }
    
    public async Task<bool> OpenSolution(string path)
    {
        var solution = await _workspace.OpenSolutionAsync(path);
        // _project = _solution?.Projects.FirstOrDefault(it => it?.Name == "Assembly-CSharp", null);
        var project = solution.Projects.FirstOrDefault(it => it?.Name == "unity", null);
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

    public void GenerateDoc()
    {
        if (_compilation == null)
        {
            return;
        }
        var finder = new CustomSymbolFinder();
        finder.Filter = new HashSet<string>()
        {
            "System",
            "unity",
            "unity.Services"
        };     
        
        var symbols = finder.GetAllSymbols(_compilation);
        
        foreach (var symbol in symbols)
        {
            Console.WriteLine(symbol);
        
            foreach (var member in symbol.GetMembers())
            {
                if (member.DeclaredAccessibility == Accessibility.Public)
                {
                    var doc = symbol.GetDocumentationCommentXml();
                    if (!string.IsNullOrEmpty(doc))
                    {
                        Log.Logger.Debug($"    Comment:\n{doc}");
                    }
                    if (member.Kind == SymbolKind.Field)
                    {
                        Log.Logger.Debug($"    Field:{member.Name}");
                    }
                    else if (member.Kind == SymbolKind.Property)
                    {
                        Log.Logger.Debug($"    property:{member.Name}");
                    }
                    else if (member.Kind == SymbolKind.Method)
                    {
                        Log.Logger.Debug($"    Method:{member.Name}");
                    }
                    else
                    {
                        Log.Logger.Debug($"    unknown#{member.Kind}:{member.Name}");
                    }
                }
            }
        }
    }
}