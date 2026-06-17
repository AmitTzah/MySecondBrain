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
}
