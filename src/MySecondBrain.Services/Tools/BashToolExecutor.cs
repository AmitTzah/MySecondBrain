using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class BashToolExecutor : IToolExecutor
{
    private readonly ILogger<BashToolExecutor> _logger;

    public BashToolExecutor(ILogger<BashToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "bash";

    public bool RequiresUserConfirmation => true;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Medium;

    public bool CanAutoApprove => false;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
