using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IChatSearchService
{
    Task<IReadOnlyList<ChatSearchResult>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSearchResult>> SearchTransientAsync(string query, int maxResults = 20, CancellationToken ct = default);
}
