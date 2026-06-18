using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IPersonaRepository
{
    Task<IReadOnlyList<Persona>> GetAllAsync();
    Task<Persona?> GetByIdAsync(string id);
    Task<Persona?> GetDefaultAsync();
    Task<Persona> CreateAsync(Persona persona);
    Task UpdateAsync(Persona persona);
    Task DeleteAsync(string id);
}
