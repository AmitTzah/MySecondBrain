using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class LocalWhisperProvider : ISTTProvider
{
    private readonly ILogger<LocalWhisperProvider> _logger;

    public LocalWhisperProvider(ILogger<LocalWhisperProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "Local Whisper";

    public STTProviderType Type => STTProviderType.LocalWhisper;

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
