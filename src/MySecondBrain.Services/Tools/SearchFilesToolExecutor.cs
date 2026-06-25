using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class SearchFilesToolExecutor : IToolExecutor
{
    private readonly ILogger<SearchFilesToolExecutor> _logger;

    private const string Schema = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The directory to search in (absolute or relative to workspace)"
                },
                "regex": {
                    "type": "string",
                    "description": "The regular expression pattern to search for"
                },
                "file_pattern": {
                    "type": "string",
                    "description": "Optional glob pattern to filter files (e.g., '*.cs')"
                }
            },
            "required": ["path", "regex"]
        }
        """;

    public SearchFilesToolExecutor(ILogger<SearchFilesToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "search_files";

    public string Description =>
        "Search files in a directory for content matching a regex pattern. " +
        "Optionally filter by file glob pattern. " +
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
        _logger.LogDebug("search_files stub called with args: {Arguments}", toolCall.Arguments);
        // Stub — real execution in Feature 17
        return Task.FromResult(new ToolResult(true, "Not yet implemented — Feature 17", null));
    }

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
