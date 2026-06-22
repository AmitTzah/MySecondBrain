using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class ModelConfigurationRepository : IModelConfigurationRepository
{
    private readonly AppDbContext _db;

    public ModelConfigurationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ModelConfiguration>> GetAllAsync()
    {
        var entities = await _db.ModelConfigurations
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ModelConfiguration?> GetByIdAsync(string id)
    {
        var entity = await _db.ModelConfigurations.FindAsync(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<ModelConfiguration> CreateAsync(ModelConfiguration config)
    {
        var entity = MapToEntity(config);
        _db.ModelConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(ModelConfiguration config)
    {
        var entity = await _db.ModelConfigurations.FindAsync(config.Id);
        if (entity is null) return;

        entity.DisplayName = config.DisplayName;
        entity.Provider = config.ProviderType.ToString();
        entity.ModelIdentifier = config.ModelIdentifier;
        entity.Temperature = config.Temperature;
        entity.MaxOutputTokens = config.MaxOutputTokens;
        entity.MaxContextWindow = config.MaxContextWindow;
        entity.ThinkingEnabled = config.ThinkingEnabled;
        entity.ApiKeyId = config.ApiKeyId;
        entity.PricingInputPer1K = config.PricingInputPer1K;
        entity.PricingOutputPer1K = config.PricingOutputPer1K;
        entity.PricingCacheHitPer1K = config.PricingCacheHitPer1K;
        entity.PricingCacheMissPer1K = config.PricingCacheMissPer1K;
        entity.ContextOverflowStrategy = config.ContextOverflowStrategy;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Entry(entity).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await _db.ModelConfigurations
            .Include(mc => mc.Personas)
            .FirstOrDefaultAsync(mc => mc.Id == id);

        if (entity is null) return;

        if (entity.Personas.Count > 0)
            throw new InvalidOperationException(
                $"Cannot delete ModelConfiguration '{entity.DisplayName}' — " +
                $"it is referenced by {entity.Personas.Count} Persona(s).");

        _db.ModelConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Mapping helpers ──

    private static ModelConfiguration MapToDomain(Entities.ModelConfiguration entity)
    {
        return new ModelConfiguration
        {
            Id = entity.Id,
            DisplayName = entity.DisplayName,
            ProviderType = Enum.TryParse<ProviderType>(entity.Provider, out var pt) ? pt : ProviderType.OpenAI,
            ModelIdentifier = entity.ModelIdentifier ?? string.Empty,
            EndpointUrl = null, // Not stored in entity; set by caller if needed
            ApiKeyId = entity.ApiKeyId,
            Temperature = entity.Temperature,
            MaxOutputTokens = entity.MaxOutputTokens,
            MaxContextWindow = entity.MaxContextWindow,
            ThinkingEnabled = entity.ThinkingEnabled,
            ThinkingTokens = null, // Domain-only concept; not stored in entity
            PricingInputPer1K = entity.PricingInputPer1K,
            PricingOutputPer1K = entity.PricingOutputPer1K,
            PricingCacheHitPer1K = entity.PricingCacheHitPer1K,
            PricingCacheMissPer1K = entity.PricingCacheMissPer1K,
            ContextOverflowStrategy = entity.ContextOverflowStrategy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static Entities.ModelConfiguration MapToEntity(ModelConfiguration model)
    {
        return new Entities.ModelConfiguration
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            Provider = model.ProviderType.ToString(),
            ModelIdentifier = model.ModelIdentifier,
            ApiKeyId = model.ApiKeyId,
            Temperature = model.Temperature,
            MaxOutputTokens = model.MaxOutputTokens,
            MaxContextWindow = model.MaxContextWindow,
            ThinkingEnabled = model.ThinkingEnabled,
            PricingInputPer1K = model.PricingInputPer1K,
            PricingOutputPer1K = model.PricingOutputPer1K,
            PricingCacheHitPer1K = model.PricingCacheHitPer1K,
            PricingCacheMissPer1K = model.PricingCacheMissPer1K,
            ContextOverflowStrategy = model.ContextOverflowStrategy,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
    }
}
