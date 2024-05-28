using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CsTypeBase
{
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class CsTypeField : CsTypeBase
{
    public string TypeName = string.Empty;
}

public class LuaParam
{
    public string Name { get; set; } = string.Empty;

    public bool Nullable { get; set; }

    public RefKind Kind { get; set; } = RefKind.None;

    public string TypeName { get; set; } = string.Empty;
}

public class CsTypeMethod : CsTypeBase
{
    public string ReturnTypeName = string.Empty;

    public List<LuaParam> Params = new List<LuaParam>();

    public bool IsStatic;
}

public interface IHasNamespace
{
    public string Namespace { get; set; }
}

public class CsType : CsTypeBase, IHasNamespace
{
    public string Namespace { get; set; } = string.Empty;
}

public interface IHasFields
{
    public List<CsTypeField> Fields { get; }
}

public interface IHasMethods
{
    public List<CsTypeMethod> Methods { get; }
}

public class CsClassType : CsType, IHasFields, IHasMethods
{
    public string BaseClass = string.Empty;

    public List<string> Interfaces { get; set; } = new List<string>();

    public List<CsTypeField> Fields { get; } = new List<CsTypeField>();

    public List<CsTypeMethod> Methods { get; } = new List<CsTypeMethod>();

    public bool IsStatic { get; set; }
}

public class CsEnumType : CsType, IHasFields
{
    public List<CsTypeField> Fields { get; } = new List<CsTypeField>();
}

public class CsInterface : CsType, IHasFields, IHasMethods
{
    public List<string> Interfaces { get; set; } = new List<string>();

    public List<CsTypeField> Fields { get; } = new List<CsTypeField>();

    public List<CsTypeMethod> Methods { get; } = new List<CsTypeMethod>();
}

public class CsDelegate : CsType
{
    public CsTypeMethod InvokeMethod { get; set; } = new CsTypeMethod();
}