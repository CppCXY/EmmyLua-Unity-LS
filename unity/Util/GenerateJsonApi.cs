using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;

namespace unity.Util;

class LuaApiBase
{
    public string Name { get; set; } = String.Empty;
    public string Comment { get; set; } = String.Empty;
    public string Location { get; set; } = String.Empty;
}

class LuaApiField : LuaApiBase
{
    public string TypeName = String.Empty;
}

class LuaApiMethod : LuaApiBase
{
    public string ReturnTypeName = String.Empty;
    public List<string> Params = new List<string>();
    public bool IsStatic;
}

class LuaClassRequest : LuaApiBase, IRequest
{
    public string Namespace = String.Empty;
    public string BaseClass = String.Empty;
    public List<LuaApiField> Fields = new List<LuaApiField>();
    public List<LuaApiMethod> Methods = new List<LuaApiMethod>();
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

    public void Begin()
    {
        _server.SendNotification("api/begin");
    }

    public void Finish()
    {
        _server.SendNotification("api/finish");
    }

    public void SendClass(ISymbol symbol)
    {
        if (symbol.Kind == SymbolKind.NamedType)
        {
            try
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
            }
            catch (Exception e)
            {
                Log.Logger.Error(e.Message);
            }

            _server.SendNotification("api/add", _currentClass!);
        }
    }

    private void WriteClassField(ISymbol fieldSymbol)
    {
        var field = new LuaApiField();
        FillBaeInfo(fieldSymbol, field);
        field.TypeName = fieldSymbol.ContainingType?.ToString() ?? "any";
        _currentClass?.Fields.Add(field);
    }

    private void WriteClassFunction(IMethodSymbol methodSymbol)
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
        _currentClass?.Methods.Add(method);
    }

    private void SetClass(INamedTypeSymbol symbol)
    {
        _currentClass = new LuaClassRequest();
        if (!symbol.ContainingNamespace.IsGlobalNamespace)
        {
            _currentClass.Namespace = symbol.ContainingNamespace.ToString() ?? "";
        }

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
                        $"{new Uri(location.SourceTree.FilePath)}#{lineSpan.Span.Start.Line + 1}";
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