namespace EmmyLua.Unity.Generator.Dump;

public interface IDumper
{
    public string Name { get; }
    
    public void Dump(List<CsType> csTypes, string outPath);
}