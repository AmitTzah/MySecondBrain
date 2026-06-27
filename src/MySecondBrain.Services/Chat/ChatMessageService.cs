using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;

using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Chat;

/// <summary>
/// Handles message-level operations: send, edit, delete, regenerate, continue generation.
/// Owns the streaming LLM interaction and fires <see cref="OnStreamChunk"/> for UI consumption.
/// </summary>
public class ChatMessageService
{
    private readonly IChatThreadRepository _threadRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly ILLMProviderService _llmService;
    private readonly IUsageRepository _usageRepo;
    private readonly ChatTitleGenerator _titleGenerator;
    private readonly ChatThreadLifecycleService _lifecycle;
    private readonly ILogger<ChatMessageService> _logger;

    /// <summary>
    /// Raised when a stream chunk is received during SendMessageAsync / RegenerateAsync / ContinueGenerationAsync.
    /// Subscribers (e.g., the ViewModel) can forward these to the MarkdownStreamRenderer.
    /// </summary>
    public event Action<StreamChunk>? OnStreamChunk;

    public ChatMessageService(
        IChatThreadRepository threadRepo,
        IMessageRepository messageRepo,
        ILLMProviderService llmService,
        IUsageRepository usageRepo,
        ChatTitleGenerator titleGenerator,
        ChatThreadLifecycleService lifecycle,
        ILogger<ChatMessageService> logger)
    {
        _threadRepo = threadRepo;
        _messageRepo = messageRepo;
        _llmService = llmService;
        _usageRepo = usageRepo;
        _titleGenerator = titleGenerator;
        _lifecycle = lifecycle;
        _logger = logger;
    }

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

        var persona = await _lifecycle.ResolvePersonaAsync(thread);
        var modelConfig = await _lifecycle.ResolveModelConfigRequiredAsync(thread, persona);
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

        _logger.LogInformation(
            "SendMessageAsync: calling LLM streaming — thread={ThreadId}, persona={Persona}, config={Config}, model={Model}",
            threadId, persona.DisplayName, modelConfig.DisplayName, modelConfig.ModelIdentifier);

        var responseBuilder = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        var chunkCount = 0;
        int promptTokens = 0, completionTokens = 0;

        try
        {
            await foreach (var chunk in _llmService.ChatStreamAsync(
                thread, content, persona, modelConfig, tools, ct))
            {
                chunkCount++;
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
            _logger.LogInformation(
                "SendMessageAsync: stream completed for thread {ThreadId} — {ChunkCount} chunks, {ResponseLen} chars, {TimeMs}ms",
                threadId, chunkCount, responseBuilder.Length, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "SendMessageAsync cancelled for thread {ThreadId}. Partial response preserved ({Length} chars), {ChunkCount} chunks received.",
                threadId, responseBuilder.Length, chunkCount);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "SendMessageAsync failed for thread {ThreadId} after {ChunkCount} chunks",
                threadId, chunkCount);
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

        var persona = await _lifecycle.ResolvePersonaAsync(thread);
        var modelConfig = await _lifecycle.ResolveModelConfigRequiredAsync(thread, persona);

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

        var persona = await _lifecycle.ResolvePersonaAsync(thread);
        var modelConfig = await _lifecycle.ResolveModelConfigRequiredAsync(thread, persona);
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
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════════

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
