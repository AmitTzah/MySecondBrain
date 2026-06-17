using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Search;

public class GoogleCustomSearchProvider : ISearchProvider
{
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<GoogleCustomSearchProvider> _logger;

    public GoogleCustomSearchProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<GoogleCustomSearchProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "Google Custom Search";

    public SearchProviderType Type => SearchProviderType.GoogleCustomSearch;

    public Task<SearchResults> SearchAsync(string query, int maxResults, CancellationToken ct) =>
        Task.FromResult<SearchResults>(default!);

    public Task<bool> IsAvailableAsync(CancellationToken ct) =>
        Task.FromResult(false);
}
