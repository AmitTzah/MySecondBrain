using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface ISTTProvider
{
    string ProviderName { get; }
    STTProviderType Type { get; }

    Task<STTResult> TranscribeAsync(byte[] audioData, string audioFormat, CancellationToken ct);

    IAsyncEnumerable<string> TranscribeStreamAsync(Stream audioStream, string audioFormat, CancellationToken ct);

    Task<bool> IsAvailableAsync(CancellationToken ct);
}
