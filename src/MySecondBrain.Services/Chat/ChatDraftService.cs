using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Data.Entities;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Manages message drafts (one draft per thread, persisted to the database).
/// </summary>
public class ChatDraftService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChatDraftService> _logger;

    public ChatDraftService(
        AppDbContext db,
        ILogger<ChatDraftService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveDraftAsync(string threadId, string content, int cursorPosition)
    {
        var existing = await _db.MessageDrafts.FindAsync(threadId);
        if (existing is not null)
        {
            existing.Content = content;
            existing.CursorPosition = cursorPosition;
            existing.SavedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.MessageDrafts.Add(new MessageDrafts
            {
                ThreadId = threadId,
                Content = content,
                CursorPosition = cursorPosition,
                SavedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<MessageDraft?> GetDraftAsync(string threadId)
    {
        var entity = await _db.MessageDrafts.FindAsync(threadId);
        if (entity is null) return null;

        return new MessageDraft(entity.ThreadId, entity.Content, entity.CursorPosition);
    }

    public async Task DeleteDraftAsync(string threadId)
    {
        var entity = await _db.MessageDrafts.FindAsync(threadId);
        if (entity is null) return;

        _db.MessageDrafts.Remove(entity);
        await _db.SaveChangesAsync();
    }
}
