using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public ApiKeyRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ApiKey>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<ApiKey>>(Array.Empty<ApiKey>());

    public Task<ApiKey?> GetByIdAsync(string id) =>
        Task.FromResult<ApiKey?>(null);

    public Task<ApiKey> CreateAsync(ApiKey key) =>
        Task.FromResult<ApiKey>(default!);

    public Task UpdateAsync(ApiKey key) =>
        Task.CompletedTask;

    public Task DeleteAsync(string id) =>
        Task.CompletedTask;
}
