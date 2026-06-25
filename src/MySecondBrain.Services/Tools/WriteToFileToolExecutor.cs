using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class WriteToFileToolExecutor : IToolExecutor
{
    private readonly ILogger<WriteToFileToolExecutor> _logger;

    private const string Schema = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The path of the file to write (absolute or relative to workspace)"
                },
                "content": {
                    "type": "string",
                    "description": "The complete content to write to the file"
                },
                "overwrite": {
                    "type": "boolean",
                    "description": "If true, overwrite existing file. If false, append. (optional, default false)"
                }
            },
            "required": ["path", "content"]
        }
        """;

    public WriteToFileToolExecutor(ILogger<WriteToFileToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "write_to_file";

    public string Description =>
        "Create or overwrite a file with the specified content. " +
        "Used for creating new files or completely rewriting existing ones. " +
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
        _logger.LogDebug("write_to_file stub called with args: {Arguments}", toolCall.Arguments);
        // Stub — real execution in Feature 17
        return Task.FromResult(new ToolResult(true, "Not yet implemented — Feature 17", null));
    }

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
