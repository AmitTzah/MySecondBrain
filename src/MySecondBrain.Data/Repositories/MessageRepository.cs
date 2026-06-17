using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public MessageRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Message?> GetByIdAsync(string id) =>
        Task.FromResult<Message?>(null);

    public Task<IReadOnlyList<Message>> GetActiveBranchAsync(string threadId) =>
        Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

    public Task<IReadOnlyList<Message>> GetBranchAsync(string branchId) =>
        Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

    public Task<IReadOnlyList<Message>> GetAllBranchesForThreadAsync(string threadId) =>
        Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

    public Task<IReadOnlyList<Message>> SearchAsync(string query, int maxResults) =>
        Task.FromResult<IReadOnlyList<Message>>(Array.Empty<Message>());

    public Task<Message> CreateAsync(Message message) =>
        Task.FromResult<Message>(default!);

    public Task UpdateAsync(Message message) =>
        Task.CompletedTask;

    public Task SetActiveBranch(string messageId, string branchId) =>
        Task.CompletedTask;

    public Task<int> GetBranchCountAsync(string threadId) =>
        Task.FromResult(0);
}
