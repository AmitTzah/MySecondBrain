using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class OpenAIWhisperProvider : ISTTProvider
{
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<OpenAIWhisperProvider> _logger;

    public OpenAIWhisperProvider(
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILogger<OpenAIWhisperProvider> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "OpenAI Whisper";

    public STTProviderType Type => STTProviderType.OpenAI;

    public Task<STTResult> TranscribeAsync(byte[] audioData, string audioFormat, CancellationToken ct) =>
        Task.FromResult<STTResult>(default!);

#pragma warning disable CS1998, CS8425
    public async IAsyncEnumerable<string> TranscribeStreamAsync(Stream audioStream, string audioFormat, CancellationToken ct)
#pragma warning restore CS1998, CS8425
    {
        yield break;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct) =>
        Task.FromResult(false);
}
