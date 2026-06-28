using System.Runtime.CompilerServices;
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

    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        IReadOnlyList<ToolDefinition>? tools,
        IReadOnlyList<ChatMessage>? history,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var provider = _providerFactory.GetProvider(modelConfig.ProviderType, modelConfig.EndpointUrl);
        if (provider is null)
        {
            _logger.LogWarning("No provider found for type {ProviderType}", modelConfig.ProviderType);
            yield break;
        }

        _logger.LogInformation(
            "ChatStreamAsync: provider={ProviderName}, type={ProviderType}, endpoint={Endpoint}, model={Model}",
            provider.ProviderName, modelConfig.ProviderType, modelConfig.EndpointUrl ?? "(default)",
            modelConfig.ModelIdentifier);

        var messages = BuildMessages(persona, userMessage, history);
        var request = new ChatRequest(messages, modelConfig, tools, persona.SystemPrompt);

        _logger.LogInformation(
            "ChatStreamAsync: built request for {Provider}, messages={MsgCount}, sysPrompt={SysLen}chars, history={HistCount}",
            provider.ProviderName, messages.Count, persona.SystemPrompt?.Length ?? 0, history?.Count ?? 0);

        await foreach (var chunk in provider.ChatStreamAsync(request, ct))
            yield return chunk;
    }

    public async Task<ChatResponse> ChatAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        IReadOnlyList<ChatMessage>? history,
        CancellationToken ct)
    {
        var provider = _providerFactory.GetProvider(modelConfig.ProviderType, modelConfig.EndpointUrl);
        if (provider is null)
        {
            _logger.LogWarning("No provider found for type {ProviderType}", modelConfig.ProviderType);
            return new ChatResponse(string.Empty, null, null, "error", new UsageInfo(0, 0, 0));
        }

        var messages = BuildMessages(persona, userMessage, history);
        var request = new ChatRequest(messages, modelConfig, null, persona.SystemPrompt);

        return await provider.ChatAsync(request, ct);
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ModelConfiguration config, CancellationToken ct)
    {
        _logger.LogDebug("[ListModels] LLMProviderService: resolving provider for type={ProviderType}, endpoint={Endpoint}",
            config.ProviderType, config.EndpointUrl ?? "(null)");

        var provider = _providerFactory.GetProvider(config.ProviderType, config.EndpointUrl);
        if (provider == null)
        {
            _logger.LogWarning("[ListModels] LLMProviderService: no provider found for type {ProviderType}",
                config.ProviderType);
            return Array.Empty<ModelInfo>();
        }

        _logger.LogInformation("[ListModels] LLMProviderService: resolved provider={ProviderName} (type={ProviderType}), fetching models",
            provider.ProviderName, provider.Type);

        var models = await provider.ListModelsAsync(ct);
        _logger.LogInformation("[ListModels] LLMProviderService: got {Count} models from {Provider}",
            models.Count, provider.ProviderName);
        return models;
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

    /// <summary>
    /// Builds the full chat message list: system prompt (if any), conversation history,
    /// and the user's current message. History messages are interleaved in their original
    /// user/assistant order between the system prompt and the current message.
    /// </summary>
    private static IReadOnlyList<ChatMessage> BuildMessages(
        Persona persona,
        string userMessage,
        IReadOnlyList<ChatMessage>? history)
    {
        var capacity = (string.IsNullOrEmpty(persona.SystemPrompt) ? 0 : 1) + (history?.Count ?? 0) + 1;
        var msgs = new List<ChatMessage>(capacity);

        if (!string.IsNullOrEmpty(persona.SystemPrompt))
            msgs.Add(new ChatMessage("system", persona.SystemPrompt));

        if (history is not null)
            msgs.AddRange(history);

        msgs.Add(new ChatMessage("user", userMessage));

        return msgs;
    }
}
