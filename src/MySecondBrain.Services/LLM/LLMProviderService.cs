using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class LLMProviderService : ILLMProviderService
{
    private readonly ILLMProviderFactory _providerFactory;
    private readonly ITokenizerFactory _tokenizerFactory;
    private readonly ILogger<LLMProviderService> _logger;

    public LLMProviderService(
        ILLMProviderFactory providerFactory,
        ITokenizerFactory tokenizerFactory,
        ILogger<LLMProviderService> logger)
    {
        _providerFactory = providerFactory;
        _tokenizerFactory = tokenizerFactory;
        _logger = logger;
    }

#pragma warning disable CS1998, CS8425
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        IReadOnlyList<ToolDefinition>? tools,
        CancellationToken ct)
#pragma warning restore CS1998, CS8425
    {
        yield break;
    }

    public Task<ChatResponse> ChatAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        CancellationToken ct) =>
        Task.FromResult<ChatResponse>(default!);

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ModelConfiguration config, CancellationToken ct)
    {
        var provider = _providerFactory.GetProvider(config.ProviderType, config.EndpointUrl);
        if (provider == null)
        {
            _logger.LogWarning("No provider found for type {ProviderType}", config.ProviderType);
            return Array.Empty<ModelInfo>();
        }

        _logger.LogDebug("Listing models for {Provider} via LLMProviderService", provider.ProviderName);
        return await provider.ListModelsAsync(ct);
    }

    public async Task<bool> ValidateApiKeyAsync(
        ProviderType providerType,
        string apiKey,
        string? endpointUrl,
        CancellationToken ct)
    {
        var provider = _providerFactory.GetProvider(providerType, endpointUrl);
        if (provider == null)
        {
            _logger.LogWarning("No provider found for type {ProviderType}", providerType);
            return false;
        }

        _logger.LogDebug("Validating API key for {Provider} via LLMProviderService", provider.ProviderName);
        return await provider.ValidateKeyAsync(apiKey, ct, endpointUrl);
    }

    public int CountTokens(string text, string modelId, ProviderType provider) => 0;

    public int CountMessageTokens(ChatMessage message, string modelId, ProviderType provider) => 0;

    public ContextOverflowResult CheckContextOverflow(
        IReadOnlyList<ChatMessage> history,
        string newMessage,
        ModelConfiguration config) =>
        new(false, 0, config.MaxOutputTokens, 0, ContextOverflowStrategy.HardStop);
}
