using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Data.Repositories;

public class UsageRepository : IUsageRepository
{
    private readonly AppDbContext _db; // Reserved for EF Core implementation

    public UsageRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task RecordUsageAsync(UsageRecord record) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<UsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to) =>
        Task.FromResult<IReadOnlyList<UsageRecord>>(Array.Empty<UsageRecord>());

    public Task<UsageSummary> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to) =>
        Task.FromResult<UsageSummary>(default!);

    public Task<IReadOnlyList<UsageByProvider>> GetByProviderAsync(DateTimeOffset from, DateTimeOffset to) =>
        Task.FromResult<IReadOnlyList<UsageByProvider>>(Array.Empty<UsageByProvider>());

    public Task<IReadOnlyList<UsageByModel>> GetByModelAsync(DateTimeOffset from, DateTimeOffset to) =>
        Task.FromResult<IReadOnlyList<UsageByModel>>(Array.Empty<UsageByModel>());

    public Task<IReadOnlyList<UsageByChat>> GetByChatAsync(DateTimeOffset from, DateTimeOffset to) =>
        Task.FromResult<IReadOnlyList<UsageByChat>>(Array.Empty<UsageByChat>());

    public Task<FeedbackSummary> GetFeedbackSummaryAsync(DateTimeOffset from, DateTimeOffset to) =>
        Task.FromResult<FeedbackSummary>(default!);
}
