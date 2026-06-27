using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IChatThreadService
{
    /// <summary>
    /// Raised when a stream chunk is received during SendMessageAsync / RegenerateAsync / ContinueGenerationAsync.
    /// Subscribers (e.g., the ViewModel) can forward these to the MarkdownStreamRenderer for progressive rendering.
    /// </summary>
    event Action<StreamChunk>? OnStreamChunk;

    Task<ChatThread> CreateThreadAsync(string? title, bool isTransient, Persona persona);
    Task<ChatThread?> GetThreadAsync(string threadId);
    Task<IReadOnlyList<ChatThread>> GetPermanentThreadsAsync(ChatSortOrder sort);
    Task<IReadOnlyList<ChatThread>> GetTransientThreadsAsync();
    Task SoftDeleteThreadAsync(string threadId);
    Task RestoreThreadAsync(string threadId);
    Task PermanentDeleteThreadAsync(string threadId);

    Task ElevateToPermanentAsync(string threadId);

    Task<Message> SendMessageAsync(string threadId, string content, CancellationToken ct);
    Task<Message> EditMessageAsync(string messageId, string newContent, bool createBranch);
    Task DeleteMessageAsync(string messageId);
    Task<Message> RegenerateAsync(string messageId, CancellationToken ct);
    Task<Message> ContinueGenerationAsync(string threadId, CancellationToken ct);

    Task<IReadOnlyList<Message>> GetActiveBranchMessagesAsync(string threadId);
    Task SetActiveBranchAsync(string messageId, string branchId);
    Task<int> GetBranchCountAsync(string threadId);
    Task<ChatTree> GetChatTreeAsync(string threadId);

    Task<IReadOnlyList<SearchResult>> SearchMessagesAsync(string query, int maxResults);

    Task SaveDraftAsync(string threadId, string content, int cursorPosition);
    Task<MessageDraft?> GetDraftAsync(string threadId);
    Task DeleteDraftAsync(string threadId);
}
