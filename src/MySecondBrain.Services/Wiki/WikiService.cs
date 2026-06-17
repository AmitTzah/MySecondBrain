using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Wiki;

public class WikiService : IWikiService
{
    private readonly IWikiIndexRepository _wikiRepo;
    private readonly IWikiFileWatcher _fileWatcher;
    private readonly IWikiGitService _gitService;
    private readonly ILogger<WikiService> _logger;

    public WikiService(
        IWikiIndexRepository wikiRepo,
        IWikiFileWatcher fileWatcher,
        IWikiGitService gitService,
        ILogger<WikiService> logger)
    {
        _wikiRepo = wikiRepo;
        _fileWatcher = fileWatcher;
        _gitService = gitService;
        _logger = logger;
    }

    public Task IndexAllAsync(CancellationToken ct) =>
        Task.CompletedTask;

    public Task IndexFileAsync(string relativePath, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<WikiSearchResult>> SearchAsync(string query, int maxResults) =>
        Task.FromResult<IReadOnlyList<WikiSearchResult>>(Array.Empty<WikiSearchResult>());

    public Task<IReadOnlyList<WikiFileTree>> GetFileTreeAsync() =>
        Task.FromResult<IReadOnlyList<WikiFileTree>>(Array.Empty<WikiFileTree>());

    public Task<WikiFileDetail?> GetFileDetailAsync(string relativePath) =>
        Task.FromResult<WikiFileDetail?>(null);

    public Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string relativePath) =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string relativePath, int maxResults) =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<WikiWritePreview> GenerateWikiContentAsync(string chatThreadId, string targetPath, CancellationToken ct) =>
        Task.FromResult<WikiWritePreview>(default!);

    public Task SaveToWikiAsync(string relativePath, string markdownContent, bool appendMode) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<WikiVersionSnapshot>> GetVersionHistoryAsync(string relativePath) =>
        Task.FromResult<IReadOnlyList<WikiVersionSnapshot>>(Array.Empty<WikiVersionSnapshot>());

    public Task RestoreVersionAsync(string relativePath, int versionNumber) =>
        Task.CompletedTask;

    public Task RegenerateIndexMdAsync(CancellationToken ct) =>
        Task.CompletedTask;

    public Task InitializeGitAsync() =>
        Task.CompletedTask;

    public Task CommitChangesAsync(string message) =>
        Task.CompletedTask;

    public Task PushAsync(CancellationToken ct) =>
        Task.CompletedTask;
}
