using System.Windows;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IVideoPlayerService : IDisposable
{
    FrameworkElement CreatePlayer(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    bool IsPlaying { get; }
    event EventHandler<VideoErrorEventArgs>? PlaybackError;
}
