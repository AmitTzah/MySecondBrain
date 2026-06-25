using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IUsageRepository
{
    Task RecordUsageAsync(UsageRecord record);

    Task<IReadOnlyList<UsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);

    Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);

    Task<IReadOnlyList<UsageByProvider>> GetByProviderAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);

    Task<IReadOnlyList<UsageByModel>> GetByModelAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);

    Task<IReadOnlyList<UsageByChat>> GetByChatAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);

    Task<FeedbackSummary> GetFeedbackSummaryAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Aggregated cache token usage over a time range, optionally filtered by provider, model, and tier.
    /// </summary>
    Task<CacheSummary> GetCacheSummaryAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);

    /// <summary>
    /// Latency distribution statistics over a time range, optionally filtered by provider, model, and tier.
    /// </summary>
    Task<LatencyDistribution> GetLatencyDistributionAsync(DateTimeOffset from, DateTimeOffset to,
        string? provider = null, string? model = null, int? tier = null);
}
