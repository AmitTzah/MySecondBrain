using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class PresentFilesToolExecutor : IToolExecutor
{
    private readonly ILogger<PresentFilesToolExecutor> _logger;

    public PresentFilesToolExecutor(ILogger<PresentFilesToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "present_files";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
