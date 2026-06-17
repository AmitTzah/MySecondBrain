using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ISearchProvider
{
    string ProviderName { get; }
    SearchProviderType Type { get; }

    Task<SearchResults> SearchAsync(string query, int maxResults, CancellationToken ct);

    Task<bool> IsAvailableAsync(CancellationToken ct);
}
