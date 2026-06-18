using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public SettingsRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<string?> GetAsync(string key) =>
        Task.FromResult<string?>(null);

    public Task<T?> GetAsync<T>(string key) where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync(string key, string value) =>
        Task.CompletedTask;

    public Task SetAsync<T>(string key, T value) where T : class =>
        Task.CompletedTask;

    public Task DeleteAsync(string key) =>
        Task.CompletedTask;

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
}
