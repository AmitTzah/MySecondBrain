using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ILLMProvider
{
    string ProviderName { get; }
    ProviderType Type { get; }

    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct);

    IAsyncEnumerable<StreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken ct);

    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct);

    Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct);

    /// <summary>
    /// Validates an API key against the provider's API.
    /// The optional endpointUrl is used by OpenAI-compatible providers to target a custom server.
    /// Default implementation delegates to the 2-parameter overload.
    /// </summary>
    Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct, string? endpointUrl) =>
        ValidateKeyAsync(apiKey, ct);
}
