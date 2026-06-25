using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class ReadFileToolExecutor : IToolExecutor
{
    private readonly ILogger<ReadFileToolExecutor> _logger;

    private const string Schema = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The path of the file to read (absolute or relative to workspace)"
                },
                "offset": {
                    "type": "integer",
                    "description": "1-based line offset to start reading from (optional)"
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of lines to return (optional, default 2000)"
                }
            },
            "required": ["path"]
        }
        """;

    public ReadFileToolExecutor(ILogger<ReadFileToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "read_file";

    public string Description =>
        "Read any file on the filesystem. Use offset and limit to read specific sections. " +
        "Auto-approved within workspace, artifacts, and wiki directories. " +
        "Out-of-workspace reads trigger an approval gate.";

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
        _logger.LogDebug("read_file stub called with args: {Arguments}", toolCall.Arguments);
        // Stub — real execution in Feature 17
        return Task.FromResult(new ToolResult(true, "Not yet implemented — Feature 17", null));
    }

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
