using System.Windows;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class WpfVideoPlayerService : IVideoPlayerService
{
#pragma warning disable CS0414, CS0067
    public event EventHandler<VideoErrorEventArgs>? PlaybackError;
#pragma warning restore CS0414, CS0067

    public FrameworkElement CreatePlayer(string filePath) => new();

    public void Play() { }

    public void Pause() { }

    public void Stop() { }

    public void Seek(TimeSpan position) { }

    public TimeSpan Position => TimeSpan.Zero;

    public TimeSpan Duration => TimeSpan.Zero;

    public bool IsPlaying => false;

    public void Dispose()
    {
        PlaybackError = null;
    }
}
