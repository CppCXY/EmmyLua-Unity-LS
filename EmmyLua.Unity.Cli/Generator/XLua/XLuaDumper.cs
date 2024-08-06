using System.Text;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator.XLua;

public class XLuaDumper : IDumper
{
    public string Name => "XLuaDumper";

    // 500kb
    private static readonly int SingleFileLength = 500 * 1024;

    private int Count { get; set; } = 0;

    private Dictionary<string, bool> NamespaceDict { get; } = new ();

    public void Dump(List<CSType> csTypes, string outPath)
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
                case CSClassType csClassType:
                    HandleCsClassType(csClassType, sb);
                    break;
                case CSInterface csInterface:
                    HandleCsInterface(csInterface, sb);
                    break;
                case CSEnumType csEnumType:
                    HandleCsEnumType(csEnumType, sb);
                    break;
                case CSDelegate csDelegate:
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
        foreach (var (namespaceString, isNamespace) in NamespaceDict)
        {
            if (isNamespace)
            {
                sb.AppendLine($"---@type namespace <\"{namespaceString}\">\nCS.{namespaceString} = {{}}");
            }
            else
            {
                sb.AppendLine($"---@type {namespaceString}\nCS.{namespaceString} = {{}}");
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

    private void HandleCsClassType(CSClassType csClassType, StringBuilder sb)
    {
        if (csClassType.Namespace.Length > 0)
        {
            var firstNamespace = csClassType.Namespace.Split('.').FirstOrDefault();
            if (firstNamespace != null)
            {
                NamespaceDict.TryAdd(firstNamespace, true);
            }
        }
        else
        {
            NamespaceDict.TryAdd(csClassType.Name, false);
        }

        var classFullName = csClassType.Name;
        if (csClassType.Namespace.Length > 0)
        {
            classFullName = $"{csClassType.Namespace}.{csClassType.Name}";
        }

        WriteCommentAndLocation(csClassType.Comment, csClassType.Location, sb);
        WriteTypeAnnotation("class", classFullName, csClassType.BaseClass, csClassType.Interfaces,
            csClassType.GenericTypes, sb);
        if (!csClassType.IsStatic)
        {
            var ctors = GetCtorList(csClassType);
            if (ctors.Count > 0)
            {
                foreach (var ctor in ctors)
                {
                    var paramsString = string.Join(",",
                        ctor.Params.Select(it => $"{it.Name}: {Util.CovertToLuaTypeName(it.TypeName)}"));
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

            var outParams = new List<CSParam>();
            foreach (var param in csTypeMethod.Params)
            {
                if (param.Kind is RefKind.Out or RefKind.Ref)
                {
                    outParams.Add(param);
                }

                if (param.Kind != RefKind.Out)
                {
                    var comment = param.Comment;
                    if (comment.Length > 0)
                    {
                        comment = comment.Replace("\n", "\n---");
                        sb.AppendLine($"---@param {param.Name} {Util.CovertToLuaTypeName(param.TypeName)} {comment}");
                    }
                    else
                    {
                        sb.AppendLine($"---@param {param.Name} {Util.CovertToLuaTypeName(param.TypeName)}");
                    }
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

            var dot = csTypeMethod.IsStatic ? "." : ":";
            sb.Append($"function {csClassType.Name}{dot}{csTypeMethod.Name}(");
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
        List<string> genericTypes,
        StringBuilder sb)
    {
        sb.Append($"---@{tag} {fullName}");
        if (genericTypes.Count > 0)
        {
            sb.Append($"<{genericTypes[0]}>");
            for (var i = 1; i < genericTypes.Count; i++)
            {
                sb.Append($", {genericTypes[i]}");
            }

            sb.Append('>');
        }

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

        if (location.StartsWith("file://"))
        {
            location = location.Replace("\"", "'");
            sb.AppendLine($"---@source \"{location}\"");
        }
    }

    private void HandleCsInterface(CSInterface csInterface, StringBuilder sb)
    {
        sb.AppendLine($"---@interface {csInterface.Name}");
    }

    private void HandleCsEnumType(CSEnumType csEnumType, StringBuilder sb)
    {
        if (csEnumType.Namespace.Length > 0)
        {
            var firstNamespace = csEnumType.Namespace.Split('.').FirstOrDefault();
            if (firstNamespace != null)
            {
                NamespaceDict.TryAdd(firstNamespace, true);
            }
        }
        else
        {
            NamespaceDict.TryAdd(csEnumType.Name, false);
        }

        var classFullName = csEnumType.Name;
        if (csEnumType.Namespace.Length > 0)
        {
            classFullName = $"{csEnumType.Namespace}.{csEnumType.Name}";
        }

        WriteCommentAndLocation(csEnumType.Comment, csEnumType.Location, sb);
        WriteTypeAnnotation("enum", classFullName, string.Empty, [], [], sb);
        
        sb.AppendLine($"local {csEnumType.Name} = {{}}");
        foreach (var csTypeField in csEnumType.Fields)
        {
            WriteCommentAndLocation(csTypeField.Comment, csTypeField.Location, sb);
            sb.AppendLine("---@type integer");
            sb.AppendLine($"{csEnumType.Name}.{csTypeField.Name} = nil");
            sb.AppendLine();
        }
    }

    private void HandleCsDelegate(CSDelegate csDelegate, StringBuilder sb)
    {
        var paramsString = string.Join(",",
            csDelegate.InvokeMethod.Params.Select(it => $"{it.Name}: {Util.CovertToLuaTypeName(it.TypeName)}"));
        sb.AppendLine($"---@alias {csDelegate.Name} fun({paramsString}): {Util.CovertToLuaTypeName(csDelegate.InvokeMethod.ReturnTypeName)}");
    }

    private List<CSTypeMethod> GetCtorList(CSClassType csClassType)
    {
        return csClassType.Methods.FindAll(method => method.Name == ".ctor");
    }
}