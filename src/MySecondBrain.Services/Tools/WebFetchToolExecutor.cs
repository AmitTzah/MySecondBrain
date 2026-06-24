using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class WebFetchToolExecutor : IToolExecutor
{
    private readonly ILogger<WebFetchToolExecutor> _logger;

    public WebFetchToolExecutor(ILogger<WebFetchToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "web_fetch";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
