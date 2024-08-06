using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CSTypeBase
{
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class CSTypeField : CSTypeBase
{
    public string TypeName = string.Empty;
}

public class CSParam
{
    public string Name { get; set; } = string.Empty;

    public bool Nullable { get; set; }

    public RefKind Kind { get; set; } = RefKind.None;

    public string TypeName { get; set; } = string.Empty;
    
    public string Comment { get; set; } = string.Empty;
}

public class CSTypeMethod : CSTypeBase
{
    public string ReturnTypeName = string.Empty;

    public List<CSParam> Params = [];

    public bool IsStatic;
}

public interface IHasNamespace
{
    public string Namespace { get; set; }
}

public class CSType : CSTypeBase, IHasNamespace
{
    public string Namespace { get; set; } = string.Empty;
}

public interface IHasFields
{
    public List<CSTypeField> Fields { get; }
}

public interface IHasMethods
{
    public List<CSTypeMethod> Methods { get; }
}

public class CSClassType : CSType, IHasFields, IHasMethods
{
    public string BaseClass = string.Empty;

    public List<string> GenericTypes { get; set; } = [];

    public List<string> Interfaces { get; set; } = [];

    public List<CSTypeField> Fields { get; } = [];

    public List<CSTypeMethod> Methods { get; } = [];

    public bool IsStatic { get; set; }
}

public class CSEnumType : CSType, IHasFields
{
    public List<CSTypeField> Fields { get; } = [];
}

public class CSInterface : CSType, IHasFields, IHasMethods
{
    public List<string> Interfaces { get; set; } = [];

    public List<CSTypeField> Fields { get; } = [];

    public List<CSTypeMethod> Methods { get; } = [];
}

public class CSDelegate : CSType
{
    public CSTypeMethod InvokeMethod { get; set; } = new();
}