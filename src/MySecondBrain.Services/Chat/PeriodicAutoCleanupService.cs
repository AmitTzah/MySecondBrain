using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

public class PeriodicAutoCleanupService : IAutoCleanupService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly ILogger<PeriodicAutoCleanupService> _logger;

    public PeriodicAutoCleanupService(
        IChatThreadRepository threadRepo,
        ILogger<PeriodicAutoCleanupService> logger)
    {
        _threadRepo = threadRepo;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    public Task<int> CleanupTransientAsync(CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task<int> PurgeTrashAsync(CancellationToken ct = default) =>
        Task.FromResult(0);

#pragma warning disable CS0414
    public event EventHandler<CleanupCompletedEventArgs>? CleanupCompleted;
#pragma warning restore CS0414

    public void Dispose()
    {
        CleanupCompleted = null;
    }
}
