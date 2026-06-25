using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class ListFilesToolExecutor : IToolExecutor
{
    private readonly ILogger<ListFilesToolExecutor> _logger;

    private const string Schema = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The directory path to list (absolute or relative to workspace)"
                },
                "recursive": {
                    "type": "boolean",
                    "description": "Whether to list files recursively (optional, default false)"
                }
            },
            "required": ["path"]
        }
        """;

    public ListFilesToolExecutor(ILogger<ListFilesToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "list_files";

    public string Description =>
        "List files and directories within the specified directory. " +
        "Set recursive to true to list all files and directories recursively. " +
        "Auto-approved within workspace, artifacts, and wiki directories.";

    public string ParametersJsonSchema => Schema;

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct)
    {
        // Stub — real validation in Feature 17
        return Task.FromResult(new ToolValidationResult(true, null, ToolRiskLevel.Low));
    }

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct)
    {
        _logger.LogDebug("list_files stub called with args: {Arguments}", toolCall.Arguments);
        // Stub — real execution in Feature 17
        return Task.FromResult(new ToolResult(true, "Not yet implemented — Feature 17", null));
    }

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
