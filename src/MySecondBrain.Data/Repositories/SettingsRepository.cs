using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;

    public SettingsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.Settings.FindAsync(key);
        return setting?.Value;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var json = await GetAsync(key);
        if (json is null)
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        var existing = await _db.Settings.FindAsync(key);
        if (existing is null)
        {
            _db.Settings.Add(new AppSetting
            {
                Key = key,
                Value = value,
                ValueType = "String",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetAsync<T>(string key, T value) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        var existing = await _db.Settings.FindAsync(key);
        if (existing is null)
        {
            _db.Settings.Add(new AppSetting
            {
                Key = key,
                Value = json,
                ValueType = "Json",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.Value = json;
            existing.ValueType = "Json";
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string key)
    {
        var setting = await _db.Settings.FindAsync(key);
        if (setting is not null)
        {
            _db.Settings.Remove(setting);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        var settings = await _db.Settings
            .AsNoTracking()
            .ToListAsync();

        return settings
            .Where(s => s.Value is not null)
            .ToDictionary(s => s.Key, s => s.Value!);
    }
}
