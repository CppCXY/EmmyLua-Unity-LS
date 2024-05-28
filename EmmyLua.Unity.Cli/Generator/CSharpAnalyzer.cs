using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CSharpAnalyzer
{
    private List<CsType> CsTypes { get; } = new();

    private Dictionary<string, List<CsTypeMethod>> ExtendMethods { get; } = new();

    public void AnalyzeType(INamedTypeSymbol namedType)
    {
        try
        {
            if (namedType.IsNamespace)
            {
                return;
            }

            CsType csType = namedType switch
            {
                { TypeKind: TypeKind.Class or TypeKind.Struct } => AnalyzeClassType(namedType),
                { TypeKind: TypeKind.Interface } => AnalyzeInterfaceType(namedType),
                { TypeKind: TypeKind.Enum } => AnalyzeEnumType(namedType),
                { TypeKind: TypeKind.Delegate } => AnalyzeDelegateType(namedType),
                _ => new CsType()
            };
            CsTypes.Add(csType);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public List<CsType> GetCsTypes()
    {
        if (ExtendMethods.Count != 0)
        {
            foreach (var csType in CsTypes)
            {
                if (ExtendMethods.TryGetValue(csType.Name, out var methods))
                {
                    if (csType is IHasMethods hasMethods)
                    {
                        hasMethods.Methods.AddRange(methods);
                    }
                }
            }

            ExtendMethods.Clear();
        }

        return CsTypes;
    }

    private void AnalyzeTypeFields(ISymbol symbol, IHasFields classType)
    {
        var field = new CsTypeField();
        FillBaeInfo(symbol, field);
        field.TypeName = symbol switch
        {
            IFieldSymbol fieldSymbol => fieldSymbol.Type.ToDisplayString(),
            IPropertySymbol propertySymbol => propertySymbol.Type.ToDisplayString(),
            IEventSymbol eventSymbol => eventSymbol.Type.ToDisplayString(),
            _ => "any"
        };

        classType.Fields.Add(field);
    }

    private void AnalyzeTypeMethods(IMethodSymbol methodSymbol, IHasMethods csClassType)
    {
        if (methodSymbol.Name.StartsWith("get_") || methodSymbol.Name.StartsWith("set_"))
        {
            return;
        }

        var method = new CsTypeMethod();
        FillBaeInfo(methodSymbol, method);
        method.IsStatic = methodSymbol.IsStatic;
        method.ReturnTypeName = methodSymbol.ReturnType.ToDisplayString();
        if (methodSymbol.IsExtensionMethod)
        {
            method.IsStatic = false;
            var parameters = methodSymbol.Parameters;
            var thisParameter = parameters.FirstOrDefault();
            if (thisParameter == null) return;
            var thisType = thisParameter.Type;
            if (thisType is not INamedTypeSymbol namedTypeSymbol) return;
            method.Params = methodSymbol.Parameters
                .Skip(1)
                .Select(it => new LuaParam()
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString()
                }).ToList();

            if (ExtendMethods.TryGetValue(namedTypeSymbol.Name, out var extendMethod))
            {
                extendMethod.Add(method);
            }
            else
            {
                ExtendMethods.Add(namedTypeSymbol.Name, [method]);
            }
        }
        else
        {
            method.Params = methodSymbol.Parameters
                .Select(it => new LuaParam()
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString()
                }).ToList();
            csClassType.Methods.Add(method);
        }
    }

    private CsType AnalyzeClassType(INamedTypeSymbol symbol)
    {
        var csType = new CsClassType();
        FillNamespace(symbol, csType);

        if (!symbol.AllInterfaces.IsEmpty)
        {
            csType.Interfaces = symbol.AllInterfaces.Select(it => it.ToDisplayString()).ToList();
        }

        csType.BaseClass = symbol.BaseType?.ToString() ?? "";
        FillBaeInfo(symbol, csType);

        foreach (var member in symbol.GetMembers().Where(it => it is { DeclaredAccessibility: Accessibility.Public }))
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    AnalyzeTypeFields(fieldSymbol, csType);
                    break;
                case IMethodSymbol methodSymbol:
                    AnalyzeTypeMethods(methodSymbol, csType);
                    break;
            }
        }

        return csType;
    }

    private CsType AnalyzeInterfaceType(INamedTypeSymbol symbol)
    {
        var csType = new CsInterface();
        FillNamespace(symbol, csType);

        if (!symbol.AllInterfaces.IsEmpty)
        {
            csType.Interfaces = symbol.AllInterfaces.Select(it => it.ToDisplayString()).ToList();
        }

        FillBaeInfo(symbol, csType);

        foreach (var member in symbol.GetMembers().Where(it => it is { DeclaredAccessibility: Accessibility.Public }))
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    AnalyzeTypeFields(fieldSymbol, csType);
                    break;
                case IMethodSymbol methodSymbol:
                    AnalyzeTypeMethods(methodSymbol, csType);
                    break;
            }
        }

        return csType;
    }

    private CsType AnalyzeEnumType(INamedTypeSymbol symbol)
    {
        var csType = new CsEnumType();

        FillNamespace(symbol, csType);
        FillBaeInfo(symbol, csType);

        foreach (var member in symbol.GetMembers().Where(it => it is { DeclaredAccessibility: Accessibility.Public }))
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    AnalyzeTypeFields(fieldSymbol, csType);
                    break;
            }
        }

        return csType;
    }

    private CsType AnalyzeDelegateType(INamedTypeSymbol symbol)
    {
        var csType = new CsDelegate();
        FillNamespace(symbol, csType);
        FillBaeInfo(symbol, csType);
        var invokeMethod = symbol.DelegateInvokeMethod;
        if (invokeMethod != null)
        {
            var method = new CsTypeMethod();
            method.ReturnTypeName = invokeMethod.ReturnType.ToDisplayString();
            method.Params = invokeMethod.Parameters
                .Select(it => new LuaParam()
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString()
                }).ToList();
            csType.InvokeMethod = method;
        }

        return csType;
    }

    private void FillNamespace(INamedTypeSymbol symbol, IHasNamespace hasNamespace)
    {
        if (symbol.ContainingSymbol is INamespaceSymbol nsSymbol)
        {
            if (nsSymbol.IsGlobalNamespace)
            {
                hasNamespace.Namespace = string.Empty;
            }

            hasNamespace.Namespace = nsSymbol.ToString()!;
        }
        else if (symbol.ContainingSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            hasNamespace.Namespace = namedTypeSymbol.ToString()!;
        }
    }

    private void FillBaeInfo(ISymbol symbol, CsTypeBase typeBase)
    {
        typeBase.Name = symbol.Name;
        if (symbol is INamedTypeSymbol { TypeArguments.Length: > 0 } namedTypeSymbol)
        {
            typeBase.Name += "<" + string.Join(",", namedTypeSymbol.TypeArguments.Select(x => x.ToDisplayString())) +
                             ">";
        }

        if (!symbol.Locations.IsEmpty)
        {
            var location = symbol.Locations.First();
            if (location.IsInMetadata)
            {
                typeBase.Location = location.MetadataModule?.ToString() ?? "";
            }
            else if (location.IsInSource)
            {
                var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan);

                typeBase.Location =
                    $"{new Uri(location.SourceTree.FilePath)}#{lineSpan.Span.Start.Line + 1}:{lineSpan.Span.Start.Character + 1}";
            }
        }

        var comment = symbol.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(comment))
        {
            typeBase.Comment = HandleXmlComment(comment);
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