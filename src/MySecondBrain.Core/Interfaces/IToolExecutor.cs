using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IToolExecutor
{
    string ToolName { get; }
    bool RequiresUserConfirmation { get; }
    ToolRiskLevel RiskLevel { get; }
    bool CanAutoApprove { get; }

    Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct);

    Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct);

    string GetConfirmationDescription(ToolCall toolCall);
}
