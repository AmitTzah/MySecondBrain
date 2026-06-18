using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class WikiIndexRepository : IWikiIndexRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public WikiIndexRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<WikiFile>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<WikiFile?> GetByPathAsync(string relativePath) =>
        Task.FromResult<WikiFile?>(null);

    public Task<IReadOnlyList<WikiFile>> SearchAsync(string query, int maxResults) =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<WikiFile> UpsertAsync(WikiFile file) =>
        Task.FromResult<WikiFile>(default!);

    public Task DeleteAsync(string relativePath) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string targetPath) =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string filePath, int maxResults) =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<IReadOnlyList<WikiFile>> GetOrphansAsync() =>
        Task.FromResult<IReadOnlyList<WikiFile>>(Array.Empty<WikiFile>());

    public Task<IReadOnlyList<WikiVersionSnapshot>> GetSnapshotsAsync(string filePath) =>
        Task.FromResult<IReadOnlyList<WikiVersionSnapshot>>(Array.Empty<WikiVersionSnapshot>());

    public Task CreateSnapshotAsync(WikiVersionSnapshot snapshot) =>
        Task.CompletedTask;

    public Task PruneSnapshotsAsync(string filePath, int maxSnapshots, long maxTotalBytes) =>
        Task.CompletedTask;

    public Task<WikiVersionSnapshot?> GetSnapshotAsync(string filePath, int versionNumber) =>
        Task.FromResult<WikiVersionSnapshot?>(null);
}
