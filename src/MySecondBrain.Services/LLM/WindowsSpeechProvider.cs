using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.LLM;

public class WindowsSpeechProvider : ISTTProvider
{
    private readonly ILogger<WindowsSpeechProvider> _logger;

    public WindowsSpeechProvider(ILogger<WindowsSpeechProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "Windows Speech";

    public STTProviderType Type => STTProviderType.WindowsSpeech;

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
