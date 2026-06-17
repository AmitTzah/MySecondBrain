using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ILLMProviderService
{
    IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        IReadOnlyList<ToolDefinition>? tools,
        CancellationToken ct);

    Task<ChatResponse> ChatAsync(
        ChatThread thread,
        string userMessage,
        Persona persona,
        ModelConfiguration modelConfig,
        CancellationToken ct);

    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ModelConfiguration config, CancellationToken ct);

    Task<bool> ValidateApiKeyAsync(ProviderType provider, string apiKey, string? endpointUrl, CancellationToken ct);

    int CountTokens(string text, string modelId, ProviderType provider);

    int CountMessageTokens(ChatMessage message, string modelId, ProviderType provider);

    ContextOverflowResult CheckContextOverflow(
        IReadOnlyList<ChatMessage> history,
        string newMessage,
        ModelConfiguration config);
}
