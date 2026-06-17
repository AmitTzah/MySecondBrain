using MySecondBrain.Core.Models;

namespace MySecondBrain.Core.Interfaces;

public interface IBackupProvider
{
    string ProviderName { get; }
    BackupProviderType Type { get; }

    Task<BackupResult> UploadAsync(Stream backupData, string backupName, CancellationToken ct);

    Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken ct);

    Task<Stream> DownloadAsync(string backupId, CancellationToken ct);

    Task DeleteAsync(string backupId, CancellationToken ct);

    Task<bool> ValidateCredentialsAsync(CancellationToken ct);
}
