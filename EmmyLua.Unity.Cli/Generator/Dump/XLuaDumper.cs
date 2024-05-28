using System.Text;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator.Dump;

public class XLuaDumper : IDumper
{
    public string Name => "XLuaDumper";

    // 200kb
    private static readonly int SingleFileLength = 100 * 1024;

    private int Count { get; set; } = 0;
    
    private HashSet<string> NamespaceSet { get; } = new HashSet<string>();

    public void Dump(List<CsType> csTypes, string outPath)
    {
        if (!Directory.Exists(outPath))
        {
            Directory.CreateDirectory(outPath);
        }

        var sb = new StringBuilder();
        ResetSb(sb);
        foreach (var csType in csTypes)
        {
            switch (csType)
            {
                case CsClassType csClassType:
                    HandleCsClassType(csClassType, sb);
                    break;
                case CsInterface csInterface:
                    HandleCsInterface(csInterface, sb);
                    break;
                case CsEnumType csEnumType:
                    HandleCsEnumType(csEnumType, sb);
                    break;
                case CsDelegate csDelegate:
                    HandleCsDelegate(csDelegate, sb);
                    break;
            }

            sb.AppendLine();

            CacheOrDumpToFile(sb, outPath);
        }

        if (sb.Length > 0)
        {
            CacheOrDumpToFile(sb, outPath, true);
        }
        
        DumpNamespace(sb, outPath);
    }
    
    private void DumpNamespace(StringBuilder sb, string outPath)
    {
        sb.AppendLine("CS = {}");
        var wroteNameSet = new HashSet<string>();
        foreach (var namespaceString in NamespaceSet)
        {
            if (!wroteNameSet.Contains(namespaceString))
            {
                var parts = namespaceString.Split('.');
                var name = "CS";
                foreach (var part in parts)
                {
                    name += $".{part}";
                    if (wroteNameSet.Add(name))
                    {
                        sb.AppendLine($"{name} = {{}}");
                    }
                }
            }
        }
        
        var filePath = Path.Combine(outPath, "xlua_namespace.lua");
        File.WriteAllText(filePath, sb.ToString());
    }

    private void CacheOrDumpToFile(StringBuilder sb, string outPath, bool force = false)
    {
        if (sb.Length > SingleFileLength || force)
        {
            var filePath = Path.Combine(outPath, $"xlua_dump_{Count}.lua");
            File.WriteAllText(filePath, sb.ToString());
            ResetSb(sb);
            Count++;
        }
    }

    private void ResetSb(StringBuilder sb)
    {
        sb.Clear();
        sb.AppendLine("---@meta");
    }

    private void HandleCsClassType(CsClassType csClassType, StringBuilder sb)
    {
        NamespaceSet.Add(csClassType.Namespace);
        var classFullName = csClassType.Name;
        if (csClassType.Namespace.Length > 0)
        {
            classFullName = $"{csClassType.Namespace}.{csClassType.Name}";
        }

        WriteCommentAndLocation(csClassType.Comment, csClassType.Location, sb);
        WriteTypeAnnotation("class", classFullName, csClassType.BaseClass, csClassType.Interfaces, sb);
        if (!csClassType.IsStatic)
        {
            var ctors = GetCtorList(csClassType);
            if (ctors.Count > 0)
            {
                foreach (var ctor in ctors)
                {
                    var paramsString = string.Join(",", ctor.Params.Select(it => $"{it.Name}: {Util.CovertToLuaTypeName(it.TypeName)}"));
                    sb.AppendLine(
                        $"---@overload fun({paramsString}): {classFullName}");
                }
            }
            else
            {
                sb.AppendLine($"---@overload fun(): {classFullName}");
            }
        }

        sb.AppendLine($"local {csClassType.Name} = {{}}");
        if (csClassType.Namespace.Length > 0)
        {
            sb.AppendLine($"CS.{csClassType.Namespace}.{csClassType.Name} = {csClassType.Name}");
        }
        else
        {
            sb.AppendLine($"CS.{csClassType.Name} = {csClassType.Name}");
        }

        foreach (var csTypeField in csClassType.Fields)
        {
            WriteCommentAndLocation(csTypeField.Comment, csTypeField.Location, sb);
            sb.AppendLine($"---@type {Util.CovertToLuaTypeName(csTypeField.TypeName)}");
            sb.AppendLine($"{csClassType.Name}.{csTypeField.Name} = nil");
            sb.AppendLine();
        }

        foreach (var csTypeMethod in csClassType.Methods)
        {
            if (csTypeMethod.Name == ".ctor")
            {
                continue;
            }
            
            WriteCommentAndLocation(csTypeMethod.Comment, csTypeMethod.Location, sb);

            var outParams = new List<LuaParam>();
            foreach (var param in csTypeMethod.Params)
            {
                if (param.Kind is RefKind.Out or RefKind.Ref)
                {
                    outParams.Add(param);
                }

                if (param.Kind != RefKind.Out)
                {
                    sb.AppendLine($"---@param {param.Name} {Util.CovertToLuaTypeName(param.TypeName)}");
                }
            }
            
            sb.Append($"---@return {Util.CovertToLuaTypeName(csTypeMethod.ReturnTypeName)}");
            if (outParams.Count > 0)
            {
                for (var i = 0; i < outParams.Count; i++)
                {
                    sb.Append($"{outParams[i].TypeName}");
                    if (i < outParams.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }

                sb.AppendLine();
            }
            else
            {
                sb.AppendLine();
            }
            
            sb.Append($"function {csClassType.Name}:{csTypeMethod.Name}(");
            for (var i = 0; i < csTypeMethod.Params.Count; i++)
            {
                sb.Append(Util.CovertToLuaCompactName(csTypeMethod.Params[i].Name));
                if (i < csTypeMethod.Params.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.AppendLine(")");
            sb.AppendLine("end");
            sb.AppendLine();
        }
    }

    private void WriteTypeAnnotation(string tag, string fullName, string baseClass, List<string> interfaces,
        StringBuilder sb)
    {
        sb.Append($"---@{tag} {fullName}");
        if (!string.IsNullOrEmpty(baseClass))
        {
            sb.Append($": {baseClass}");
            foreach (var csInterface in interfaces)
            {
                sb.Append($", {csInterface}");
            }
        }
        else if (interfaces.Count > 0)
        {
            sb.Append($": {interfaces[0]}");
            for (var i = 1; i < interfaces.Count; i++)
            {
                sb.Append($", {interfaces[i]}");
            }
        }

        sb.Append('\n');
    }

    private void WriteCommentAndLocation(string comment, string location, StringBuilder sb)
    {
        if (comment.Length > 0)
        {
            sb.AppendLine($"---{comment.Replace("\n", "\n---")}");
        }

        if (location.Length > 0)
        {
            sb.AppendLine($"---@source {location}");
        }
    }

    private void HandleCsInterface(CsInterface csInterface, StringBuilder sb)
    {
        sb.AppendLine($"---@interface {csInterface.Name}");
    }

    private void HandleCsEnumType(CsEnumType csEnumType, StringBuilder sb)
    {
        // sb.AppendLine($"---@class {csEnumType.Name}");
        // sb.AppendLine($"---@field public {csEnumType.EnumType} {csEnumType.EnumType}");
    }

    private void HandleCsDelegate(CsDelegate csDelegate, StringBuilder sb)
    {
    }

    private List<CsTypeMethod> GetCtorList(CsClassType csClassType)
    {
        return csClassType.Methods.FindAll(method => method.Name == ".ctor");
    }
}