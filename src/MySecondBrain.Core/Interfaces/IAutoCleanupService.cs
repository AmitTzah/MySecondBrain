using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IAutoCleanupService : IDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    Task<int> CleanupTransientAsync(CancellationToken ct = default);
    Task<int> PurgeTrashAsync(CancellationToken ct = default);
    event EventHandler<CleanupCompletedEventArgs>? CleanupCompleted;
}
