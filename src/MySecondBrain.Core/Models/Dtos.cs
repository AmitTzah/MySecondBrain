using System.Windows.Input;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.Core.Models;

// === LLM Chat DTOs ===

public record StreamChunk(
    string? ContentDelta,
    IReadOnlyList<ToolCallDelta>? ToolCalls,
    string? ThinkingDelta,
    string? FinishReason,
    UsageInfo? Usage,
    bool IsFinal
);

public record ChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    ModelConfiguration ModelConfig,
    IReadOnlyList<ToolDefinition>? Tools,
    string? SystemMessage
);

public record ChatResponse(
    string Content,
    IReadOnlyList<ToolCall>? ToolCalls,
    string? ThinkingContent,
    string FinishReason,
    UsageInfo Usage
);

public record ChatMessage(string Role, string Content);

public record ToolDefinition(string Name, string Description, string ParametersJsonSchema);

public record ToolCallDelta(int Index, string? Id, string? Name, string? ArgumentsDelta);

public record ToolCall(string Id, string Name, string Arguments);

public record UsageInfo(int PromptTokens, int CompletionTokens, int TotalTokens);

public record ModelInfo(string Id, string DisplayName, int MaxContextTokens);

// === STT DTOs ===

public record STTResult(string Text, string? Language, double? Confidence);

// === Backup DTOs ===

public record BackupResult(string BackupId, DateTimeOffset Timestamp, long SizeBytes);

public record BackupInfo(string BackupId, DateTimeOffset Timestamp, long SizeBytes, string Name);

// === Search DTOs ===

public record SearchResults(
    string Query,
    IReadOnlyList<SearchResultItem> Items,
    long TotalEstimatedResults
);

public record SearchResultItem(
    string Title,
    string Url,
    string Snippet,
    string? DisplayUrl
);

// === Tool DTOs ===

public record ToolValidationResult(bool IsValid, string? ErrorMessage, ToolRiskLevel AssessedRisk);

public record ToolResult(bool Success, string Content, string? ErrorMessage);

// === Renderer DTOs ===

public record RenderContext(
    ChatThread Thread,
    Message Message,
    IThemeProvider Theme,
    bool IsStreaming,
    double AvailableWidth,
    string? ArtifactDirectory = null
);

// === Import DTOs ===

public record ImportResult(
    IReadOnlyList<ImportedChatThread> Threads,
    IReadOnlyList<ImportWarning> Warnings
);

public record ImportedChatThread(
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ImportedMessage> Messages,
    string? SourceSystemMessage
);

public record ImportedMessage(
    string Role,
    string Content,
    DateTimeOffset Timestamp,
    string? ModelName
);

public record ImportWarning(string Message, string? Detail);

public record ImportValidationResult(bool IsValid, string? ErrorMessage, int EstimatedMessageCount);

// === Update DTOs ===

public record UpdateCheckResult(
    bool UpdateAvailable,
    UpdateInfo? Update,
    string? ErrorMessage
);

public record UpdateInfo(
    Version Version,
    string ReleaseNotes,
    DateTimeOffset ReleaseDate,
    long DownloadSizeBytes,
    string DownloadUrl,
    bool IsMandatory
);

// === Platform Integration DTOs ===

public record HwndCaptureResult(
    IntPtr Hwnd,
    string? AppName,
    string? DocumentTitle,
    string? SelectedText
);

public record TextInjectionResult(
    bool Success,
    string MethodUsed,
    string? ErrorMessage
);

public record PlaybackPositionEventArgs(TimeSpan Position, TimeSpan Duration);

public record VideoErrorEventArgs(string FilePath, string ErrorMessage, bool FallbackAvailable);

public record WikiFileChangedEventArgs(
    string RelativePath,
    WikiFileChangeType ChangeType
);

public record HotkeyAssignment(string HotkeyId, ModifierKeys Modifiers, VirtualKey Key);

public record HotkeyTriggeredEventArgs(string HotkeyId);

public record ChatSearchResult(
    string MessageId,
    string ThreadId,
    string ThreadTitle,
    string Snippet,
    string Role,
    DateTimeOffset Timestamp
);

public record CleanupCompletedEventArgs(int TransientDeleted, int TrashPurged);

public record GitLogEntry(string Sha, string Message, DateTimeOffset Timestamp, string Author);

public record GitCommitEventArgs(int FilesChanged);
