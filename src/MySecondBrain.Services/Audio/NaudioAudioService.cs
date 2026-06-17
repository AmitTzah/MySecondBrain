using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Audio;

public class NaudioAudioService : IAudioService
{
    private readonly ILogger<NaudioAudioService> _logger;

    public NaudioAudioService(ILogger<NaudioAudioService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> RecordAsync(TimeSpan duration, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

#pragma warning disable CS1998, CS8425
    public async IAsyncEnumerable<byte[]> RecordStreamAsync(CancellationToken ct = default)
#pragma warning restore CS1998, CS8425
    {
        yield break;
    }

    public bool IsMicrophoneAvailable => false;

    public Task PlayAsync(byte[] audioData, string format, CancellationToken ct = default) =>
        Task.CompletedTask;

    public void StopPlayback() { }

#pragma warning disable CS0414
    public event EventHandler<PlaybackPositionEventArgs>? PlaybackPositionChanged;
#pragma warning restore CS0414

    public void Dispose()
    {
        PlaybackPositionChanged = null;
    }
}
