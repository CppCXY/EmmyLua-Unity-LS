using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using unity.core;

namespace unity.Lsp;

[Parallel, Method("api/pull")]
public class PullRequestParams : IRequest
{
    public List<string> export { get; set; } = new List<string>();
}

[Parallel, Method("api/pull")]
public class PullHandler : IJsonRpcNotificationHandler<PullRequestParams>
{
    private readonly ILogger<WorkspaceHandler> _logger;
    private readonly CSharpWorkspace _workspace;

    public PullHandler(
        ILogger<WorkspaceHandler> logger,
        CSharpWorkspace workspace)
    {
        _logger = logger;
        _workspace = workspace;
    }

    public Task<Unit> Handle(PullRequestParams request, CancellationToken cancellationToken)
    {
        _workspace.SetExportNamespace(request.export);
        _workspace.GenerateDoc();
        return Unit.Task;
    }
}