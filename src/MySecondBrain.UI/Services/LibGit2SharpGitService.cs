using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.Services;

public class LibGit2SharpGitService : IWikiGitService
{
#pragma warning disable CS0414, CS0067
    public event EventHandler<GitCommitEventArgs>? CommitCompleted;
#pragma warning restore CS0414, CS0067

    public bool IsInitialized => false;

    public bool IsRemoteConfigured => false;

    public Task InitializeAsync(string repoPath, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task CommitAsync(string message, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task PushAsync(string? remoteName = null, string? branchName = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<GitLogEntry>> GetLogAsync(int maxCount = 50, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GitLogEntry>>(Array.Empty<GitLogEntry>());

    public void ConfigureRemote(string remoteUrl, string personalAccessToken) { }

    public void Dispose()
    {
        CommitCompleted = null;
    }
}
