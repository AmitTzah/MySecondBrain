using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IUsageRepository
{
    Task RecordUsageAsync(UsageRecord record);
    Task<IReadOnlyList<UsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to);
    Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<UsageByProvider>> GetByProviderAsync(DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<UsageByModel>> GetByModelAsync(DateTimeOffset from, DateTimeOffset to);
    Task<IReadOnlyList<UsageByChat>> GetByChatAsync(DateTimeOffset from, DateTimeOffset to);
    Task<FeedbackSummary> GetFeedbackSummaryAsync(DateTimeOffset from, DateTimeOffset to);
}
