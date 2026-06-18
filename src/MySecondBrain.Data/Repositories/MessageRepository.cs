using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _db;

    public MessageRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Message?> GetByIdAsync(string id)
    {
        var entity = await _db.Messages.FindAsync(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<Message>> GetActiveBranchAsync(string threadId)
    {
        // Recursive CTE following the active branch chain from root to leaf
        var entities = await _db.Messages
            .FromSqlRaw(@"
                WITH RECURSIVE active_chain AS (
                    SELECT * FROM Messages
                    WHERE ThreadId = {0} AND ParentMessageId IS NULL AND IsActiveBranch = 1
                    UNION ALL
                    SELECT m.* FROM Messages m
                    INNER JOIN active_chain a ON m.ParentMessageId = a.Id
                    WHERE m.IsActiveBranch = 1
                )
                SELECT * FROM active_chain ORDER BY CreatedAt
            ", threadId)
            .AsNoTracking()
            .ToListAsync();

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Message>> GetBranchAsync(string branchId)
    {
        var entities = await _db.Messages
            .Where(m => m.BranchId == branchId)
            .OrderBy(m => m.VersionNumber)
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Message>> GetAllBranchesForThreadAsync(string threadId)
    {
        var entities = await _db.Messages
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.BranchId)
            .ThenBy(m => m.VersionNumber)
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Message>> SearchAsync(string query, int maxResults)
    {
        var entities = await _db.Messages
            .FromSqlRaw(@"
                SELECT m.* FROM Messages m
                INNER JOIN MessageFts fts ON m.rowid = fts.rowid
                WHERE MessageFts MATCH {0}
                ORDER BY rank
                LIMIT {1}
            ", query, maxResults)
            .AsNoTracking()
            .ToListAsync();

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Message> CreateAsync(Message message)
    {
        var entity = MapToEntity(message);
        _db.Messages.Add(entity);
        await _db.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(Message message)
    {
        var entity = await _db.Messages.FindAsync(message.Id);
        if (entity is null) return;

        entity.Content = message.Content;
        entity.Role = message.Role;
        entity.BranchId = message.BranchId ?? entity.BranchId;
        entity.ParentMessageId = message.ParentMessageId;
        entity.IsActiveBranch = message.IsActiveBranch;

        _db.Entry(entity).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task SetActiveBranch(string messageId, string branchId)
    {
        var message = await _db.Messages.FindAsync(messageId);
        if (message is null) return;

        // Deactivate current active branch from this message forward
        var activeChain = await _db.Messages
            .FromSqlRaw(@"
                WITH RECURSIVE active_chain AS (
                    SELECT * FROM Messages
                    WHERE ThreadId = {0} AND ParentMessageId IS NULL AND IsActiveBranch = 1
                    UNION ALL
                    SELECT m.* FROM Messages m
                    INNER JOIN active_chain a ON m.ParentMessageId = a.Id
                    WHERE m.IsActiveBranch = 1
                )
                SELECT * FROM active_chain ORDER BY CreatedAt
            ", message.ThreadId)
            .ToListAsync();

        var fromIndex = activeChain.FindIndex(m => m.Id == messageId);
        // Deactivate messages AFTER the branch point, preserving the common ancestor
        for (int i = fromIndex + 1; i < activeChain.Count; i++)
            activeChain[i].IsActiveBranch = false;

        // Activate messages in the target branch that are not already active
        var newBranchMessages = await _db.Messages
            .Where(m => m.BranchId == branchId && !m.IsActiveBranch)
            .ToListAsync();
        foreach (var m in newBranchMessages)
            m.IsActiveBranch = true;

        await _db.SaveChangesAsync();
    }

    public async Task<int> GetBranchCountAsync(string threadId)
    {
        return await _db.Messages
            .Where(m => m.ThreadId == threadId)
            .Select(m => m.BranchId)
            .Distinct()
            .CountAsync();
    }

    // ── Mapping helpers ──

    private static Message MapToDomain(Entities.Message entity)
    {
        return new Message
        {
            Id = entity.Id,
            ThreadId = entity.ThreadId,
            Role = entity.Role,
            Content = entity.Content,
            RawContent = entity.RawContent,
            CreatedAt = entity.CreatedAt,
            BranchId = entity.BranchId,
            ParentMessageId = entity.ParentMessageId,
            VersionNumber = entity.VersionNumber,
            IsActiveBranch = entity.IsActiveBranch,
            IsDirectTransformation = entity.IsDirectTransformation,
            Feedback = entity.Feedback,
            EstimatedCost = entity.EstimatedCost,
            GenerationTimeMs = entity.GenerationTimeMs,
        };
    }

    private static Entities.Message MapToEntity(Message model)
    {
        return new Entities.Message
        {
            Id = model.Id,
            ThreadId = model.ThreadId,
            Role = model.Role,
            Content = model.Content,
            RawContent = model.RawContent,
            CreatedAt = model.CreatedAt,
            BranchId = model.BranchId ?? Guid.NewGuid().ToString("N"),
            ParentMessageId = model.ParentMessageId,
            VersionNumber = model.VersionNumber,
            IsActiveBranch = model.IsActiveBranch,
            IsDirectTransformation = model.IsDirectTransformation,
            Feedback = model.Feedback,
            EstimatedCost = model.EstimatedCost,
            GenerationTimeMs = model.GenerationTimeMs,
        };
    }
}
