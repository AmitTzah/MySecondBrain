using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class ModelConfigurationRepository : IModelConfigurationRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public ModelConfigurationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ModelConfiguration>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<ModelConfiguration>>(Array.Empty<ModelConfiguration>());

    public Task<ModelConfiguration?> GetByIdAsync(string id) =>
        Task.FromResult<ModelConfiguration?>(null);

    public Task<ModelConfiguration> CreateAsync(ModelConfiguration config) =>
        Task.FromResult<ModelConfiguration>(default!);

    public Task UpdateAsync(ModelConfiguration config) =>
        Task.CompletedTask;

    public Task DeleteAsync(string id) =>
        Task.CompletedTask;
}
