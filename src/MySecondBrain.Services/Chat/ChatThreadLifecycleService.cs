using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Manages ChatThread lifecycle: creation, retrieval, soft/hard delete, restore, elevation.
/// </summary>
public class ChatThreadLifecycleService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly IPersonaRepository _personaRepo;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly ILogger<ChatThreadLifecycleService> _logger;

    public ChatThreadLifecycleService(
        IChatThreadRepository threadRepo,
        IPersonaRepository personaRepo,
        IModelConfigurationRepository modelConfigRepo,
        ILogger<ChatThreadLifecycleService> logger)
    {
        _threadRepo = threadRepo;
        _personaRepo = personaRepo;
        _modelConfigRepo = modelConfigRepo;
        _logger = logger;
    }

    public async Task<ChatThread> CreateThreadAsync(string? title, bool isTransient, Persona persona)
    {
        var thread = new ChatThread
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            IsTransient = isTransient,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            PersonaId = persona.Id,
            ModelConfigId = persona.DefaultModelConfigId,
        };

        await _threadRepo.CreateAsync(thread);
        _logger.LogDebug("Created ChatThread {ThreadId} (transient: {IsTransient})", thread.Id, isTransient);
        return thread;
    }

    public async Task<ChatThread?> GetThreadAsync(string threadId)
    {
        return await _threadRepo.GetByIdAsync(threadId);
    }

    public async Task<IReadOnlyList<ChatThread>> GetPermanentThreadsAsync(ChatSortOrder sort)
    {
        return await _threadRepo.GetAllPermanentAsync(sort);
    }

    public async Task<IReadOnlyList<ChatThread>> GetTransientThreadsAsync()
    {
        return await _threadRepo.GetTransientInWindowAsync();
    }

    public async Task SoftDeleteThreadAsync(string threadId)
    {
        await _threadRepo.SoftDeleteAsync(threadId);
        _logger.LogDebug("Soft-deleted ChatThread {ThreadId}", threadId);
    }

    public async Task RestoreThreadAsync(string threadId)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread is null)
        {
            _logger.LogWarning("Cannot restore ChatThread {ThreadId}: not found", threadId);
            return;
        }

        thread.IsDeleted = false;
        await _threadRepo.UpdateAsync(thread);
        _logger.LogDebug("Restored ChatThread {ThreadId}", threadId);
    }

    public async Task PermanentDeleteThreadAsync(string threadId)
    {
        await _threadRepo.PermanentDeleteAsync(threadId);
        _logger.LogDebug("Permanently deleted ChatThread {ThreadId}", threadId);
    }

    public async Task ElevateToPermanentAsync(string threadId)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread is null)
        {
            _logger.LogWarning("Cannot elevate ChatThread {ThreadId}: not found", threadId);
            return;
        }

        thread.IsTransient = false;
        await _threadRepo.UpdateAsync(thread);
        _logger.LogDebug("Elevated ChatThread {ThreadId} to permanent", threadId);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Internal Helpers (used by sub-services)
    // ═══════════════════════════════════════════════════════════════

    internal async Task<Persona> ResolvePersonaAsync(ChatThread thread)
    {
        if (!string.IsNullOrEmpty(thread.PersonaId))
        {
            var persona = await _personaRepo.GetByIdAsync(thread.PersonaId);
            if (persona is not null) return persona;
        }

        return await _personaRepo.GetDefaultAsync()
            ?? throw new InvalidOperationException("No personas configured.");
    }

    internal async Task<ModelConfiguration> ResolveModelConfigRequiredAsync(ChatThread thread, Persona persona)
    {
        var configId = thread.ModelConfigId ?? persona.DefaultModelConfigId;

        if (!string.IsNullOrEmpty(configId))
        {
            var config = await _modelConfigRepo.GetByIdAsync(configId);
            if (config is not null) return config;
        }

        var allConfigs = await _modelConfigRepo.GetAllAsync();
        return allConfigs?.FirstOrDefault()
            ?? throw new InvalidOperationException("No model configurations configured.");
    }
}
