using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Wiki;

public class FileSystemWatcherAdapter : IWikiFileWatcher
{
    private readonly ILogger<FileSystemWatcherAdapter> _logger;

    public FileSystemWatcherAdapter(ILogger<FileSystemWatcherAdapter> logger)
    {
        _logger = logger;
    }

    public string WatchedDirectory { get; private set; } = string.Empty;

    public bool IsRunning { get; private set; }

    public void Start(string directoryPath)
    {
        WatchedDirectory = directoryPath;
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
    }

#pragma warning disable CS0414
    public event EventHandler<WikiFileChangedEventArgs>? FileChanged;
#pragma warning restore CS0414

    public void Dispose()
    {
        FileChanged = null;
    }
}
