using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class PersonaRepository : IPersonaRepository
{
    private readonly AppDbContext _db;

    public PersonaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Persona>> GetAllAsync()
    {
        var entities = await _db.Personas
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Persona?> GetByIdAsync(string id)
    {
        var entity = await _db.Personas.FindAsync(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Persona?> GetDefaultAsync()
    {
        var entity = await _db.Personas
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(p => p.IsBuiltIn)
            ?? await _db.Personas
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync();
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Persona> CreateAsync(Persona persona)
    {
        var entity = MapToEntity(persona);
        _db.Personas.Add(entity);
        await _db.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(Persona persona)
    {
        var entity = await _db.Personas.FindAsync(persona.Id);
        if (entity is null) return;

        entity.DisplayName = persona.DisplayName;
        entity.SystemPrompt = persona.SystemPrompt;
        entity.IsBuiltIn = persona.IsBuiltIn;
        entity.DefaultModelConfigId = persona.DefaultModelConfigId;
        entity.DefaultChatMode = persona.DefaultChatMode;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Entry(entity).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await _db.Personas.FindAsync(id);
        if (entity is null) return;

        _db.Personas.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Mapping helpers ──

    private static Persona MapToDomain(Entities.Persona entity)
    {
        return new Persona
        {
            Id = entity.Id,
            DisplayName = entity.DisplayName,
            SystemPrompt = entity.SystemPrompt,
            DefaultModelConfigId = entity.DefaultModelConfigId,
            DefaultChatMode = entity.DefaultChatMode,
            IsBuiltIn = entity.IsBuiltIn,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static Entities.Persona MapToEntity(Persona model)
    {
        return new Entities.Persona
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            SystemPrompt = model.SystemPrompt,
            DefaultModelConfigId = model.DefaultModelConfigId,
            DefaultChatMode = model.DefaultChatMode,
            IsBuiltIn = model.IsBuiltIn,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
    }
}
