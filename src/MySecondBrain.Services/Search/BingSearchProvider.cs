using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Search;

public class BingSearchProvider : ISearchProvider
{
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<BingSearchProvider> _logger;

    public BingSearchProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<BingSearchProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "Bing Search";

    public SearchProviderType Type => SearchProviderType.Bing;

    public Task<SearchResults> SearchAsync(string query, int maxResults, CancellationToken ct) =>
        Task.FromResult<SearchResults>(default!);

    public Task<bool> IsAvailableAsync(CancellationToken ct) =>
        Task.FromResult(false);
}
