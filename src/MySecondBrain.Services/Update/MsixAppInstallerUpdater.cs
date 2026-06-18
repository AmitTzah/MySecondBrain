using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Update;

public class MsixAppInstallerUpdater : IUpdateChecker
{
    private readonly ILogger<MsixAppInstallerUpdater> _logger;

    public MsixAppInstallerUpdater(ILogger<MsixAppInstallerUpdater> logger)
    {
        _logger = logger;
    }

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct) =>
        Task.FromResult(new UpdateCheckResult(false, null, null));

    public Task<Stream> DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct) =>
        Task.FromResult<Stream>(Stream.Null);

    public Task InstallAsync(Stream updatePackage, CancellationToken ct) =>
        Task.CompletedTask;

    public Version CurrentVersion => new(0, 0, 0);

    public string UpdateFeedUrl => string.Empty;
}
