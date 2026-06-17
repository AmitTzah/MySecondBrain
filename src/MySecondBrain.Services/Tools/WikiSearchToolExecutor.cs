using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class WikiSearchToolExecutor : IToolExecutor
{
    private readonly IWikiService _wikiService;
    private readonly ILogger<WikiSearchToolExecutor> _logger;

    public WikiSearchToolExecutor(
        IWikiService wikiService,
        ILogger<WikiSearchToolExecutor> logger)
    {
        _wikiService = wikiService;
        _logger = logger;
    }

    public string ToolName => "wiki_search";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
