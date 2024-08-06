using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EmmyLua.Unity.Generator.XLua;

public class XLuaClassFinder
{
    public List<INamedTypeSymbol> GetAllValidTypes(Compilation compilation)
    {
        var luaCallCSharpMembers = new List<INamedTypeSymbol>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // 查找所有类声明
            var typeDeclarationSyntaxes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();

            foreach (var typeDeclaration in typeDeclarationSyntaxes)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;

                // 查找类上的属性
                if (typeSymbol?.GetAttributes().Any(attr => attr.AttributeClass?.Name == "LuaCallCSharpAttribute") ==
                    true)
                {
                    luaCallCSharpMembers.Add(typeSymbol);
                }

                // 如果是类，查找类中的字段
                if (typeDeclaration is ClassDeclarationSyntax classDeclaration)
                {
                    var fieldDeclarations = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>();
                    foreach (var fieldDeclaration in fieldDeclarations)
                    {
                        if (semanticModel.GetDeclaredSymbol(fieldDeclaration.Declaration.Variables.First()) is
                            IFieldSymbol
                            {
                                IsStatic: true
                            } fieldSymbol)
                        {
                            if (fieldSymbol.GetAttributes()
                                .Any(attr => attr.AttributeClass?.Name == "LuaCallCSharpAttribute"))
                            {
                                luaCallCSharpMembers.AddRange(AnalyzeLuaCallCSharpMembers(fieldSymbol, semanticModel));
                            }
                        }
                    }
                }
            }
        }

        return luaCallCSharpMembers;
    }

    // 通过分析列表获得所有类
    private List<INamedTypeSymbol> AnalyzeLuaCallCSharpMembers(IFieldSymbol fieldSymbol, SemanticModel semanticModel)
    {
        var luaCallCSharpMembers = new List<INamedTypeSymbol>();

        // 检查字段类型是否为List<Type>
        if (fieldSymbol.Type is INamedTypeSymbol namedTypeSymbol &&
            namedTypeSymbol.ToString() == "System.Collections.Generic.List<System.Type>")
        {
            var variableDeclarator =
                fieldSymbol.DeclaringSyntaxReferences.First().GetSyntax() as VariableDeclaratorSyntax;

            if (variableDeclarator?.Initializer?.Value is ObjectCreationExpressionSyntax initializerValue)
            { 
                initializerValue.DescendantNodes().OfType<IdentifierNameSyntax>().ToList().ForEach(identifierName =>
                {
                    if (semanticModel.GetTypeInfo(identifierName).Type is INamedTypeSymbol typeSymbol)
                    {
                        luaCallCSharpMembers.Add(typeSymbol);
                    }
                });
            }
        }

        return luaCallCSharpMembers;
    }
}