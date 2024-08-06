using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CSharpAnalyzer
{
    private List<CSType> CsTypes { get; } = [];

    private Dictionary<string, List<CSTypeMethod>> ExtendMethods { get; } = [];

    public void AnalyzeType(INamedTypeSymbol namedType)
    {
        try
        {
            if (namedType.IsNamespace)
            {
                return;
            }

            CSType csType = namedType switch
            {
                { TypeKind: TypeKind.Class or TypeKind.Struct } => AnalyzeClassType(namedType),
                { TypeKind: TypeKind.Interface } => AnalyzeInterfaceType(namedType),
                { TypeKind: TypeKind.Enum } => AnalyzeEnumType(namedType),
                { TypeKind: TypeKind.Delegate } => AnalyzeDelegateType(namedType),
                _ => new CSType()
            };
            CsTypes.Add(csType);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public List<CSType> GetCsTypes()
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
        var field = new CSTypeField();
        FillBaeInfo(symbol, field);
        field.Comment = GetXmlSummaryComment(symbol);
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

        var method = new CSTypeMethod();
        FillBaeInfo(methodSymbol, method);
        var xmlDictionary = GetXmlComment(methodSymbol);
        if (xmlDictionary.TryGetValue("<summary>", out var summary))
        {
            method.Comment = summary;
        }
        method.IsStatic = methodSymbol.IsStatic;
        method.ReturnTypeName = methodSymbol.ReturnType.ToDisplayString();
        if (methodSymbol.IsExtensionMethod)
        {
            method.IsStatic = false;
            var parameters = methodSymbol.Parameters;
            var thisParameter = parameters.FirstOrDefault();
            var thisType = thisParameter?.Type;
            if (thisType is not INamedTypeSymbol namedTypeSymbol) return;
            method.Params = methodSymbol.Parameters
                .Skip(1)
                .Select(it => new CSParam()
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString(),
                    Comment = xmlDictionary.GetValueOrDefault(it.Name, "")
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
                .Select(it => new CSParam()
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString(),
                    Comment = xmlDictionary.GetValueOrDefault(it.Name, "")
                }).ToList();
            csClassType.Methods.Add(method);
        }
    }

    private CSType AnalyzeClassType(INamedTypeSymbol symbol)
    {
        var csType = new CSClassType();
        FillNamespace(symbol, csType);

        if (!symbol.AllInterfaces.IsEmpty)
        {
            csType.Interfaces = symbol.AllInterfaces.Select(it => it.ToDisplayString()).ToList();
        }

        csType.BaseClass = symbol.BaseType?.ToString() ?? "";
        
        FillBaeInfo(symbol, csType);
        csType.Comment = GetXmlSummaryComment(symbol);
        
        if (symbol is { TypeArguments.Length: > 0 })
        {
            csType.GenericTypes = symbol.TypeArguments.Select(it => it.ToDisplayString()).ToList();
        }

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

    private CSType AnalyzeInterfaceType(INamedTypeSymbol symbol)
    {
        var csType = new CSInterface();
        FillNamespace(symbol, csType);

        if (!symbol.AllInterfaces.IsEmpty)
        {
            csType.Interfaces = symbol.AllInterfaces.Select(it => it.ToDisplayString()).ToList();
        }

        FillBaeInfo(symbol, csType);
        
        csType.Comment = GetXmlSummaryComment(symbol);

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

    private CSType AnalyzeEnumType(INamedTypeSymbol symbol)
    {
        var csType = new CSEnumType();

        FillNamespace(symbol, csType);
        FillBaeInfo(symbol, csType);
        
        csType.Comment = GetXmlSummaryComment(symbol);

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

    private CSType AnalyzeDelegateType(INamedTypeSymbol symbol)
    {
        var csType = new CSDelegate();
        FillNamespace(symbol, csType);
        FillBaeInfo(symbol, csType);
        csType.Comment = GetXmlSummaryComment(symbol);
        var invokeMethod = symbol.DelegateInvokeMethod;
        if (invokeMethod != null)
        {
            var method = new CSTypeMethod();
            method.ReturnTypeName = invokeMethod.ReturnType.ToDisplayString();
            method.Params = invokeMethod.Parameters
                .Select(it => new CSParam()
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
                return;
            }

            hasNamespace.Namespace = nsSymbol.ToString()!;
        }
        else if (symbol.ContainingSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            hasNamespace.Namespace = namedTypeSymbol.ToString()!;
        }
    }

    private void FillBaeInfo(ISymbol symbol, CSTypeBase typeBase)
    {
        typeBase.Name = symbol.Name;

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
                    $"{new Uri(location.SourceTree.FilePath)}#{lineSpan.Span.Start.Line}:{lineSpan.Span.Start.Character}";
            }
        }
    }
    
    private static string GetXmlSummaryComment(ISymbol symbol)
    {
        var comment = symbol.GetDocumentationCommentXml();
        if (comment is null)
        {
            return string.Empty;
        }
        
        comment = comment.Replace('\r', ' ').Trim();
        if (!comment.StartsWith("<member") && !comment.StartsWith("<summary"))
        {
            return "";
        }

        if (comment.StartsWith("<summary"))
        {
            comment = $"<parent>{comment}</parent>";
        }

        using var xmlDoc = XmlReader.Create(new StringReader(comment));
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
                        return xmlDoc.Value.Trim();
                    }

                    break;
                }
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, string> GetXmlComment(ISymbol symbol)
    {
        var comment = symbol.GetDocumentationCommentXml();
        if (comment is null)
        {
            return [];
        }
        
        comment = comment.Replace('\r', ' ').Trim();
        if (!comment.StartsWith("<member") && !comment.StartsWith("<summary"))
        {
            return [];
        }
    
        if (comment.StartsWith("<summary"))
        {
            comment = $"<parent>{comment}</parent>";
        }
    
        var result = new Dictionary<string, string>();
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
                            var summaryText = xmlDoc.Value.Trim();
                            result["<summary>"] = summaryText;
                        }
    
                        break;
                    }
                    case "param":
                    {
                        var paramName = xmlDoc.GetAttribute("name");
                        xmlDoc.Read();
                        if (xmlDoc.NodeType == XmlNodeType.Text && paramName is not null)
                        {
                            var paramValue = xmlDoc.Value.Trim();
                            result[paramName] = paramValue;
                        }
    
                        break;
                    }
                    case "returns":
                    {
                        var returnInfo = xmlDoc.Value;
                        result["<returns>"] = returnInfo;
                        break;
                    }
                }
            }
        }

        return result;
    }
}