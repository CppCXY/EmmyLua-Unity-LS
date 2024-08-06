using System.Collections.Concurrent;
using EmmyLua.Unity.Generator.XLua;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CustomSymbolFinder
{
    public static List<INamedTypeSymbol> GetAllSymbols(Compilation compilation, GenerateOptions o)
    {
        if (o.BindingType == LuaBindingType.XLua)
        {
            var finder = new XLuaClassFinder();
            return finder.GetAllValidTypes(compilation);
        }
        // var visitor = new FindAllSymbolsVisitor(filterNamespace);
        // visitor.Visit(compilation.GlobalNamespace);
        // result.AddRange(visitor.AllTypeSymbols.ToList());
        // return result;
        return [];
    }

    // private static List<INamedTypeSymbol> FindProjectLocalTypes(Compilation compilation)
    // {
    //     var result = new List<INamedTypeSymbol>();
    //
    //     foreach (var syntaxTree in compilation.SyntaxTrees)
    //     {
    //         var semanticModel = compilation.GetSemanticModel(syntaxTree);
    //         var root = syntaxTree.GetRoot();
    //
    //         var typeDeclarations = root.DescendantNodes()
    //             .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>();
    //
    //         foreach (var typeDeclaration in typeDeclarations)
    //         {
    //             if (semanticModel.GetDeclaredSymbol(typeDeclaration) is INamedTypeSymbol symbol)
    //             {
    //                 result.Add(symbol);
    //             }
    //         }
    //     }
    //
    //     return result;
    // }
    //
    // private class FindAllSymbolsVisitor(List<string> filter) : SymbolVisitor
    // {
    //     public ConcurrentBag<INamedTypeSymbol> AllTypeSymbols { get; } = new();
    //
    //     public override void VisitNamespace(INamespaceSymbol symbol)
    //     {
    //         var members = symbol.GetMembers().ToList();
    //         // Consider the number of members before deciding on parallel execution
    //         if (members.Count > 10) // This threshold can be adjusted based on actual use cases
    //         {
    //             Parallel.ForEach(members, s => s.Accept(this));
    //         }
    //         else
    //         {
    //             foreach (var member in symbol.GetMembers())
    //             {
    //                 member.Accept(this);
    //             }
    //         }
    //     }
    //
    //     public override void VisitNamedType(INamedTypeSymbol symbol)
    //     {
    //         if (symbol.DeclaredAccessibility == Accessibility.Public &&
    //             IsAllowNamespacePrefix(symbol.ContainingNamespace))
    //         {
    //             AllTypeSymbols.Add(symbol);
    //             foreach (var childSymbol in symbol.GetTypeMembers())
    //             {
    //                 base.Visit(childSymbol);
    //             }
    //         }
    //     }
    //
    //     private bool IsAllowNamespacePrefix(INamespaceSymbol symbol)
    //     {
    //         if (symbol.IsGlobalNamespace)
    //         {
    //             return false;
    //         }
    //
    //         var namespaceString = symbol.ToString();
    //         if (namespaceString == null)
    //         {
    //             return false;
    //         }
    //
    //         return filter.Any(f => namespaceString.StartsWith(f));
    //     }
    // }
}