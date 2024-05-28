using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace EmmyLua.Unity.Generator;

public static class CSharpWorkspace
{
    public static async Task<List<Compilation>> OpenSolutionAsync(string path, Dictionary<string, string> msbuildProperties)
    {
        var workspace = MSBuildWorkspace.Create(msbuildProperties);
        var solution = await workspace.OpenSolutionAsync(path);
        var projectCompilationList = new List<Compilation>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(CancellationToken.None);
            if (compilation != null) projectCompilationList.Add(compilation);
        }
  
        return projectCompilationList;
    }
}