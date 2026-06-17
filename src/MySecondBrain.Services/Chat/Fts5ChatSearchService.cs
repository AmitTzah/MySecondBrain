using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

public class Fts5ChatSearchService : IChatSearchService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<Fts5ChatSearchService> _logger;

    public Fts5ChatSearchService(
        IChatThreadRepository threadRepo,
        IMessageRepository messageRepo,
        ILogger<Fts5ChatSearchService> logger)
    {
        _threadRepo = threadRepo;
        _messageRepo = messageRepo;
        _logger = logger;
    }

    public Task<IReadOnlyList<ChatSearchResult>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChatSearchResult>>(Array.Empty<ChatSearchResult>());

    public Task<IReadOnlyList<ChatSearchResult>> SearchTransientAsync(string query, int maxResults = 20, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChatSearchResult>>(Array.Empty<ChatSearchResult>());
}
