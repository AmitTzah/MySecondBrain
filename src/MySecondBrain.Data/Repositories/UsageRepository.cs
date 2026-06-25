using Microsoft.EntityFrameworkCore;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using Entities = MySecondBrain.Data.Entities;

namespace MySecondBrain.Data.Repositories;

public class UsageRepository : IUsageRepository
{
    private readonly AppDbContext _db;

    public UsageRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task RecordUsageAsync(UsageRecord record)
    {
        var entity = MapToEntity(record);
        _db.UsageRecords.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<UsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        return allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .OrderBy(r => r.CreatedAt)
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .ToList();

        if (filtered.Count == 0)
            return new UsageSummary(0, 0, 0, 0, 0);

        return new UsageSummary(
            filtered.Count,
            filtered.Sum(r => (long)r.PromptTokens),
            filtered.Sum(r => (long)r.CompletionTokens),
            filtered.Sum(r => (long)r.TotalTokens),
            filtered.Sum(r => r.EstimatedCost ?? 0)
        );
    }

    public async Task<IReadOnlyList<UsageByProvider>> GetByProviderAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .ToList();

        return filtered
            .GroupBy(r => r.Provider)
            .Select(g =>
            {
                var providerType = ParseProviderType(g.Key);
                return new UsageByProvider(
                    providerType,
                    g.Count(),
                    g.Sum(r => (long)r.TotalTokens),
                    g.Sum(r => r.EstimatedCost ?? 0)
                );
            })
            .OrderByDescending(p => p.TotalTokens)
            .ToList();
    }

    public async Task<IReadOnlyList<UsageByModel>> GetByModelAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .ToList();

        return filtered
            .GroupBy(r => r.ModelIdentifier)
            .Select(g => new UsageByModel(
                g.Key,
                g.Count(),
                g.Sum(r => (long)r.TotalTokens),
                g.Sum(r => r.EstimatedCost ?? 0)
            ))
            .OrderByDescending(m => m.TotalTokens)
            .ToList();
    }

    public async Task<IReadOnlyList<UsageByChat>> GetByChatAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .ToList();

        var threadIds = filtered.Select(r => r.ThreadId).Distinct().ToList();
        var threads = await _db.ChatThreads
            .Where(t => threadIds.Contains(t.Id))
            .AsNoTracking()
            .ToListAsync();
        var titleMap = threads.ToDictionary(t => t.Id, t => t.Title ?? "(Untitled)");

        return filtered
            .GroupBy(r => r.ThreadId)
            .Select(g => new UsageByChat(
                g.Key,
                titleMap.GetValueOrDefault(g.Key, "(Unknown)"),
                g.Count(),
                g.Sum(r => (long)r.TotalTokens)
            ))
            .OrderByDescending(c => c.TotalTokens)
            .ToList();
    }

    public async Task<FeedbackSummary> GetFeedbackSummaryAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var allMessages = await _db.Messages
            .AsNoTracking()
            .ToListAsync();

        var filtered = allMessages.Where(m => m.CreatedAt >= from && m.CreatedAt <= to).ToList();

        var positive = 0;
        var negative = 0;

        foreach (var msg in filtered)
        {
            if (string.IsNullOrEmpty(msg.Feedback))
                continue;

            if (msg.Feedback.Equals("thumbs_up", StringComparison.OrdinalIgnoreCase))
                positive++;
            else if (msg.Feedback.Equals("thumbs_down", StringComparison.OrdinalIgnoreCase))
                negative++;
        }

        var ratedCount = positive + negative;
        var averageRating = ratedCount > 0 ? (double)positive / ratedCount : 0;

        return new FeedbackSummary(positive, negative, averageRating);
    }

    // ── Step 2: New enriched query methods ──────────────────────────

    public async Task<CacheSummary> GetCacheSummaryAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .ToList();

        var totalCacheRead = filtered.Sum(r => (long)r.CacheReadTokens);
        var totalCacheCreation = filtered.Sum(r => (long)r.CacheCreationTokens);
        var totalPrompt = filtered.Sum(r => (long)r.PromptTokens);

        var denominator = totalCacheRead + totalPrompt;
        var hitRate = denominator > 0
            ? (double)totalCacheRead / denominator
            : 0.0;

        var byProvider = filtered
            .GroupBy(r => r.Provider)
            .Select(g =>
            {
                var grpPrompt = g.Sum(r => (long)r.PromptTokens);
                var grpRead = g.Sum(r => (long)r.CacheReadTokens);
                var grpDenom = grpRead + grpPrompt;
                return new CacheByProvider(
                    g.Key,
                    grpRead,
                    g.Sum(r => (long)r.CacheCreationTokens),
                    grpDenom > 0 ? (double)grpRead / grpDenom : 0.0
                );
            })
            .OrderByDescending(c => c.CacheReadTokens)
            .ToList();

        return new CacheSummary(totalCacheRead, totalCacheCreation, hitRate, byProvider);
    }

    public async Task<LatencyDistribution> GetLatencyDistributionAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .WhereIf(!string.IsNullOrWhiteSpace(provider), r => r.Provider == provider)
            .WhereIf(!string.IsNullOrWhiteSpace(model), r => r.ModelIdentifier == model)
            .WhereIf(tier.HasValue, r => r.Tier == tier!.Value)
            .Where(r => r.LatencyMs > 0)
            .ToList();

        if (filtered.Count == 0)
        {
            return new LatencyDistribution(0, 0, 0, 0, Array.Empty<LatencyByModel>());
        }

        // Percentile calculation using floor-based indexing: P50 at index floor(N * 0.50), etc.
        // This is one valid convention. Alternative: nearest-rank uses Ceiling(N * p) - 1.
        var latencies = filtered.Select(r => r.LatencyMs).OrderBy(v => v).ToList();
        var avg = latencies.Average();
        var p50 = latencies[(int)(latencies.Count * 0.50)];
        var p95 = latencies[(int)(latencies.Count * 0.95)];
        var p99 = latencies[(int)(latencies.Count * 0.99)];

        var byModel = filtered
            .GroupBy(r => r.ModelIdentifier)
            .Select(g =>
            {
                var m = g.Select(r => r.LatencyMs).OrderBy(v => v).ToList();
                return new LatencyByModel(
                    g.Key,
                    m.Average(),
                    m[(int)(m.Count * 0.50)],
                    m[(int)(m.Count * 0.95)],
                    m[(int)(m.Count * 0.99)]
                );
            })
            .OrderByDescending(m => m.AverageMs)
            .ToList();

        return new LatencyDistribution(avg, p50, p95, p99, byModel);
    }

    // ── Mapping helpers ────────────────────────────────────────────

    private static UsageRecord MapToDomain(Entities.UsageRecord entity)
    {
        return new UsageRecord
        {
            Id = entity.Id,
            ThreadId = entity.ThreadId,
            MessageId = entity.MessageId,
            ModelIdentifier = entity.ModelIdentifier,
            ProviderType = ParseProviderType(entity.Provider),
            PersonaId = entity.PersonaId,
            ModelConfigId = entity.ModelConfigId,
            PromptTokens = entity.PromptTokens,
            CompletionTokens = entity.CompletionTokens,
            TotalTokens = entity.TotalTokens,
            EstimatedCost = entity.EstimatedCost,
            CreatedAt = entity.CreatedAt,

            // Step 2 enriched fields
            CacheReadTokens = entity.CacheReadTokens,
            CacheCreationTokens = entity.CacheCreationTokens,
            LatencyMs = entity.LatencyMs,
            Tier = entity.Tier,
            ErrorType = entity.ErrorType,
            ErrorMessage = entity.ErrorMessage,
            ErrorStatusCode = entity.ErrorStatusCode,
            RawJsonPath = entity.RawJsonPath,
        };
    }

    private static Entities.UsageRecord MapToEntity(UsageRecord model)
    {
        return new Entities.UsageRecord
        {
            Id = model.Id,
            MessageId = model.MessageId,
            ThreadId = model.ThreadId,
            Provider = model.ProviderType.ToString(),
            ModelIdentifier = model.ModelIdentifier,
            PersonaId = model.PersonaId,
            ModelConfigId = model.ModelConfigId,
            PromptTokens = model.PromptTokens,
            CompletionTokens = model.CompletionTokens,
            TotalTokens = model.TotalTokens,
            EstimatedCost = model.EstimatedCost,
            CreatedAt = model.CreatedAt,

            // Step 2 enriched fields
            CacheReadTokens = model.CacheReadTokens,
            CacheCreationTokens = model.CacheCreationTokens,
            LatencyMs = model.LatencyMs,
            Tier = model.Tier,
            ErrorType = model.ErrorType,
            ErrorMessage = model.ErrorMessage,
            ErrorStatusCode = model.ErrorStatusCode,
            RawJsonPath = model.RawJsonPath,
        };
    }

    private static ProviderType ParseProviderType(string provider)
    {
        return provider switch
        {
            "OpenAI" => ProviderType.OpenAI,
            "Anthropic" => ProviderType.Anthropic,
            "Google" => ProviderType.Google,
            "DeepSeek" => ProviderType.DeepSeek,
            "MiMo" => ProviderType.MiMo,
            "Moonshot" => ProviderType.Moonshot,
            "Mistral" => ProviderType.Mistral,
            "OpenAICompatible" => ProviderType.OpenAICompatible,
            _ => ProviderType.OpenAICompatible, // Default for unknown/custom providers
        };
    }
}

/// <summary>
/// Extension methods for conditional filtering on in-memory sequences.
/// </summary>
internal static class EnumerableFilterExtensions
{
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }
}
