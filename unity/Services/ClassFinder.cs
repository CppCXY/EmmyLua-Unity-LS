using Microsoft.CodeAnalysis;

namespace unity.Services;

public class CustomSymbolFinder
{
    public List<INamedTypeSymbol> GetAllSymbols(Compilation compilation)
    {
        var visitor = new FindAllSymbolsVisitor();
        visitor.Visit(compilation.GlobalNamespace);
        return visitor.AllTypeSymbols;
    }

    // public HashSet<string> Filter { get; set; } = new HashSet<string>();

    private class FindAllSymbolsVisitor : SymbolVisitor
    {
        // private readonly HashSet<string> _filter;

        // public FindAllSymbolsVisitor()
        // {
        //     // _filter = filter;
        // }

        public List<INamedTypeSymbol> AllTypeSymbols { get; } = new List<INamedTypeSymbol>();

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            if (symbol.IsGlobalNamespace
                || symbol.ToString()!.StartsWith("System"))
            {
                // Parallel.ForEach(symbol.GetMembers(), s => s.Accept(this));
                foreach (var member in symbol.GetMembers())
                {
                    member.Accept(this);
                }
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.DeclaredAccessibility == Accessibility.Public)
            {
                AllTypeSymbols.Add(symbol);
                foreach (var childSymbol in symbol.GetTypeMembers())
                {
                    base.Visit(childSymbol);
                }
            }
        }
    }
}