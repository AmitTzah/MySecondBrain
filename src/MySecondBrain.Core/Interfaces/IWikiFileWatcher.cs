using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IWikiFileWatcher : IDisposable
{
    string WatchedDirectory { get; }
    bool IsRunning { get; }
    void Start(string directoryPath);
    void Stop();
    event EventHandler<WikiFileChangedEventArgs>? FileChanged;
}
