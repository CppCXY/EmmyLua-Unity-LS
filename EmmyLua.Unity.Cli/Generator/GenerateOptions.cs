using CommandLine;

namespace EmmyLua.Unity.Generator;

// ReSharper disable once ClassNeverInstantiated.Global
public class GenerateOptions
{
    [Option('s', "solution", Required = true, HelpText = "The path to the solution file(.sln).")]
    public string Solution { get; set; } = string.Empty;

    [Option('n', "namespace", Required = true, HelpText = "The namespace to export. split by ';' if multiple.")]
    public string Namespace { get; set; } = string.Empty;

    [Option('p', "properties", Required = false, HelpText = "The MSBuild properties.")]
    public IEnumerable<string> Properties { get; set; } = new List<string>();
    
    // for xlua or tolua
    [Option('b', "bind", Required = true, HelpText = "Generate XLua/ToLua binding.")]
    public LuaBindingType BindingType { get; set; } = LuaBindingType.None;
    
    [Option('o', "output", Required = true, HelpText = "The output path.")]
    public string Output { get; set; } = string.Empty;
    
    [Option('e', "export", Required = false, HelpText = "Export type.")]
    public LuaExportType ExportType { get; set; } = LuaExportType.None;
}

public enum LuaBindingType
{
    None,
    XLua,
    ToLua,
}

public enum LuaExportType
{
    None,
    Json,
    Lua
}