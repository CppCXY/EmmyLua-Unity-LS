using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using unity.core;

namespace unity.Lsp;

public class WorkspaceHandler : IDidChangeWatchedFilesHandler
{
    private readonly ILogger<WorkspaceHandler> _logger;
    private readonly CSharpWorkspace _workspace;

    public WorkspaceHandler(
        ILogger<WorkspaceHandler> logger,
        CSharpWorkspace workspace)
    {
        _logger = logger;
        _workspace = workspace;
    }
    
    public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        foreach (var fileEvent in request.Changes)
        {
            switch (fileEvent.Type)
            {
                case FileChangeType.Changed:
                case FileChangeType.Created:
                {
                    var fsPath = fileEvent.Uri.GetFileSystemPath();
                    _workspace.ApplyChange(fsPath);
                    break;
                }
                case FileChangeType.Deleted:
                {
                    _workspace.ApplyDelete(fileEvent.Uri.GetFileSystemPath());
                    break;
                }
            }
        }

        return Unit.Task;
    }

    public DidChangeWatchedFilesRegistrationOptions GetRegistrationOptions(DidChangeWatchedFilesCapability capability,
        ClientCapabilities clientCapabilities)
        => new DidChangeWatchedFilesRegistrationOptions();
}