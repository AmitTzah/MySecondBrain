using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

public class ChatThreadService : IChatThreadService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILLMProviderService _llmService;
    private readonly ILogger<ChatThreadService> _logger;

    public ChatThreadService(
        IChatThreadRepository threadRepo,
        IMessageRepository messageRepo,
        ILLMProviderService llmService,
        ILogger<ChatThreadService> logger)
    {
        _threadRepo = threadRepo;
        _messageRepo = messageRepo;
        _llmService = llmService;
        _logger = logger;
    }

    public Task<ChatThread> CreateThreadAsync(string? title, bool isTransient, Persona persona) =>
        Task.FromResult<ChatThread>(default!);

    public Task<ChatThread?> GetThreadAsync(string threadId) =>
        Task.FromResult<ChatThread?>(null);

    public Task<IReadOnlyList<ChatThread>> GetPermanentThreadsAsync(ChatSortOrder sort) =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    public Task<IReadOnlyList<ChatThread>> GetTransientThreadsAsync() =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    public Task SoftDeleteThreadAsync(string threadId) =>
        Task.CompletedTask;

    public Task RestoreThreadAsync(string threadId) =>
        Task.CompletedTask;

    public Task PermanentDeleteThreadAsync(string threadId) =>
        Task.CompletedTask;

    public Task ElevateToPermanentAsync(string threadId) =>
        Task.CompletedTask;

    public Task<Message> SendMessageAsync(string threadId, string content, CancellationToken ct) =>
        Task.FromResult<Message>(default!);

    public Task<Message> EditMessageAsync(string messageId, string newContent, bool createBranch) =>
        Task.FromResult<Message>(default!);

    public Task DeleteMessageAsync(string messageId) =>
        Task.CompletedTask;

    public Task<Message> RegenerateAsync(string messageId, CancellationToken ct) =>
        Task.FromResult<Message>(default!);

    public Task<Message> ContinueGenerationAsync(string threadId, CancellationToken ct) =>
        Task.FromResult<Message>(default!);

    public Task<IReadOnlyList<Message>> GetActiveBranchMessagesAsync(string threadId) =>
        Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

    public Task SetActiveBranchAsync(string messageId, string branchId) =>
        Task.CompletedTask;

    public Task<int> GetBranchCountAsync(string threadId) =>
        Task.FromResult(0);

    public Task<ChatTree> GetChatTreeAsync(string threadId) =>
        Task.FromResult<ChatTree>(default!);

    public Task<IReadOnlyList<SearchResult>> SearchMessagesAsync(string query, int maxResults) =>
        Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());

    public Task SaveDraftAsync(string threadId, string content, int cursorPosition) =>
        Task.CompletedTask;

    public Task<MessageDraft?> GetDraftAsync(string threadId) =>
        Task.FromResult<MessageDraft?>(null);

    public Task DeleteDraftAsync(string threadId) =>
        Task.CompletedTask;
}
