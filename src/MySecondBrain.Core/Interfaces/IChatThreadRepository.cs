using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IChatThreadRepository
{
    Task<ChatThread?> GetByIdAsync(string id);
    Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort);
    Task<IReadOnlyList<ChatThread>> GetTransientInWindowAsync();
    Task<IReadOnlyList<ChatThread>> GetTrashAsync();
    Task<IReadOnlyList<ChatThread>> SearchAsync(string query, int maxResults);
    Task<ChatThread> CreateAsync(ChatThread thread);
    Task UpdateAsync(ChatThread thread);
    Task SoftDeleteAsync(string id);
    Task PermanentDeleteAsync(string id);
    Task<int> CleanupTransientAsync(DateTimeOffset olderThan);
    Task<int> PurgeTrashAsync(DateTimeOffset olderThan);
}
