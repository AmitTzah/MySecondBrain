using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IWikiGitService : IDisposable
{
    bool IsInitialized { get; }
    bool IsRemoteConfigured { get; }
    Task InitializeAsync(string repoPath, CancellationToken ct = default);
    Task CommitAsync(string message, CancellationToken ct = default);
    Task PushAsync(string? remoteName = null, string? branchName = null, CancellationToken ct = default);
    Task<IReadOnlyList<GitLogEntry>> GetLogAsync(int maxCount = 50, CancellationToken ct = default);
    void ConfigureRemote(string remoteUrl, string personalAccessToken);
    event EventHandler<GitCommitEventArgs>? CommitCompleted;
}
