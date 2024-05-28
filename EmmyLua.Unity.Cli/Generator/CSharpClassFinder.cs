using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CustomSymbolFinder
{
    public static List<INamedTypeSymbol> GetAllSymbols(Compilation compilation, List<string> filterNamespace)
    {
        var visitor = new FindAllSymbolsVisitor(filterNamespace);
        visitor.Visit(compilation.GlobalNamespace);
        return visitor.AllTypeSymbols.ToList();
    }

    // public HashSet<string> Filter { get; set; } = new HashSet<string>();

    private class FindAllSymbolsVisitor(List<string> filter) : SymbolVisitor
    {
        public ConcurrentBag<INamedTypeSymbol> AllTypeSymbols { get; } = new ConcurrentBag<INamedTypeSymbol>();

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            Parallel.ForEach(symbol.GetMembers(), s => s.Accept(this));
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public
                || !IsAllowNamespacePrefix(symbol.ContainingNamespace)) return;
            
            AllTypeSymbols.Add(symbol);
            foreach (var childSymbol in symbol.GetTypeMembers())
            {
                base.Visit(childSymbol);
            }
        }

        private bool IsAllowNamespacePrefix(INamespaceSymbol symbol)
        {
            if (symbol.IsGlobalNamespace)
            {
                return true;
            }

            var namespaceString = symbol.ToString();
            return namespaceString != null && filter.Any(prefix => namespaceString.StartsWith(prefix));
        }
    }
}