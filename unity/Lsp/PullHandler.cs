using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using unity.core;

namespace unity.Lsp;

[Parallel, Method("api/pull")]
// ReSharper disable once ClassNeverInstantiated.Global
public class PullRequestParams : IRequest
{
    public string slnPath { get; set; } = "";

    public Dictionary<string, string> properties = new Dictionary<string, string>();
    public List<string> export { get; set; } = new List<string>();
}

[Parallel, Method("api/pull")]
public class PullHandler : IJsonRpcNotificationHandler<PullRequestParams>
{
    private readonly CSharpWorkspace _workspace;

    public PullHandler(CSharpWorkspace workspace)
    {
        _workspace = workspace;
    }

    public async Task<Unit> Handle(PullRequestParams request, CancellationToken cancellationToken)
    {
        _workspace.SetExportNamespace(request.export);
        await _workspace.OpenSolutionAsync(request.slnPath, request.properties);
        _workspace.GenerateDoc();
        return new Unit();
    }
}