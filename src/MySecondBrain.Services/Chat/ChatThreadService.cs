using System.Diagnostics;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;

namespace MySecondBrain.Services.Chat;

public class ChatThreadService : IChatThreadService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILLMProviderService _llmService;
    private readonly IPersonaRepository _personaRepo;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly IUsageRepository _usageRepo;
    private readonly ChatTitleGenerator _titleGenerator;
    private readonly AppDbContext _db;
    private readonly ILogger<ChatThreadService> _logger;

    /// <summary>
    /// Raised when a stream chunk is received during SendMessageAsync / RegenerateAsync / ContinueGenerationAsync.
    /// Subscribers (e.g., the ViewModel) can forward these to the MarkdownStreamRenderer.
    /// </summary>
    public event Action<StreamChunk>? OnStreamChunk;

    public ChatThreadService(
        IChatThreadRepository threadRepo,
        IMessageRepository messageRepo,
        ILLMProviderService llmService,
        IPersonaRepository personaRepo,
        IModelConfigurationRepository modelConfigRepo,
        IUsageRepository usageRepo,
        ChatTitleGenerator titleGenerator,
        AppDbContext db,
        ILogger<ChatThreadService> logger)
    {
        _threadRepo = threadRepo;
        _messageRepo = messageRepo;
        _llmService = llmService;
        _personaRepo = personaRepo;
        _modelConfigRepo = modelConfigRepo;
        _usageRepo = usageRepo;
        _titleGenerator = titleGenerator;
        _db = db;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Thread Lifecycle
    // ═══════════════════════════════════════════════════════════════

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
    //  Message Operations
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Full message send lifecycle:
    /// 1. Create and persist the user Message entity
    /// 2. Build conversation context (active branch messages)
    /// 3. Resolve Persona + ModelConfiguration
    /// 4. Call LLM streaming API
    /// 5. Accumulate response from stream chunks (preserve partial on cancellation)
    /// 6. Persist assistant Message entity with token counts, cost, timing
    /// 7. Trigger auto-title on first message pair
    /// 8. Update ChatThread.LastActivityAt
    /// </summary>
    public async Task<Message> SendMessageAsync(string threadId, string content, CancellationToken ct)
    {
        // ── 1. Create user message ────────────────────────────────
        var branchId = Guid.NewGuid().ToString("N");

        var userMsg = new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = threadId,
            Role = "User",
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
            VersionNumber = 1,
            BranchId = branchId,
            IsActiveBranch = true,
            ParentMessageId = null,
        };
        await _messageRepo.CreateAsync(userMsg);

        // ── 2. Resolve persona & model config ─────────────────────
        var thread = await _threadRepo.GetByIdAsync(threadId)
            ?? throw new InvalidOperationException($"Thread '{threadId}' not found.");

        var persona = await ResolvePersonaAsync(thread);
        var modelConfig = await ResolveModelConfigRequiredAsync(thread, persona);
        var tools = Array.Empty<ToolDefinition>();

        // ── 3. Call LLM (streaming) ──────────────────────────────
        var assistantMsg = new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = threadId,
            Role = "Assistant",
            Content = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            VersionNumber = 1,
            BranchId = branchId,
            IsActiveBranch = true,
            ParentMessageId = userMsg.Id,
            ModelName = modelConfig.ModelIdentifier,
        };
        await _messageRepo.CreateAsync(assistantMsg);

        var responseBuilder = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        int promptTokens = 0, completionTokens = 0;

        try
        {
            await foreach (var chunk in _llmService.ChatStreamAsync(
                thread, content, persona, modelConfig, tools, ct))
            {
                if (chunk.ContentDelta is not null)
                {
                    responseBuilder.Append(chunk.ContentDelta);
                }

                if (chunk.Usage is not null)
                {
                    promptTokens = chunk.Usage.PromptTokens;
                    completionTokens = chunk.Usage.CompletionTokens;
                }

                OnStreamChunk?.Invoke(chunk);
            }
            stopwatch.Stop();
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "SendMessageAsync cancelled for thread {ThreadId}. Partial response preserved ({Length} chars).",
                threadId, responseBuilder.Length);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "SendMessageAsync failed for thread {ThreadId}", threadId);
            responseBuilder.Append($"[Error: {ex.GetType().Name} — {ex.Message}]");
        }

        // ── 4. Persist assistant message ─────────────────────────
        assistantMsg.Content = responseBuilder.ToString();
        assistantMsg.GenerationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;
        assistantMsg.EstimatedCost = CalculateEstimatedCost(promptTokens, completionTokens, modelConfig);
        await _messageRepo.UpdateAsync(assistantMsg);

        await RecordUsageAsync(thread, assistantMsg, persona, modelConfig, promptTokens, completionTokens);

        // ── 5. Auto-title on first message pair ──────────────────
        if (string.IsNullOrEmpty(thread.Title))
        {
            var title = await _titleGenerator.GenerateTitleAsync(
                content, assistantMsg.Content, persona, modelConfig, ct);
            thread.Title = title;
        }

        // ── 6. Update thread activity ────────────────────────────
        thread.LastActivityAt = DateTimeOffset.UtcNow;
        await _threadRepo.UpdateAsync(thread);

        _logger.LogDebug(
            "SendMessageAsync complete for thread {ThreadId}. Tokens: {PromptTokens}+{CompletionTokens}, Time: {TimeMs}ms",
            threadId, promptTokens, completionTokens, assistantMsg.GenerationTimeMs);

        return assistantMsg;
    }

    public async Task<Message> EditMessageAsync(string messageId, string newContent, bool createBranch)
    {
        var existing = await _messageRepo.GetByIdAsync(messageId)
            ?? throw new InvalidOperationException($"Message '{messageId}' not found.");

        if (createBranch)
        {
            var branchMsg = new Message
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = existing.ThreadId,
                Role = existing.Role,
                Content = newContent,
                CreatedAt = DateTimeOffset.UtcNow,
                VersionNumber = existing.VersionNumber + 1,
                BranchId = existing.BranchId,
                IsActiveBranch = true,
                ParentMessageId = existing.ParentMessageId,
            };

            existing.IsActiveBranch = false;
            await _messageRepo.UpdateAsync(existing);

            await _messageRepo.CreateAsync(branchMsg);
            return branchMsg;
        }
        else
        {
            existing.Content = newContent;
            existing.VersionNumber++;
            await _messageRepo.UpdateAsync(existing);
            return existing;
        }
    }

    public async Task DeleteMessageAsync(string messageId)
    {
        var msg = await _messageRepo.GetByIdAsync(messageId);
        if (msg is null) return;

        msg.IsActiveBranch = false;
        await _messageRepo.UpdateAsync(msg);
        _logger.LogDebug("Deleted (deactivated) Message {MessageId}", messageId);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Regeneration & Continuation
    // ═══════════════════════════════════════════════════════════════

    public async Task<Message> RegenerateAsync(string messageId, CancellationToken ct)
    {
        var existing = await _messageRepo.GetByIdAsync(messageId)
            ?? throw new InvalidOperationException($"Message '{messageId}' not found.");

        if (existing.Role != "Assistant")
            throw new InvalidOperationException("Only assistant messages can be regenerated.");

        var threadId = existing.ThreadId;
        var thread = await _threadRepo.GetByIdAsync(threadId)
            ?? throw new InvalidOperationException($"Thread '{threadId}' not found.");

        existing.IsActiveBranch = false;
        await _messageRepo.UpdateAsync(existing);

        var newMsg = new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = threadId,
            Role = "Assistant",
            Content = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            VersionNumber = existing.VersionNumber + 1,
            BranchId = existing.BranchId,
            IsActiveBranch = true,
            ParentMessageId = existing.ParentMessageId,
            ModelName = existing.ModelName,
        };
        await _messageRepo.CreateAsync(newMsg);

        var persona = await ResolvePersonaAsync(thread);
        var modelConfig = await ResolveModelConfigRequiredAsync(thread, persona);

        var userMsg = await _messageRepo.GetByIdAsync(existing.ParentMessageId ?? string.Empty);
        var userContent = userMsg?.Content ?? string.Empty;

        var tools = Array.Empty<ToolDefinition>();
        var responseBuilder = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        int promptTokens = 0, completionTokens = 0;

        try
        {
            await foreach (var chunk in _llmService.ChatStreamAsync(
                thread, userContent, persona, modelConfig, tools, ct))
            {
                if (chunk.ContentDelta is not null)
                    responseBuilder.Append(chunk.ContentDelta);

                if (chunk.Usage is not null)
                {
                    promptTokens = chunk.Usage.PromptTokens;
                    completionTokens = chunk.Usage.CompletionTokens;
                }

                OnStreamChunk?.Invoke(chunk);
            }
            stopwatch.Stop();
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "RegenerateAsync cancelled for thread {ThreadId}. Partial preserved ({Length} chars).",
                threadId, responseBuilder.Length);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "RegenerateAsync failed for thread {ThreadId}", threadId);
            responseBuilder.Append($"[Error: {ex.GetType().Name} — {ex.Message}]");
        }

        newMsg.Content = responseBuilder.ToString();
        newMsg.GenerationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;
        newMsg.EstimatedCost = CalculateEstimatedCost(promptTokens, completionTokens, modelConfig);
        await _messageRepo.UpdateAsync(newMsg);

        await RecordUsageAsync(thread, newMsg, persona, modelConfig, promptTokens, completionTokens);

        thread.LastActivityAt = DateTimeOffset.UtcNow;
        await _threadRepo.UpdateAsync(thread);

        return newMsg;
    }

    public async Task<Message> ContinueGenerationAsync(string threadId, CancellationToken ct)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId)
            ?? throw new InvalidOperationException($"Thread '{threadId}' not found.");

        var history = await _messageRepo.GetActiveBranchAsync(threadId);
        var lastMsg = history.LastOrDefault();
        if (lastMsg is null || lastMsg.Role != "Assistant")
            throw new InvalidOperationException("No assistant message to continue from.");

        var persona = await ResolvePersonaAsync(thread);
        var modelConfig = await ResolveModelConfigRequiredAsync(thread, persona);
        var tools = Array.Empty<ToolDefinition>();

        var continuationPrompt = $"Continue the above response. Previous content:\n\n{lastMsg.Content}\n\nContinue:";
        var responseBuilder = new StringBuilder(lastMsg.Content);
        var stopwatch = Stopwatch.StartNew();
        int promptTokens = 0, completionTokens = 0;

        // Preserve original content before mutation
        lastMsg.RawContent = lastMsg.Content;

        try
        {
            await foreach (var chunk in _llmService.ChatStreamAsync(
                thread, continuationPrompt, persona, modelConfig, tools, ct))
            {
                if (chunk.ContentDelta is not null)
                    responseBuilder.Append(chunk.ContentDelta);

                if (chunk.Usage is not null)
                {
                    promptTokens = chunk.Usage.PromptTokens;
                    completionTokens = chunk.Usage.CompletionTokens;
                }

                OnStreamChunk?.Invoke(chunk);
            }
            stopwatch.Stop();
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "ContinueGenerationAsync cancelled for thread {ThreadId}. Partial preserved ({Length} chars).",
                threadId, responseBuilder.Length);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "ContinueGenerationAsync failed for thread {ThreadId}", threadId);
            responseBuilder.Append($"[Error: {ex.GetType().Name} — {ex.Message}]");
        }

        lastMsg.Content = responseBuilder.ToString();
        lastMsg.GenerationTimeMs = (long)stopwatch.Elapsed.TotalMilliseconds;
        lastMsg.EstimatedCost = CalculateEstimatedCost(promptTokens, completionTokens, modelConfig);
        await _messageRepo.UpdateAsync(lastMsg);

        await RecordUsageAsync(thread, lastMsg, persona, modelConfig, promptTokens, completionTokens);

        thread.LastActivityAt = DateTimeOffset.UtcNow;
        await _threadRepo.UpdateAsync(thread);

        return lastMsg;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Branch & Search Operations
    // ═══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Message>> GetActiveBranchMessagesAsync(string threadId)
    {
        return await _messageRepo.GetActiveBranchAsync(threadId);
    }

    public async Task SetActiveBranchAsync(string messageId, string branchId)
    {
        await _messageRepo.SetActiveBranch(messageId, branchId);
    }

    public async Task<int> GetBranchCountAsync(string threadId)
    {
        return await _messageRepo.GetBranchCountAsync(threadId);
    }

    public async Task<ChatTree> GetChatTreeAsync(string threadId)
    {
        var allMessages = await _messageRepo.GetAllBranchesForThreadAsync(threadId);

        var nodes = allMessages.Select(m => new ChatTreeNode(
            m.Id,
            m.ParentMessageId,
            m.BranchId ?? string.Empty,
            m.IsActiveBranch,
            m.Role,
            m.Content.Length > 100 ? m.Content[..100] : m.Content
        )).ToList();

        return new ChatTree(threadId, nodes);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchMessagesAsync(string query, int maxResults)
    {
        var messages = await _messageRepo.SearchAsync(query, maxResults);
        return messages.Select(m => new SearchResult(
            m.Id,
            m.ThreadId,
            string.Empty, // thread title will be resolved by caller if needed
            m.Content.Length > 150 ? m.Content[..150] : m.Content,
            m.Role,
            m.CreatedAt
        )).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Drafts
    // ═══════════════════════════════════════════════════════════════

    public async Task SaveDraftAsync(string threadId, string content, int cursorPosition)
    {
        var existing = await _db.MessageDrafts.FindAsync(threadId);
        if (existing is not null)
        {
            existing.Content = content;
            existing.CursorPosition = cursorPosition;
            existing.SavedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.MessageDrafts.Add(new Data.Entities.MessageDrafts
            {
                ThreadId = threadId,
                Content = content,
                CursorPosition = cursorPosition,
                SavedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<MessageDraft?> GetDraftAsync(string threadId)
    {
        var entity = await _db.MessageDrafts.FindAsync(threadId);
        if (entity is null) return null;

        return new MessageDraft(entity.ThreadId, entity.Content, entity.CursorPosition);
    }

    public async Task DeleteDraftAsync(string threadId)
    {
        var entity = await _db.MessageDrafts.FindAsync(threadId);
        if (entity is null) return;

        _db.MessageDrafts.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════════

    private async Task<Persona> ResolvePersonaAsync(ChatThread thread)
    {
        if (!string.IsNullOrEmpty(thread.PersonaId))
        {
            var persona = await _personaRepo.GetByIdAsync(thread.PersonaId);
            if (persona is not null) return persona;
        }

        return await _personaRepo.GetDefaultAsync()
            ?? throw new InvalidOperationException("No personas configured.");
    }

    /// <summary>
    /// Resolves the ModelConfiguration for a thread. Throws if none is available.
    /// </summary>
    private async Task<ModelConfiguration> ResolveModelConfigRequiredAsync(ChatThread thread, Persona persona)
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

    private static decimal? CalculateEstimatedCost(
        int promptTokens,
        int completionTokens,
        ModelConfiguration? config)
    {
        if (config is null) return null;
        if (config.PricingInputPer1K is null && config.PricingOutputPer1K is null) return null;

        decimal cost = 0;
        if (config.PricingInputPer1K.HasValue)
            cost += promptTokens * (config.PricingInputPer1K.Value / 1000m);
        if (config.PricingOutputPer1K.HasValue)
            cost += completionTokens * (config.PricingOutputPer1K.Value / 1000m);

        return cost;
    }

    private async Task RecordUsageAsync(
        ChatThread thread,
        Message message,
        Persona persona,
        ModelConfiguration? config,
        int promptTokens,
        int completionTokens)
    {
        try
        {
            var record = new UsageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = thread.Id,
                MessageId = message.Id,
                ModelIdentifier = config?.ModelIdentifier ?? "unknown",
                ProviderType = config?.ProviderType ?? ProviderType.OpenAI,
                PersonaId = persona.Id,
                ModelConfigId = config?.Id,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                EstimatedCost = message.EstimatedCost,
                CreatedAt = DateTimeOffset.UtcNow,
                LatencyMs = (int)(message.GenerationTimeMs ?? 0),
                Tier = 3,
            };

            await _usageRepo.RecordUsageAsync(record);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Failed to record usage for thread {ThreadId}, message {MessageId}",
                thread.Id, message.Id);
        }
    }
}
