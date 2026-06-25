using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Tools;

public class PresentFilesToolExecutor : IToolExecutor
{
    private readonly ILogger<PresentFilesToolExecutor> _logger;

    private const string Schema = """
        {
            "type": "object",
            "properties": {
                "paths": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "List of file paths to present as artifacts"
                },
                "chat_id": {
                    "type": "string",
                    "description": "System-injected chat identifier for per-chat artifacts isolation"
                }
            },
            "required": ["paths"]
        }
        """;

    /// <summary>
    /// Base artifacts directory: %LOCALAPPDATA%\MySecondBrain\artifacts\
    /// Each chat gets a subdirectory: artifacts/{chat-id}/
    /// </summary>
    public static readonly string ArtifactsBasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain",
        "artifacts");

    public PresentFilesToolExecutor(ILogger<PresentFilesToolExecutor> logger)
    {
        _logger = logger;
    }

    public string ToolName => "present_files";

    public string Description =>
        "Copy files from the per-chat workspace to the per-chat artifacts directory " +
        "for display in the artifacts panel. " +
        "chat_id is system-injected for per-chat isolation.";

    public string ParametersJsonSchema => Schema;

    public bool RequiresUserConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    public bool CanAutoApprove => true;

    /// <summary>
    /// Extracts chat_id from the ToolCall arguments JSON.
    /// chat_id is system-injected by the caller, not provided by the LLM.
    /// Returns null if chat_id is missing or cannot be parsed.
    /// </summary>
    private static string? ExtractChatId(ToolCall toolCall)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolCall.Arguments);
            if (doc.RootElement.TryGetProperty("chat_id", out var chatIdProp))
                return chatIdProp.GetString();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Get the per-chat artifacts path.
    /// </summary>
    public static string GetChatArtifactsPath(string chatId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatId);
        return Path.Combine(ArtifactsBasePath, chatId);
    }

    public Task<ToolValidationResult> ValidateAsync(ToolCall toolCall, CancellationToken ct)
    {
        // Stub — real validation in Feature 17
        return Task.FromResult(new ToolValidationResult(true, null, ToolRiskLevel.Low));
    }

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct)
    {
        var chatId = ExtractChatId(toolCall);
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return Task.FromResult(new ToolResult(false, "", "chat_id is required for per-chat artifacts isolation"));
        }

        var artifactsPath = GetChatArtifactsPath(chatId);
        Directory.CreateDirectory(artifactsPath);

        _logger.LogDebug("present_files stub: would copy to artifacts {ArtifactsPath}", artifactsPath);
        return Task.FromResult(new ToolResult(true, "Not yet implemented — Feature 17", null));
    }

    public string GetConfirmationDescription(ToolCall toolCall) => string.Empty;
}
