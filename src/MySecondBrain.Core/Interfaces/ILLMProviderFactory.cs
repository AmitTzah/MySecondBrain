using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ILLMProviderFactory
{
    ILLMProvider GetProvider(ProviderType type, string? endpointUrl = null);
    IReadOnlyList<ProviderType> SupportedProviders { get; }
}
