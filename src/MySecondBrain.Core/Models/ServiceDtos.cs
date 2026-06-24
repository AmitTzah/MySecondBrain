namespace MySecondBrain.Core.Models;

/// <summary>
/// DTOs used by service-layer interfaces that are not covered by provider-specific DTOs.
/// </summary>

public record ChatTree(
    string ThreadId,
    IReadOnlyList<ChatTreeNode> Nodes
);

public record ChatTreeNode(
    string MessageId,
    string? ParentMessageId,
    string BranchId,
    bool IsActiveBranch,
    string Role,
    string ContentPreview
);

public record SearchResult(
    string MessageId,
    string ThreadId,
    string ThreadTitle,
    string Snippet,
    string Role,
    DateTimeOffset Timestamp
);

public record MessageDraft(
    string ThreadId,
    string Content,
    int CursorPosition
);

public record WikiSearchResult(
    string FilePath,
    string Title,
    string Snippet,
    double RelevanceScore
);

public record WikiFileTree(
    string RelativePath,
    string? Title,
    bool IsDirectory,
    IReadOnlyList<WikiFileTree>? Children
);

public record WikiFileDetail(
    string RelativePath,
    string? Title,
    string Content,
    DateTimeOffset LastModified,
    IReadOnlyList<string> Backlinks
);

public record WikiWritePreview(
    string SuggestedContent,
    string? Reasoning
);

public record UsageSummary(
    int TotalRequests,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    long TotalTokens,
    decimal EstimatedCost
);

public record UsageByProvider(
    ProviderType ProviderType,
    int RequestCount,
    long TotalTokens,
    decimal EstimatedCost
);

public record UsageByModel(
    string ModelId,
    int RequestCount,
    long TotalTokens,
    decimal EstimatedCost
);

public record UsageByChat(
    string ChatThreadId,
    string ChatTitle,
    int RequestCount,
    long TotalTokens
);

public record FeedbackSummary(
    int PositiveCount,
    int NegativeCount,
    double AverageRating
);

public record ContextOverflowResult(
    bool WouldOverflow,
    int CurrentTokens,
    int MaxTokens,
    int NewMessageTokens,
    ContextOverflowStrategy RecommendedStrategy
);

/// <summary>
/// A single image result from an image search query.
/// </summary>
public record ImageSearchResultItem(
    string Title,
    string ThumbnailUrl,
    string SourceUrl,
    int Width,
    int Height
);

/// <summary>
/// Results returned by an image search query.
/// </summary>
public record ImageSearchResults(
    string Query,
    IReadOnlyList<ImageSearchResultItem> Items,
    int TotalEstimatedResults
);
