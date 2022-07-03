using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace unity.Util;

class LuaApiBase
{
    public string Name { get; set; }
    public string Comment { get; set; }
    public string Location { get; set; }
}

class LuaApiField : LuaApiBase
{
    public string TypeName;
}

class LuaApiMethod : LuaApiBase
{
    public string ReturnTypeName;
    public List<string> Params;
    public bool IsStatic;
}

class LuaClassRequest : LuaApiBase, IRequest
{
    public string Namespace;
    public string BaseClass;
    public List<LuaApiField> Fields;
    public List<LuaApiMethod> Methods;
}

public class GenerateJsonApi
{
    private LuaClassRequest? _currentClass;
    private ILanguageServer _server;

    public GenerateJsonApi(ILanguageServer server)
    {
        _currentClass = null;
        _server = server;
    }

    public void SendClass(ISymbol symbol)
    {
        if (symbol.Kind == SymbolKind.NamedType)
        {
            var classSymbol = (symbol as INamedTypeSymbol)!;
            SetClass(classSymbol);

            foreach (var field in classSymbol.GetMembers())
            {
                if (field.DeclaredAccessibility == Accessibility.Public)
                {
                    switch (field.Kind)
                    {
                        case SymbolKind.Property:
                        case SymbolKind.Field:
                        {
                            WriteClassField(field);
                            break;
                        }
                        case SymbolKind.Method:
                        {
                            WriteClassFunction((field as IMethodSymbol)!);
                            break;
                        }
                        default: break;
                    }
                }
            }

            _server.SendNotification("reportAPI", _currentClass!);
        }
    }

    private void WriteClassField(ISymbol fieldSymbol)
    {
        var field = new LuaApiField();
        FillBaeInfo(fieldSymbol, field);
        field.TypeName = fieldSymbol.ContainingType?.ToString() ?? "any";
        _currentClass?.Fields.Add(field);
    }

    private void WriteClassFunction( IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Name.StartsWith("get_") || methodSymbol.Name.StartsWith("set_"))
        {
            return;
        }

        var method = new LuaApiMethod();
        FillBaeInfo(methodSymbol, method);
        method.IsStatic = methodSymbol.IsStatic;
        method.ReturnTypeName = methodSymbol.ReturnType.Name;
        method.Params = methodSymbol.Parameters.Select(it => it.Name).ToList();
    }

    private void SetClass(INamedTypeSymbol symbol)
    {
        _currentClass = new LuaClassRequest();
        _currentClass.Namespace = symbol.ContainingNamespace.ToString() ?? "";
        _currentClass.BaseClass = symbol.BaseType?.ToString() ?? "";
        FillBaeInfo(symbol, _currentClass);
    }

    private void FillBaeInfo(ISymbol symbol, LuaApiBase apiBase)
    {
        if (_currentClass != null)
        {
            apiBase.Name = symbol.Name;
            if (!symbol.Locations.IsEmpty)
            {
                var location = symbol.Locations.First();
                if (location.IsInMetadata)
                {
                    apiBase.Location = location.MetadataModule?.ToString() ?? "";
                }
                else if (location.IsInSource)
                {
                    var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan);

                    apiBase.Location =
                        $"{new Uri(location.SourceTree.FilePath)}#{lineSpan.Span.Start.Line + 1}#{lineSpan.Span.Start.Character}";
                }
            }

            var comment = symbol.GetDocumentationCommentXml();
            if (comment != null)
            {
                apiBase.Comment = comment;
            }
        }
    }
}