using System.Text;
using System.Xml;
using MediatR;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;

namespace unity.core;

class LuaApiBase
{
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

class LuaApiField : LuaApiBase
{
    public string TypeName = string.Empty;
}

class LuaParam
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
}

class LuaApiMethod : LuaApiBase
{
    public string ReturnTypeName = string.Empty;
    public List<LuaParam> Params = new List<LuaParam>();
    public bool IsStatic;
}

class LuaApiClass : LuaApiBase, IRequest
{
    public string Namespace = string.Empty;
    public string BaseClass = string.Empty;
    public string Attribute = string.Empty;
    public List<string> Interfaces = new List<string>();
    public List<LuaApiField> Fields = new List<LuaApiField>();
    public List<LuaApiMethod> Methods = new List<LuaApiMethod>();
}

class LuaReportApiParams
{
    public string Root = string.Empty;
    public List<LuaApiClass> Classes = new();
}

public class GenerateJsonApi
{
    private LuaApiClass? _currentClass;
    private readonly ILanguageServer _server;
    private readonly Dictionary<INamedTypeSymbol, LuaApiClass> _class2LuaApi = new();
    private readonly Dictionary<INamedTypeSymbol, List<LuaApiMethod>> _extendMethods = new();

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

    public void WriteClass(ISymbol symbol)
    {
        if (symbol.Kind != SymbolKind.NamedType) return;
        try
        {
            var classSymbol = (symbol as INamedTypeSymbol)!;
            if (_class2LuaApi.ContainsKey(classSymbol) || classSymbol.IsNamespace)
            {
                return;
            }

            if (classSymbol.BaseType is { IsGenericType: true })
            {
                WriteClass(classSymbol.BaseType);
            }

            SetClass(classSymbol);
            foreach (var field in classSymbol.GetMembers()
                         .Where(field => field.DeclaredAccessibility == Accessibility.Public))
            {
                switch (field.Kind)
                {
                    case SymbolKind.Property:
                    case SymbolKind.Field:
                    case SymbolKind.Event:
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

            _class2LuaApi.Add(classSymbol, _currentClass!);
        }
        catch (Exception e)
        {
            Log.Logger.Error(e.Message);
        }
    }

    public void SendAllClass()
    {
        foreach (var (type, method) in _extendMethods)
        {
            if (_class2LuaApi.TryGetValue(type, out var luaApiClass))
            {
                luaApiClass.Methods.AddRange(method);
            }
        }

        foreach (var (_, luaApiClass) in _class2LuaApi)
        {
            _server.SendNotification("api/add", luaApiClass);
        }
    }

    public void Output()
    {
        foreach (var (type, method) in _extendMethods)
        {
            if (_class2LuaApi.TryGetValue(type, out var luaApiClass))
            {
                luaApiClass.Methods.AddRange(method);
            }
        }

        var param = new LuaReportApiParams()
        {
            Root = "CS",
            Classes = _class2LuaApi.Select(it => it.Value).ToList(),
        };
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        Console.Write(JsonConvert.SerializeObject(param, settings));
    }

    private void WriteClassField(ISymbol symbol)
    {
        var field = new LuaApiField();
        FillBaeInfo(symbol, field);
        if (symbol is IFieldSymbol fieldSymbol)
        {
            field.TypeName = fieldSymbol.Type.ToString() ?? "any";
        }
        else if (symbol is IPropertySymbol propertySymbol)
        {
            field.TypeName = propertySymbol.Type.ToString() ?? "any";
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            field.TypeName = eventSymbol.Type.ToString() ?? "any";
        }

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
        method.ReturnTypeName = methodSymbol.ReturnType.ToString() ?? "any";
        if (methodSymbol.IsExtensionMethod)
        {
            method.IsStatic = false;
            var parameters = methodSymbol.Parameters;
            var thisParameter = parameters.FirstOrDefault();
            if (thisParameter == null) return;
            var thisType = thisParameter.Type;
            method.Params = methodSymbol.Parameters
                .Skip(1)
                .Select(it => new LuaParam()
                {
                    Name = it.Name + (it.IsOptional ? "?" : ""),
                    Kind = it.RefKind.ToString(),
                    TypeName = it.Type.ToString() ?? "any"
                }).ToList();
            
            if (thisType is not INamedTypeSymbol namedTypeSymbol) return;
            if (_extendMethods.TryGetValue(namedTypeSymbol, out var extendMethod))
            {
                extendMethod.Add(method);
            }
            else
            {
                _extendMethods.Add(namedTypeSymbol, new List<LuaApiMethod>() { method });
            }
        }
        else
        {
            method.Params = methodSymbol.Parameters
                .Select(it => new LuaParam()
            {
                Name = it.Name + (it.IsOptional ? "?" : ""),
                Kind = it.RefKind.ToString(),
                TypeName = it.Type.ToString() ?? "any"
            }).ToList();
            _currentClass?.Methods.Add(method);
        }
    }

    private void SetClass(INamedTypeSymbol symbol)
    {
        _currentClass = new LuaApiClass();

        if (symbol.ContainingSymbol != null)
        {
            _currentClass.Namespace = symbol.ContainingSymbol.ToString()!;
            if (_currentClass.Namespace == "<global namespace>")
            {
                _currentClass.Namespace = "";
            }
        }

        if (!symbol.AllInterfaces.IsEmpty)
        {
            _currentClass.Interfaces = symbol.AllInterfaces.Select(it => it.Name).ToList();
        }

        switch (symbol.TypeKind)
        {
            case TypeKind.Enum:
            {
                _currentClass.Attribute = "enum";
                // XLua Special
                var luaMethod = new LuaApiMethod()
                {
                    Name = "__CastFrom",
                    Location = "",
                    Params = new List<LuaParam>()
                    {
                        new LuaParam() { Name = "value", TypeName = "any" }
                    },
                    IsStatic = true,
                    ReturnTypeName = symbol.ToString() ?? "any"
                };

                _currentClass.Methods.Add(luaMethod);
                break;
            }
            case TypeKind.Interface:
                _currentClass.Attribute = "interface";
                break;
            case TypeKind.Delegate:
                _currentClass.Attribute = "delegate";
                break;
            case TypeKind.Struct:
                _currentClass.Attribute = "struct";
                break;
            case TypeKind.Class:
                _currentClass.Attribute = "class";
                break;
            default:
                break;
        }

        _currentClass.BaseClass = symbol.BaseType?.ToString() ?? "";
        FillBaeInfo(symbol, _currentClass);
    }

    private void FillBaeInfo(ISymbol symbol, LuaApiBase apiBase)
    {
        if (_currentClass == null) return;
        apiBase.Name = symbol.Name;
        if (symbol is INamedTypeSymbol { TypeArguments.Length: > 0 } namedTypeSymbol)
        {
            apiBase.Name += "<" + string.Join(",", namedTypeSymbol.TypeArguments.Select(x => x.ToDisplayString())) +
                            ">";
        }

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
        if (!string.IsNullOrEmpty(comment))
        {
            apiBase.Comment = HandleXmlComment(comment);
        }
    }

    private static string HandleXmlComment(string comment)
    {
        comment = comment.Replace('\r', ' ').Trim();
        if (!comment.StartsWith("<member") && !comment.StartsWith("<summary"))
        {
            return "";
        }

        if (comment.StartsWith("<summary"))
        {
            comment = $"<parent>{comment}</parent>";
        }

        var summaryText = string.Empty;
        var paramInfo = new List<string>();
        var returnInfo = string.Empty;
        using (var xmlDoc = XmlReader.Create(new StringReader(comment)))
        {
            while (xmlDoc.Read())
            {
                if (!xmlDoc.IsStartElement()) continue;
                switch (xmlDoc.Name)
                {
                    case "summary" or "para":
                    {
                        xmlDoc.Read();
                        if (xmlDoc.NodeType == XmlNodeType.Text)
                        {
                            summaryText = xmlDoc.Value.Trim();
                        }

                        break;
                    }
                    case "param":
                    {
                        var paramName = xmlDoc.GetAttribute("name");
                        xmlDoc.Read();
                        if (xmlDoc.NodeType == XmlNodeType.Text)
                        {
                            var paramValue = xmlDoc.Value.Trim();
                            paramInfo.Add($"{paramName} - {paramValue}");
                        }

                        break;
                    }
                    case "returns":
                        returnInfo = xmlDoc.Value;
                        break;
                }
            }
        }

        var sb = new StringBuilder();
        if (summaryText.Length != 0)
        {
            sb.Append(summaryText).Append("\n\n");
        }

        if (paramInfo.Count != 0)
        {
            const string paramDescription = "Params: ";
            var indentSize = paramDescription.Length;
            var indent = new string(' ', indentSize);
            sb.Append("```plaintext\n");
            sb.Append(paramDescription);
            foreach (var param in paramInfo)
            {
                sb.Append($"{param}\n{indent}");
            }

            sb.Append("\n```\n\n");
        }

        if (returnInfo.Length != 0)
        {
            var returnDescription = "Returns: ";
            sb.Append("```plaintext\n");
            sb.Append(returnDescription);
            sb.Append(returnInfo);
            sb.Append("```\n\n");
        }

        return sb.ToString();
    }
}