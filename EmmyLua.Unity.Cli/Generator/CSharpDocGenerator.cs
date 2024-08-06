using EmmyLua.Unity.Generator.XLua;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CSharpDocGenerator(GenerateOptions o)
{
    public async Task<int> Run()
    {
        var slnPath = o.Solution;
        var namespaces = o.Namespace.Split(';').ToList();
        var msbuildProperties = new Dictionary<string, string>();
        foreach (var property in o.Properties)
        {
            var s = property.Split('=');
            if (s.Length >= 2)
            {
                msbuildProperties.Add(s[0], s[1]);
            }
        }

        try
        {
            Console.WriteLine($"Open solution {slnPath} ...");
            var compilations = await CSharpWorkspace.OpenSolutionAsync(slnPath, msbuildProperties);
            var analyzer = new CSharpAnalyzer();
            Console.WriteLine("Analyzing ...");
            foreach (var symbol in from compilation in compilations
                     let finder = new CustomSymbolFinder()
                     select CustomSymbolFinder.GetAllSymbols(compilation, o)
                     into symbols
                     from symbol in symbols.Where(
                         symbol => symbol is { DeclaredAccessibility: Accessibility.Public })
                     select symbol)
            {
                analyzer.AnalyzeType(symbol);
            }

            var csTypes = analyzer.GetCsTypes();
            switch (o.BindingType)
            {
                case LuaBindingType.XLua:
                    Console.WriteLine("Generating XLua binding ...");
                    var xLuaDumper = new XLuaDumper();
                    xLuaDumper.Dump(csTypes, o.Output);
                    break;
                case LuaBindingType.ToLua:
                    Console.WriteLine("Generating ToLua binding ...");

                    break;
                default:
                    Console.WriteLine("No binding type specified.");
                    break;
            }
            
            Console.WriteLine("Done.");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 1;
        }
    }
}