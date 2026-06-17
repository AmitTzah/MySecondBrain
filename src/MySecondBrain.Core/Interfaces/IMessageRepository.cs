using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(string id);
    Task<IReadOnlyList<Message>> GetActiveBranchAsync(string threadId);
    Task<IReadOnlyList<Message>> GetBranchAsync(string branchId);
    Task<IReadOnlyList<Message>> GetAllBranchesForThreadAsync(string threadId);
    Task<IReadOnlyList<Message>> SearchAsync(string query, int maxResults);
    Task<Message> CreateAsync(Message message);
    Task UpdateAsync(Message message);
    Task SetActiveBranch(string messageId, string branchId);
    Task<int> GetBranchCountAsync(string threadId);
}
