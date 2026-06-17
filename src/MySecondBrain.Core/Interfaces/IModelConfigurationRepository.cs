using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IModelConfigurationRepository
{
    Task<IReadOnlyList<ModelConfiguration>> GetAllAsync();
    Task<ModelConfiguration?> GetByIdAsync(string id);
    Task<ModelConfiguration> CreateAsync(ModelConfiguration config);
    Task UpdateAsync(ModelConfiguration config);
    Task DeleteAsync(string id);
}
