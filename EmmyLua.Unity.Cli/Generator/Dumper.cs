namespace EmmyLua.Unity.Generator;

public interface IDumper
{
    public string Name { get; }
    
    public void Dump(List<CSType> csTypes, string outPath);
}