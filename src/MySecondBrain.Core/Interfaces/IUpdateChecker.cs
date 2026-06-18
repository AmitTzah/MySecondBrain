using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct);

    Task<Stream> DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct);

    Task InstallAsync(Stream updatePackage, CancellationToken ct);

    Version CurrentVersion { get; }

    string UpdateFeedUrl { get; }
}
