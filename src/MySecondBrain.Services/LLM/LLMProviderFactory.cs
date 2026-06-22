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

    public ILLMProvider GetProvider(ProviderType type, string? endpointUrl = null)
    {
        // DeepSeek, MiMo, Moonshot, and Mistral all use OpenAI-compatible APIs.
        // Remap them to the OpenAICompatible provider for resolution.
        var lookupType = type switch
        {
            ProviderType.DeepSeek or ProviderType.MiMo or ProviderType.Moonshot or ProviderType.Mistral
                => ProviderType.OpenAICompatible,
            _ => type
        };
        return _providers.FirstOrDefault(p => p.Type == lookupType)!;
    }

    public IReadOnlyList<ProviderType> SupportedProviders =>
        new[] { ProviderType.OpenAI, ProviderType.Anthropic, ProviderType.Google,
                ProviderType.DeepSeek, ProviderType.MiMo, ProviderType.Moonshot,
                ProviderType.Mistral, ProviderType.OpenAICompatible };
}
