using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using unity.core;

namespace unity.Lsp;

// ReSharper disable once ClassNeverInstantiated.Global
public class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly ILogger<TextDocumentHandler> _logger;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly CSharpWorkspace _workspace;

    private readonly DocumentSelector _documentSelector = new DocumentSelector(
        new DocumentFilter
        {
            Pattern = "**/*.cs"
        }
    );

    public TextDocumentHandler(
        ILogger<TextDocumentHandler> logger, 
        ILanguageServerConfiguration config,
        CSharpWorkspace workspace)
    {
        _logger = logger;
        _configuration = config;
        _workspace = workspace;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new TextDocumentAttributes(uri, "csharp");

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken) =>
        Unit.Task;

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken) =>
        Unit.Task;

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        _workspace.ApplyChange(request.TextDocument.Uri.GetFileSystemPath());
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken) =>
        Unit.Task;

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        SynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
        => new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _documentSelector,
            Save = new SaveOptions() { IncludeText = true }
        };
}