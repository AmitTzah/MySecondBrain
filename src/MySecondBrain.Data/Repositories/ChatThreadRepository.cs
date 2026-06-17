using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class ChatThreadRepository : IChatThreadRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public ChatThreadRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ChatThread?> GetByIdAsync(string id) =>
        Task.FromResult<ChatThread?>(null);

    public Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort) =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    public Task<IReadOnlyList<ChatThread>> GetTransientInWindowAsync() =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    public Task<IReadOnlyList<ChatThread>> GetTrashAsync() =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    public Task<IReadOnlyList<ChatThread>> SearchAsync(string query, int maxResults) =>
        Task.FromResult<IReadOnlyList<ChatThread>>(Array.Empty<ChatThread>());

    public Task<ChatThread> CreateAsync(ChatThread thread) =>
        Task.FromResult<ChatThread>(default!);

    public Task UpdateAsync(ChatThread thread) =>
        Task.CompletedTask;

    public Task SoftDeleteAsync(string id) =>
        Task.CompletedTask;

    public Task PermanentDeleteAsync(string id) =>
        Task.CompletedTask;

    public Task<int> CleanupTransientAsync(DateTimeOffset olderThan) =>
        Task.FromResult(0);

    public Task<int> PurgeTrashAsync(DateTimeOffset olderThan) =>
        Task.FromResult(0);
}
