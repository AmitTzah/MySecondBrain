using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly IEnumerable<ILLMProvider> _providers;
    private readonly ILogger<LLMProviderFactory> _logger;

    public LLMProviderFactory(
        IEnumerable<ILLMProvider> providers,
        ILogger<LLMProviderFactory> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public ILLMProvider GetProvider(ProviderType type, string? endpointUrl = null) =>
        _providers.FirstOrDefault(p => p.Type == type)!;

    public IReadOnlyList<ProviderType> SupportedProviders =>
        _providers.Select(p => p.Type).ToList().AsReadOnly();
}
