using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IToolOrchestrator
{
    Task<IReadOnlyList<ToolResult>> ProcessToolCallsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        ToolAutoApprovalSettings settings,
        CancellationToken ct);

    IReadOnlyList<ToolDefinition> GetAvailableToolDefinitions();

    bool IsToolEnabled(string toolName);

    ToolAutoApprovalSettings GetAutoApprovalSettings();
}
