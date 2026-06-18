namespace MySecondBrain.Core.Interfaces;

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync(string key, string value);
    Task SetAsync<T>(string key, T value) where T : class;
    Task DeleteAsync(string key);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync();
}
