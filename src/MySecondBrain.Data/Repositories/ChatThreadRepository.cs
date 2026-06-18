using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class ChatThreadRepository : IChatThreadRepository
{
    private readonly AppDbContext _db;

    public ChatThreadRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChatThread?> GetByIdAsync(string id)
    {
        var entity = await _db.ChatThreads
            .Include(t => t.Persona)
            .Include(t => t.ModelConfig)
            .FirstOrDefaultAsync(t => t.Id == id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ChatThread>> GetAllPermanentAsync(ChatSortOrder sort)
    {
        var entities = await _db.ChatThreads
            .Where(t => !t.IsTransient && !t.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        // SQLite doesn't support DateTimeOffset in ORDER BY; sort client-side
        var sorted = sort switch
        {
            ChatSortOrder.CreatedAsc => entities.OrderBy(t => t.CreatedAt),
            ChatSortOrder.TitleAsc => entities.OrderBy(t => t.Title),
            _ => entities.OrderByDescending(t => t.LastActivityAt)
        };

        return sorted.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> GetTransientInWindowAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var entities = await _db.ChatThreads
            .Where(t => t.IsTransient)
            .AsNoTracking()
            .ToListAsync();

        return entities
            .Where(t => t.CreatedAt > cutoff)
            .OrderByDescending(t => t.CreatedAt)
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> GetTrashAsync()
    {
        var entities = await _db.ChatThreads
            .Where(t => t.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        return entities
            .OrderBy(t => t.DeletedAt)
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> SearchAsync(string query, int maxResults)
    {
        var entities = await _db.ChatThreads
            .Where(t => t.Title != null && t.Title.Contains(query))
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ChatThread> CreateAsync(ChatThread thread)
    {
        var entity = MapToEntity(thread);
        _db.ChatThreads.Add(entity);
        await _db.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(ChatThread thread)
    {
        var entity = await _db.ChatThreads.FindAsync(thread.Id);
        if (entity is null) return;

        entity.Title = thread.Title;
        entity.IsTransient = thread.IsTransient;
        entity.IsDeleted = thread.IsDeleted;
        entity.PersonaId = thread.PersonaId;
        entity.ModelConfigId = thread.ModelConfigId;
        entity.LastActivityAt = thread.LastActivityAt;

        _db.Entry(entity).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(string id)
    {
        var entity = await _db.ChatThreads.FindAsync(id);
        if (entity is null) return;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task PermanentDeleteAsync(string id)
    {
        var entity = await _db.ChatThreads
            .Include(t => t.Messages)
            .Include(t => t.Artifacts)
            .Include(t => t.MediaItems)
            .Include(t => t.UsageRecords)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return;

        _db.ChatThreads.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<int> CleanupTransientAsync(DateTimeOffset olderThan)
    {
        // Elevate threads with exception criteria (server-side navigation queries)
        var allExceptions = await _db.ChatThreads
            .Where(t => t.IsTransient)
            .Where(t => t.IsFavorite || t.Tags != null || t.IsPinned || t.IsArchived
                || t.Messages.Any(m => m.Role == "User" && m.ParentMessageId != null)
                || t.Artifacts.Any())
            .ToListAsync();

        // DateTimeOffset comparison client-side (SQLite limitation)
        var exceptions = allExceptions.Where(t => t.CreatedAt < olderThan).ToList();
        foreach (var thread in exceptions)
            thread.IsTransient = false;

        // Hard-delete: load non-exception transient threads, filter DateTimeOffset client-side
        var allToDelete = await _db.ChatThreads
            .Where(t => t.IsTransient)
            .Where(t => !t.IsFavorite && t.Tags == null && !t.IsPinned && !t.IsArchived)
            .ToListAsync();

        var toDelete = allToDelete.Where(t => t.CreatedAt < olderThan).ToList();

        if (toDelete.Count == 0)
        {
            await _db.SaveChangesAsync();
            return 0;
        }

        _db.ChatThreads.RemoveRange(toDelete);
        await _db.SaveChangesAsync();
        return toDelete.Count;
    }

    public async Task<int> PurgeTrashAsync(DateTimeOffset olderThan)
    {
        var allTrash = await _db.ChatThreads
            .Where(t => t.IsDeleted)
            .ToListAsync();

        var toDelete = allTrash
            .Where(t => t.DeletedAt.HasValue && t.DeletedAt.Value < olderThan)
            .ToList();

        if (toDelete.Count == 0)
            return 0;

        _db.ChatThreads.RemoveRange(toDelete);
        await _db.SaveChangesAsync();
        return toDelete.Count;
    }

    // -- Mapping helpers --

    private static ChatThread MapToDomain(Entities.ChatThread entity)
    {
        return new ChatThread
        {
            Id = entity.Id,
            Title = entity.Title,
            IsTransient = entity.IsTransient,
            IsDeleted = entity.IsDeleted,
            CreatedAt = entity.CreatedAt,
            LastActivityAt = entity.LastActivityAt,
            PersonaId = entity.PersonaId,
            ModelConfigId = entity.ModelConfigId,
        };
    }

    private static Entities.ChatThread MapToEntity(ChatThread model)
    {
        return new Entities.ChatThread
        {
            Id = model.Id,
            Title = model.Title,
            IsTransient = model.IsTransient,
            IsDeleted = model.IsDeleted,
            CreatedAt = model.CreatedAt,
            LastActivityAt = model.LastActivityAt,
            PersonaId = model.PersonaId,
            ModelConfigId = model.ModelConfigId,
        };
    }
}
