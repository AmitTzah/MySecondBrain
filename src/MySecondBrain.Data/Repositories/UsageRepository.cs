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

    public async Task<IReadOnlyList<UsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        return allEntities
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .OrderBy(r => r.CreatedAt)
            .Select(MapToDomain)
            .ToList();
    }

    public async Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities.Where(r => r.CreatedAt >= from && r.CreatedAt <= to).ToList();

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

    public async Task<IReadOnlyList<UsageByProvider>> GetByProviderAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities.Where(r => r.CreatedAt >= from && r.CreatedAt <= to).ToList();

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

    public async Task<IReadOnlyList<UsageByModel>> GetByModelAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities.Where(r => r.CreatedAt >= from && r.CreatedAt <= to).ToList();

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

    public async Task<IReadOnlyList<UsageByChat>> GetByChatAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var allEntities = await _db.UsageRecords
            .AsNoTracking()
            .ToListAsync();

        var filtered = allEntities.Where(r => r.CreatedAt >= from && r.CreatedAt <= to).ToList();

        // Get thread titles for display
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

        // AverageRating is the ratio of positive feedback (0.0 to 1.0)
        var ratedCount = positive + negative;
        var averageRating = ratedCount > 0 ? (double)positive / ratedCount : 0;

        return new FeedbackSummary(positive, negative, averageRating);
    }

    // -- Mapping helpers --

    private static UsageRecord MapToDomain(Entities.UsageRecord entity)
    {
        return new UsageRecord
        {
            Id = entity.Id,
            ChatThreadId = entity.ThreadId,
            MessageId = entity.MessageId,
            ModelId = entity.ModelIdentifier,
            ProviderType = ParseProviderType(entity.Provider),
            PromptTokens = entity.PromptTokens,
            CompletionTokens = entity.CompletionTokens,
            TotalTokens = entity.TotalTokens,
            Timestamp = entity.CreatedAt,
        };
    }

    private static Entities.UsageRecord MapToEntity(UsageRecord model)
    {
        return new Entities.UsageRecord
        {
            Id = model.Id,
            MessageId = model.MessageId,
            ThreadId = model.ChatThreadId,
            Provider = model.ProviderType.ToString(),
            ModelIdentifier = model.ModelId,
            PromptTokens = model.PromptTokens,
            CompletionTokens = model.CompletionTokens,
            TotalTokens = model.TotalTokens,
            CreatedAt = model.Timestamp,
        };
    }

    private static ProviderType ParseProviderType(string provider)
    {
        return provider switch
        {
            "OpenAI" => ProviderType.OpenAI,
            "Anthropic" => ProviderType.Anthropic,
            "Google" => ProviderType.Google,
            "OpenAICompatible" => ProviderType.OpenAICompatible,
            _ => ProviderType.OpenAICompatible, // Default for unknown/custom providers
        };
    }
}
