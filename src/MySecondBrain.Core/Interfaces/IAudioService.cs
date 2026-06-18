using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IAudioService : IDisposable
{
    Task<byte[]> RecordAsync(TimeSpan duration, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> RecordStreamAsync(CancellationToken ct = default);
    bool IsMicrophoneAvailable { get; }

    Task PlayAsync(byte[] audioData, string format, CancellationToken ct = default);
    void StopPlayback();
    event EventHandler<PlaybackPositionEventArgs>? PlaybackPositionChanged;
}
