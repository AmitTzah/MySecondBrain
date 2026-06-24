using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class MemoryToolExecutor : IToolExecutor
{
    private readonly ILogger<MemoryToolExecutor> _logger;

    public MemoryToolExecutor(ILogger<MemoryToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "memory";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
