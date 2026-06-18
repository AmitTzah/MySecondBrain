using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class ToolOrchestrator : IToolOrchestrator
{
    private readonly IEnumerable<IToolExecutor> _executors;
    private readonly ILogger<ToolOrchestrator> _logger;

    public ToolOrchestrator(
        IEnumerable<IToolExecutor> executors,
        ILogger<ToolOrchestrator> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    public Task<IReadOnlyList<ToolResult>> ProcessToolCallsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        ToolAutoApprovalSettings settings,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ToolResult>>(Array.Empty<ToolResult>());

    public IReadOnlyList<ToolDefinition> GetAvailableToolDefinitions() =>
        Array.Empty<ToolDefinition>();

    public bool IsToolEnabled(string toolName) => false;

    public ToolAutoApprovalSettings GetAutoApprovalSettings() =>
        new();
}
