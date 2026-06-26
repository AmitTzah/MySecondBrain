using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Orchestrator that composes focused sub-services and implements <see cref="IChatThreadService"/>
/// by delegating to the appropriate sub-service for each operation.
/// </summary>
public class ChatThreadService : IChatThreadService
{
    private readonly ChatThreadLifecycleService _lifecycle;
    private readonly ChatMessageService _messages;
    private readonly ChatBranchService _branches;
    private readonly ChatDraftService _drafts;

    public ChatThreadService(
        ChatThreadLifecycleService lifecycle,
        ChatMessageService messages,
        ChatBranchService branches,
        ChatDraftService drafts)
    {
        _lifecycle = lifecycle;
        _messages = messages;
        _branches = branches;
        _drafts = drafts;

        _messages.OnStreamChunk += chunk => OnStreamChunk?.Invoke(chunk);
    }

    /// <summary>
    /// Raised when a stream chunk is received during SendMessageAsync / RegenerateAsync / ContinueGenerationAsync.
    /// Forwarded from <see cref="ChatMessageService.OnStreamChunk"/>.
    /// </summary>
    public event Action<StreamChunk>? OnStreamChunk;

    // ═══════════════════════════════════════════════════════════════
    //  Thread Lifecycle
    // ═══════════════════════════════════════════════════════════════

    public Task<ChatThread> CreateThreadAsync(string? title, bool isTransient, Persona persona)
        => _lifecycle.CreateThreadAsync(title, isTransient, persona);

    public Task<ChatThread?> GetThreadAsync(string threadId)
        => _lifecycle.GetThreadAsync(threadId);

    public Task<IReadOnlyList<ChatThread>> GetPermanentThreadsAsync(ChatSortOrder sort)
        => _lifecycle.GetPermanentThreadsAsync(sort);

    public Task<IReadOnlyList<ChatThread>> GetTransientThreadsAsync()
        => _lifecycle.GetTransientThreadsAsync();

    public Task SoftDeleteThreadAsync(string threadId)
        => _lifecycle.SoftDeleteThreadAsync(threadId);

    public Task RestoreThreadAsync(string threadId)
        => _lifecycle.RestoreThreadAsync(threadId);

    public Task PermanentDeleteThreadAsync(string threadId)
        => _lifecycle.PermanentDeleteThreadAsync(threadId);

    public Task ElevateToPermanentAsync(string threadId)
        => _lifecycle.ElevateToPermanentAsync(threadId);

    // ═══════════════════════════════════════════════════════════════
    //  Message Operations
    // ═══════════════════════════════════════════════════════════════

    public Task<Message> SendMessageAsync(string threadId, string content, CancellationToken ct)
        => _messages.SendMessageAsync(threadId, content, ct);

    public Task<Message> EditMessageAsync(string messageId, string newContent, bool createBranch)
        => _messages.EditMessageAsync(messageId, newContent, createBranch);

    public Task DeleteMessageAsync(string messageId)
        => _messages.DeleteMessageAsync(messageId);

    public Task<Message> RegenerateAsync(string messageId, CancellationToken ct)
        => _messages.RegenerateAsync(messageId, ct);

    public Task<Message> ContinueGenerationAsync(string threadId, CancellationToken ct)
        => _messages.ContinueGenerationAsync(threadId, ct);

    // ═══════════════════════════════════════════════════════════════
    //  Branch & Search Operations
    // ═══════════════════════════════════════════════════════════════

    public Task<IReadOnlyList<Message>> GetActiveBranchMessagesAsync(string threadId)
        => _branches.GetActiveBranchMessagesAsync(threadId);

    public Task SetActiveBranchAsync(string messageId, string branchId)
        => _branches.SetActiveBranchAsync(messageId, branchId);

    public Task<int> GetBranchCountAsync(string threadId)
        => _branches.GetBranchCountAsync(threadId);

    public Task<ChatTree> GetChatTreeAsync(string threadId)
        => _branches.GetChatTreeAsync(threadId);

    public Task<IReadOnlyList<SearchResult>> SearchMessagesAsync(string query, int maxResults)
        => _branches.SearchMessagesAsync(query, maxResults);

    // ═══════════════════════════════════════════════════════════════
    //  Drafts
    // ═══════════════════════════════════════════════════════════════

    public Task SaveDraftAsync(string threadId, string content, int cursorPosition)
        => _drafts.SaveDraftAsync(threadId, content, cursorPosition);

    public Task<MessageDraft?> GetDraftAsync(string threadId)
        => _drafts.GetDraftAsync(threadId);

    public Task DeleteDraftAsync(string threadId)
        => _drafts.DeleteDraftAsync(threadId);
}
