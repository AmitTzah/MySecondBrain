using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class AskUserInputToolExecutor : IToolExecutor
{
    private readonly IConfirmationService _confirmationService;
    private readonly ILogger<AskUserInputToolExecutor> _logger;

    public AskUserInputToolExecutor(
        IConfirmationService confirmationService,
        ILogger<AskUserInputToolExecutor> logger)
    {
        _confirmationService = confirmationService;
        _logger = logger;
    }

    public string ToolName => "ask_user_input";

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolValidationResult>(default!);

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct) =>
        Task.FromResult<ToolResult>(default!);

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
