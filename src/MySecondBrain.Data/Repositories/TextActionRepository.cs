using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class TextActionRepository : ITextActionRepository
{
    private readonly AppDbContext _db;

    public TextActionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TextAction>> GetAllAsync()
    {
        var entities = await _db.TextActions
            .AsNoTracking()
            .Include(ta => ta.ModelConfig)
            .OrderBy(ta => ta.DisplayName)
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<TextAction?> GetByIdAsync(string id)
    {
        var entity = await _db.TextActions
            .Include(ta => ta.ModelConfig)
            .FirstOrDefaultAsync(ta => ta.Id == id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<TextAction>> GetByHotkeyAsync(string hotkey)
    {
        var entities = await _db.TextActions
            .AsNoTracking()
            .Where(ta => ta.Hotkey == hotkey)
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<TextAction> CreateAsync(TextAction action)
    {
        var entity = MapToEntity(action);
        _db.TextActions.Add(entity);
        await _db.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(TextAction action)
    {
        var entity = await _db.TextActions.FindAsync(action.Id);
        if (entity is null) return;

        entity.DisplayName = action.DisplayName;
        entity.SystemPrompt = action.SystemPrompt;
        entity.ModelConfigId = action.ModelConfigId;
        entity.Hotkey = action.Hotkey;
        entity.CaptureScope = action.CaptureScope;
        entity.ApplyMode = action.ApplyMode;
        entity.IsBuiltIn = action.IsBuiltIn;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await _db.TextActions.FindAsync(id);
        if (entity is null) return;

        _db.TextActions.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Mapping helpers ──

    private static TextAction MapToDomain(Entities.TextAction entity)
    {
        return new TextAction
        {
            Id = entity.Id,
            DisplayName = entity.DisplayName,
            SystemPrompt = entity.SystemPrompt,
            ModelConfigId = entity.ModelConfigId,
            Hotkey = entity.Hotkey,
            CaptureScope = entity.CaptureScope,
            ApplyMode = entity.ApplyMode,
            IsBuiltIn = entity.IsBuiltIn,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static Entities.TextAction MapToEntity(TextAction model)
    {
        return new Entities.TextAction
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            SystemPrompt = model.SystemPrompt,
            ModelConfigId = model.ModelConfigId,
            Hotkey = model.Hotkey,
            CaptureScope = model.CaptureScope,
            ApplyMode = model.ApplyMode,
            IsBuiltIn = model.IsBuiltIn,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
    }
}
