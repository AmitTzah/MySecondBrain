using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IWikiIndexRepository
{
    Task<IReadOnlyList<WikiFile>> GetAllAsync();
    Task<WikiFile?> GetByPathAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> SearchAsync(string query, int maxResults);
    Task<WikiFile> UpsertAsync(WikiFile file);
    Task DeleteAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string targetPath);
    Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string filePath, int maxResults);
    Task<IReadOnlyList<WikiFile>> GetOrphansAsync();
    Task<IReadOnlyList<WikiVersionSnapshot>> GetSnapshotsAsync(string filePath);
    Task CreateSnapshotAsync(WikiVersionSnapshot snapshot);
    Task PruneSnapshotsAsync(string filePath, int maxSnapshots, long maxTotalBytes);
    Task<WikiVersionSnapshot?> GetSnapshotAsync(string filePath, int versionNumber);
}
