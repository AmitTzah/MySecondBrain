using System.Text.Json;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IToolExecutor
{
    string ToolName { get; }

    /// <summary>
    /// Human-readable description of what this tool does.
    /// Used to construct the ToolDefinition for the LLM API.
    /// </summary>
    string Description => string.Empty;

    /// <summary>
    /// JSON schema string describing the tool's input parameters.
    /// Defaults to an empty object schema if not overridden.
    /// </summary>
    string ParametersJsonSchema => """{"type":"object","properties":{},"required":[]}""";

    bool RequiresUserConfirmation { get; }
    ToolRiskLevel RiskLevel { get; }
    bool CanAutoApprove { get; }

    Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct);

    Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct);

    string GetConfirmationDescription(ToolCall toolCall);
}
