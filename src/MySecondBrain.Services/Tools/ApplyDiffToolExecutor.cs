using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class ApplyDiffToolExecutor : IToolExecutor
{
    private readonly ILogger<ApplyDiffToolExecutor> _logger;

    private const string Schema = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The path of the file to modify (absolute or relative to workspace)"
                },
                "diff": {
                    "type": "string",
                    "description": "One or more SEARCH/REPLACE blocks defining the changes to apply"
                }
            },
            "required": ["path", "diff"]
        }
        """;

    public ApplyDiffToolExecutor(ILogger<ApplyDiffToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "apply_diff";

    public string Description =>
        "Apply a precise, targeted modification to an existing file using " +
        "one or more SEARCH/REPLACE blocks. Each block finds exact existing " +
        "content (SEARCH) and replaces it with new content (REPLACE). " +
        "Requires user confirmation — not auto-approved.";

    public string ParametersJsonSchema => Schema;

    public bool RequiresUserConfirmation => true;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Medium;

    public bool CanAutoApprove => false;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct)
    {
        // Stub — real validation in Feature 17
        return Task.FromResult(new ToolValidationResult(true, null, ToolRiskLevel.Medium));
    }

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct)
    {
        _logger.LogDebug("apply_diff stub called with args: {Arguments}", toolCall.Arguments);
        // Stub — real execution in Feature 17
        return Task.FromResult(new ToolResult(true, "Not yet implemented — Feature 17", null));
    }

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
