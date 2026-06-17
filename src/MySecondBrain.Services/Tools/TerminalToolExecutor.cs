using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class TerminalToolExecutor : IToolExecutor
{
    private readonly ILogger<TerminalToolExecutor> _logger;

    public TerminalToolExecutor(ILogger<TerminalToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "terminal";

    public bool RequiresUserConfirmation => true;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.High;

    public bool CanAutoApprove => false;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
