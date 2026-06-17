using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class PersonaRepository : IPersonaRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public PersonaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<Persona>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Persona>>(Array.Empty<Persona>());

    public Task<Persona?> GetByIdAsync(string id) =>
        Task.FromResult<Persona?>(null);

    public Task<Persona?> GetDefaultAsync() =>
        Task.FromResult<Persona?>(null);

    public Task<Persona> CreateAsync(Persona persona) =>
        Task.FromResult<Persona>(default!);

    public Task UpdateAsync(Persona persona) =>
        Task.CompletedTask;

    public Task DeleteAsync(string id) =>
        Task.CompletedTask;
}
