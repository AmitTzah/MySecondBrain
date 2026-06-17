using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class AnthropicProvider : ILLMProvider
{
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<AnthropicProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "Anthropic";

    public ProviderType Type => ProviderType.Anthropic;

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct) =>
        Task.FromResult<ChatResponse>(default!);

#pragma warning disable CS1998, CS8425
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken ct)
#pragma warning restore CS1998, CS8425
    {
        yield break;
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModelInfo>>(Array.Empty<ModelInfo>());

    public Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct) =>
        Task.FromResult(false);
}
