using System.Net;
using Microsoft.CodeAnalysis;

namespace unity.Services;

public class GenerateDocument
{
    private readonly string _directoryPath;
    private StreamWriter? _currentFileStream;

    public GenerateDocument(string directoryPath)
    {
        _directoryPath = directoryPath;
        _currentFileStream = null;
    }

    public void WriteClass(ISymbol symbol)
    {
        if (symbol.Kind == SymbolKind.NamedType)
        {
            var classSymbol = (symbol as INamedTypeSymbol)!;
            SetFileStream(symbol);

            var classDoc = classSymbol.GetDocumentationCommentXml();

            WriteComment(classDoc);

            WriteClassDefine(classSymbol);

            foreach (var field in classSymbol.GetMembers())
            {
                if (field.DeclaredAccessibility == Accessibility.Public)
                {
                    switch (field.Kind)
                    {
                        case SymbolKind.Property:
                        case SymbolKind.Field:
                        {
                            WriteClassField(classSymbol, field);
                            break;
                        }
                        case SymbolKind.Method:
                        {
                            WriteClassFunction(classSymbol, (field as IMethodSymbol)!);
                            break;
                        }
                        default: break;
                    }
                }
            }

            _currentFileStream?.Close();
            _currentFileStream = null;
        }
    }


    private void WriteComment(string? comment)
    {
        if (comment != null)
        {
            var textList = comment.Split("\n");
            foreach (var text in textList)
            {
                _currentFileStream?.WriteLine($"--- {text}");
            }
        }
    }

    private void WriteClassDefine(INamedTypeSymbol classSymbol)
    {
        var className = classSymbol.Name;
        _currentFileStream?.Write($"---@class {classSymbol}");
        WriteClassExtend(classSymbol);
        _currentFileStream?.WriteLine($"local {className} = {{}}");
        _currentFileStream?.WriteLine($"CS.{classSymbol.ContainingNamespace} = {className}\n");
    }

    private void WriteClassExtend(INamedTypeSymbol classSymbol)
    {
        var baseClass = classSymbol.BaseType;
        if (baseClass != null)
        {
            _currentFileStream?.Write($" : {baseClass}");
        }

        var allInterface = classSymbol.AllInterfaces;
        if (!allInterface.IsEmpty)
        {
            var interfaceDescription = string.Join(",", allInterface.ToList().Select(it => it.ToString()));
            _currentFileStream?.Write($"@ implement {interfaceDescription}");
        }

        _currentFileStream?.Write("\n");
    }

    private void WriteClassField(INamedTypeSymbol classSymbol, ISymbol fieldSymbol)
    {
        var className = classSymbol.Name;
        var doc = fieldSymbol.GetDocumentationCommentXml();
        WriteComment(doc);
        _currentFileStream?.WriteLine($"---@type {fieldSymbol.ContainingType}");
        _currentFileStream?.WriteLine($"{className}.{fieldSymbol.Name} = {{}}\n");
    }

    private void WriteClassFunction(INamedTypeSymbol classSymbol, IMethodSymbol methodSymbol)
    {
        var className = classSymbol.Name;
        var doc = methodSymbol.GetDocumentationCommentXml();
        WriteComment(doc);
        WriteLocation(methodSymbol);
        if (methodSymbol.IsStatic)
        {
            _currentFileStream?.WriteLine(
                $"function {className}.{methodSymbol.Name}({MakeMethodArgList(methodSymbol)}) end\n");
        }
        else if (methodSymbol.Name == ".ctor")
        {
            _currentFileStream?.WriteLine($"function {className}:__call({MakeMethodArgList(methodSymbol)}) end\n");
        }
        else
        {
            _currentFileStream?.WriteLine(
                $"function {className}:{methodSymbol.Name}({MakeMethodArgList(methodSymbol)}) end\n");
        }
    }

    private void WriteLocation(ISymbol symbol)
    {
        if (!symbol.Locations.IsEmpty)
        {
            var location = symbol.Locations.First();
            if (location.IsInMetadata)
            {
                _currentFileStream?.WriteLine($"---@reference {location.MetadataModule}");
            }
            else if (location.IsInSource)
            {
                var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan);
                
                _currentFileStream?.WriteLine($"---@reference {new Uri(location.SourceTree.FilePath)}#{lineSpan.Span.Start.Line + 1}#{lineSpan.Span.Start.Character}");
            }
        }
    }

    private string MakeMethodArgList(IMethodSymbol methodSymbol)
    {
        return string.Join(", ", methodSymbol.Parameters.ToList().Select(it => it.Name));
    }

    private void SetFileStream(ISymbol symbol)
    {
        _currentFileStream?.Close();
        var path = Path.Join(_directoryPath, $"{symbol.ContainingNamespace}.{symbol.Name}.lua");
        _currentFileStream = File.CreateText(path);
    }
}