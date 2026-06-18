using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class WikiIndexRepository : IWikiIndexRepository
{
    private readonly AppDbContext _db;

    public WikiIndexRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WikiFile>> GetAllAsync()
    {
        var entities = await _db.WikiFiles
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<WikiFile?> GetByPathAsync(string relativePath)
    {
        var entity = await _db.WikiFiles.FindAsync(relativePath);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<WikiFile>> SearchAsync(string query, int maxResults)
    {
        var entities = await _db.WikiFiles
            .FromSqlRaw(@"
                SELECT w.* FROM WikiFiles w
                INNER JOIN WikiFileFts fts ON w.rowid = fts.rowid
                WHERE WikiFileFts MATCH {0}
                ORDER BY rank
                LIMIT {1}
            ", query, maxResults)
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<WikiFile> UpsertAsync(WikiFile file)
    {
        var existing = await _db.WikiFiles.FindAsync(file.RelativePath);
        if (existing is null)
        {
            var entity = MapToEntity(file);
            _db.WikiFiles.Add(entity);
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(MapToEntity(file));
        }

        await _db.SaveChangesAsync();
        return file;
    }

    public async Task DeleteAsync(string relativePath)
    {
        var entity = await _db.WikiFiles.FindAsync(relativePath);
        if (entity is not null)
        {
            _db.WikiFiles.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<WikiFile>> GetBacklinksAsync(string targetPath)
    {
        // Files that have targetPath in their CrossLinksOut JSON array
        var allFiles = await _db.WikiFiles
            .AsNoTracking()
            .Where(f => f.CrossLinksOut != null)
            .ToListAsync();

        return allFiles
            .Where(f => CrossLinksContains(f.CrossLinksOut!, targetPath))
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<IReadOnlyList<WikiFile>> GetRelatedSectionsAsync(string filePath, int maxResults)
    {
        // Find files with shared cross-links — files linked by the given file
        // and files that link to the same targets
        var file = await _db.WikiFiles.FindAsync(filePath);
        if (file?.CrossLinksOut is null)
            return Array.Empty<WikiFile>();

        var linkedPaths = DeserializeCrossLinks(file.CrossLinksOut);
        if (linkedPaths.Count == 0)
            return Array.Empty<WikiFile>();

        // Find other files that share at least one cross-link target
        var allFiles = await _db.WikiFiles
            .AsNoTracking()
            .Where(f => f.FilePath != filePath && f.CrossLinksOut != null)
            .ToListAsync();

        return allFiles
            .Where(f => CrossLinksShareAny(f.CrossLinksOut!, linkedPaths))
            .Take(maxResults)
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<IReadOnlyList<WikiFile>> GetOrphansAsync()
    {
        // Files with no incoming cross-links (CrossLinksIn is empty or null)
        var allFiles = await _db.WikiFiles
            .AsNoTracking()
            .ToListAsync();

        return allFiles
            .Where(f => string.IsNullOrEmpty(f.CrossLinksIn) || f.CrossLinksIn == "[]")
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<IReadOnlyList<WikiVersionSnapshot>> GetSnapshotsAsync(string filePath)
    {
        var entities = await _db.WikiVersionSnapshots
            .Where(s => s.WikiFilePath == filePath)
            .AsNoTracking()
            .ToListAsync();

        // SQLite doesn't support DateTimeOffset in ORDER BY; sort client-side
        entities.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));

        return entities.Select((e, i) => MapSnapshotToDomain(e, i + 1)).ToList();
    }

    public async Task CreateSnapshotAsync(WikiVersionSnapshot snapshot)
    {
        var entity = MapSnapshotToEntity(snapshot);
        _db.WikiVersionSnapshots.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task PruneSnapshotsAsync(string filePath, int maxSnapshots, long maxTotalBytes)
    {
        var snapshots = await _db.WikiVersionSnapshots
            .Where(s => s.WikiFilePath == filePath)
            .ToListAsync();

        // SQLite doesn't support DateTimeOffset in ORDER BY; sort client-side
        snapshots.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

        // Per-file limit: keep newest maxSnapshots
        if (snapshots.Count > maxSnapshots)
        {
            var toRemove = snapshots.Skip(maxSnapshots);
            _db.WikiVersionSnapshots.RemoveRange(toRemove);
            await _db.SaveChangesAsync();
        }

        // Global storage cap: remove oldest across all files until under limit
        while (true)
        {
            var allSnapshots = await _db.WikiVersionSnapshots
                .AsNoTracking()
                .ToListAsync();

            var totalBytes = allSnapshots.Sum(s => (long)(s.Content?.Length ?? 0));
            if (totalBytes <= maxTotalBytes)
                break;

            var oldest = allSnapshots
                .OrderBy(s => s.CreatedAt)
                .First();

            var tracked = await _db.WikiVersionSnapshots.FindAsync(oldest.Id);
            if (tracked is not null)
            {
                _db.WikiVersionSnapshots.Remove(tracked);
                await _db.SaveChangesAsync();
            }
            else
            {
                break; // Concurrent modification — stop pruning
            }
        }
    }

    public async Task<WikiVersionSnapshot?> GetSnapshotAsync(string filePath, int versionNumber)
    {
        var entities = await _db.WikiVersionSnapshots
            .Where(s => s.WikiFilePath == filePath)
            .AsNoTracking()
            .ToListAsync();

        // SQLite doesn't support DateTimeOffset in ORDER BY; sort client-side
        entities.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));

        if (versionNumber < 1 || versionNumber > entities.Count)
            return null;

        return MapSnapshotToDomain(entities[versionNumber - 1], versionNumber);
    }

    // -- Mapping helpers --

    private static WikiFile MapToDomain(Entities.WikiFile entity)
    {
        return new WikiFile
        {
            Id = entity.FilePath, // Use FilePath as stable identity
            RelativePath = entity.FilePath,
            Title = entity.H1Title,
            LastModified = entity.LastModifiedAt ?? DateTimeOffset.UtcNow,
            Backlinks = DeserializeCrossLinks(entity.CrossLinksIn),
        };
    }

    private static Entities.WikiFile MapToEntity(WikiFile model)
    {
        return new Entities.WikiFile
        {
            FilePath = model.RelativePath,
            FileName = Path.GetFileName(model.RelativePath),
            H1Title = model.Title,
            LastModifiedAt = model.LastModified,
            CrossLinksIn = SerializeCrossLinks(model.Backlinks),
        };
    }

    private static WikiVersionSnapshot MapSnapshotToDomain(Entities.WikiVersionSnapshot entity, int versionNumber)
    {
        return new WikiVersionSnapshot
        {
            Id = entity.Id,
            FilePath = entity.WikiFilePath,
            VersionNumber = versionNumber,
            Content = entity.Content,
            CreatedAt = entity.CreatedAt,
        };
    }

    private static Entities.WikiVersionSnapshot MapSnapshotToEntity(WikiVersionSnapshot model)
    {
        return new Entities.WikiVersionSnapshot
        {
            Id = model.Id,
            WikiFilePath = model.FilePath,
            Content = model.Content,
            Source = "ManualEdit", // Default source
            CreatedAt = model.CreatedAt,
        };
    }

    // -- CrossLinks helpers --

    private static bool CrossLinksContains(string crossLinksJson, string targetPath)
    {
        var links = DeserializeCrossLinks(crossLinksJson);
        return links.Contains(targetPath, StringComparer.OrdinalIgnoreCase);
    }

    private static bool CrossLinksShareAny(string crossLinksJson, ICollection<string> paths)
    {
        var links = DeserializeCrossLinks(crossLinksJson);
        return links.Any(l => paths.Contains(l, StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> DeserializeCrossLinks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string SerializeCrossLinks(ICollection<string> links)
    {
        return JsonSerializer.Serialize(links);
    }
}
