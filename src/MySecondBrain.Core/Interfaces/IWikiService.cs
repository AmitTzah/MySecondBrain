using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IWikiService
{
    Task IndexAllAsync(CancellationToken ct);
    Task IndexFileAsync(string relativePath, CancellationToken ct);

    Task<IReadOnlyList<WikiSearchResult>> SearchAsync(string query, int maxResults);

    Task<IReadOnlyList<WikiFileTree>> GetFileTreeAsync();
    Task<WikiFileDetail?> GetFileDetailAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string relativePath);
    Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string relativePath, int maxResults);

    Task<WikiWritePreview> GenerateWikiContentAsync(string chatThreadId, string targetPath, CancellationToken ct);
    Task SaveToWikiAsync(string relativePath, string markdownContent, bool appendMode);

    Task<IReadOnlyList<WikiVersionSnapshot>> GetVersionHistoryAsync(string relativePath);
    Task RestoreVersionAsync(string relativePath, int versionNumber);

    Task RegenerateIndexMdAsync(CancellationToken ct);

    Task InitializeGitAsync();
    Task CommitChangesAsync(string message);
    Task PushAsync(CancellationToken ct);
}
