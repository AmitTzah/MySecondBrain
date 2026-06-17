using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Backup;

public class LocalFolderBackupProvider : IBackupProvider
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<LocalFolderBackupProvider> _logger;

    public LocalFolderBackupProvider(
        IEncryptionService encryptionService,
        ILogger<LocalFolderBackupProvider> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string ProviderName => "Local Folder";

    public BackupProviderType Type => BackupProviderType.LocalFolder;

    public Task<BackupResult> UploadAsync(Stream backupData, string backupName, CancellationToken ct) =>
        Task.FromResult<BackupResult>(default!);

    public Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BackupInfo>>(Array.Empty<BackupInfo>());

    public Task<Stream> DownloadAsync(string backupId, CancellationToken ct) =>
        Task.FromResult<Stream>(Stream.Null);

    public Task DeleteAsync(string backupId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<bool> ValidateCredentialsAsync(CancellationToken ct) =>
        Task.FromResult(false);
}
