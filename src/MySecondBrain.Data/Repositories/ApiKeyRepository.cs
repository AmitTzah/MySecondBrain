using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _db;

    public ApiKeyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApiKey>> GetAllAsync()
    {
        var entities = await _db.ApiKeys
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ApiKey?> GetByIdAsync(string id)
    {
        var entity = await _db.ApiKeys.FindAsync(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ApiKey>> GetByProviderAsync(ProviderType provider)
    {
        var providerStr = provider.ToString();
        var entities = await _db.ApiKeys
            .AsNoTracking()
            .Where(k => k.Provider == providerStr)
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ApiKey> CreateAsync(ApiKey key)
    {
        var entity = MapToEntity(key);
        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(ApiKey key)
    {
        var entity = await _db.ApiKeys.FindAsync(key.Id);
        if (entity is null) return;

        entity.DisplayName = key.Label ?? string.Empty;
        entity.Provider = key.ProviderType.ToString();
        entity.KeyValue = key.EncryptedValue;
        entity.CustomProviderName = key.CustomProviderName;
        entity.CustomEndpointUrl = key.CustomEndpointUrl;
        entity.IsValid = key.IsValid;
        entity.LastTestedAt = key.LastTestedAt;

        _db.Entry(entity).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await _db.ApiKeys
            .Include(k => k.ModelConfigurations)
            .FirstOrDefaultAsync(k => k.Id == id);

        if (entity is null) return;

        // Nullify FK on ModelConfigurations referencing this key (SetNull behavior)
        foreach (var mc in entity.ModelConfigurations)
            mc.ApiKeyId = null;

        _db.ApiKeys.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Mapping helpers ──

    private static ApiKey MapToDomain(Entities.ApiKey entity)
    {
        return new ApiKey
        {
            Id = entity.Id,
            ProviderType = Enum.TryParse<ProviderType>(entity.Provider, out var pt) ? pt : ProviderType.OpenAI,
            EncryptedValue = entity.KeyValue,
            Label = entity.DisplayName,
            CustomProviderName = entity.CustomProviderName,
            CustomEndpointUrl = entity.CustomEndpointUrl,
            CreatedAt = entity.CreatedAt,
            LastTestedAt = entity.LastTestedAt,
            IsValid = entity.IsValid,
        };
    }

    private static Entities.ApiKey MapToEntity(ApiKey model)
    {
        return new Entities.ApiKey
        {
            Id = model.Id,
            DisplayName = model.Label ?? string.Empty,
            Provider = model.ProviderType.ToString(),
            KeyValue = model.EncryptedValue,
            CustomProviderName = model.CustomProviderName,
            CustomEndpointUrl = model.CustomEndpointUrl,
            IsValid = model.IsValid,
            LastTestedAt = model.LastTestedAt,
            CreatedAt = model.CreatedAt,
        };
    }
}
