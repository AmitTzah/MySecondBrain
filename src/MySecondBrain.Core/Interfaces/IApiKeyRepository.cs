using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKey>> GetAllAsync();
    Task<ApiKey?> GetByIdAsync(string id);
    Task<IReadOnlyList<ApiKey>> GetByProviderAsync(ProviderType provider);
    Task<ApiKey> CreateAsync(ApiKey key);
    Task UpdateAsync(ApiKey key);
    Task DeleteAsync(string id);
}
