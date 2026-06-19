using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.Services.Update;

public class AutoUpdaterDotNet : IUpdateChecker
{
    private readonly ILogger<AutoUpdaterDotNet> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _feedUrl;
    private string? _downloadedTempPath;

    public AutoUpdaterDotNet(ILogger<AutoUpdaterDotNet> logger)
        : this(logger, new HttpClient())
    {
    }

    // Internal constructor for testing — allows injecting a mock HttpClient and custom feed URL
    internal AutoUpdaterDotNet(
        ILogger<AutoUpdaterDotNet> logger,
        HttpClient httpClient,
        string? feedUrl = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _feedUrl = feedUrl;
    }

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 0, 0);

    public string UpdateFeedUrl =>
        _feedUrl ?? "https://updates.mysecondbrain.app/releases.xml";

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Checking for updates from {FeedUrl}", UpdateFeedUrl);

            using var response = await _httpClient.GetAsync(UpdateFeedUrl, ct);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(ct);
            var doc = XDocument.Parse(xml);

            var root = doc.Root;
            if (root is null)
            {
                _logger.LogWarning("Update feed returned empty XML");
                return new UpdateCheckResult(false, null, "Empty feed response");
            }

            var versionStr = root.Element("version")?.Value;
            var url = root.Element("url")?.Value;
            var changelog = root.Element("changelog")?.Value;
            var mandatoryStr = root.Element("mandatory")?.Value;

            if (versionStr is null || url is null)
            {
                _logger.LogWarning("Update feed XML missing required fields (version, url)");
                return new UpdateCheckResult(false, null, "Invalid feed format");
            }

            if (!Version.TryParse(versionStr, out var remoteVersion))
            {
                _logger.LogWarning("Update feed contains unparseable version '{Version}'", versionStr);
                return new UpdateCheckResult(false, null, $"Unparseable version: {versionStr}");
            }

            bool isMandatory = string.Equals(mandatoryStr, "true", StringComparison.OrdinalIgnoreCase);

            if (remoteVersion <= CurrentVersion)
            {
                _logger.LogInformation(
                    "Local version {Local} is up-to-date (remote {Remote})",
                    CurrentVersion, remoteVersion);
                return new UpdateCheckResult(false, null, null);
            }

            var update = new UpdateInfo(
                Version: remoteVersion,
                ReleaseNotes: changelog ?? string.Empty,
                ReleaseDate: DateTimeOffset.UtcNow,
                DownloadSizeBytes: 0, // Unknown until download starts
                DownloadUrl: url,
                IsMandatory: isMandatory);

            _logger.LogInformation(
                "Update available: v{Remote} (mandatory: {Mandatory})",
                remoteVersion, isMandatory);

            return new UpdateCheckResult(true, update, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Update check cancelled");
            return new UpdateCheckResult(false, null, "Cancelled");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error checking for updates from {FeedUrl}", UpdateFeedUrl);
            return new UpdateCheckResult(false, null, $"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking for updates");
            return new UpdateCheckResult(false, null, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<Stream> DownloadUpdateAsync(
        UpdateInfo update, IProgress<int>? progress, CancellationToken ct)
    {
        _logger.LogInformation("Downloading update from {Url}", update.DownloadUrl);

        using var response = await _httpClient.GetAsync(
            update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        _downloadedTempPath = Path.Combine(
            Path.GetTempPath(), $"MySecondBrain-Update-{Guid.NewGuid():N}.msix");

        await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
        var fileStream = new FileStream(
            _downloadedTempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        try
        {
            var buffer = new byte[8192];
            long bytesRead = 0;
            int bytesJustRead;

            while ((bytesJustRead = await sourceStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesJustRead), ct);
                bytesRead += bytesJustRead;

                if (totalBytes > 0 && progress is not null)
                {
                    var pct = (int)(bytesRead * 100 / totalBytes);
                    progress.Report(pct);
                }
            }

            _logger.LogInformation(
                "Update downloaded: {BytesRead} bytes to {TempPath}",
                bytesRead, _downloadedTempPath);

            // Schedule cleanup of the download temp file after a sufficient delay
            _ = CleanupTempFileWithDelayAsync(_downloadedTempPath, TimeSpan.FromMinutes(30));

            // Seek to beginning before returning so the consumer can read from start
            fileStream.Seek(0, SeekOrigin.Begin);
            return fileStream;
        }
        catch
        {
            await fileStream.DisposeAsync();
            TryDeleteTempFile(_downloadedTempPath);
            _downloadedTempPath = null;
            throw;
        }
    }

    public async Task InstallAsync(Stream updatePackage, CancellationToken ct)
    {
        // Reuse the download temp path if available to avoid duplicating the file.
        // If this method is called independently (without a prior download), save the
        // stream to a new temp file first.
        string tempPath;
        bool ownTempFile;

        if (_downloadedTempPath is not null && File.Exists(_downloadedTempPath))
        {
            tempPath = _downloadedTempPath;
            ownTempFile = false;
            _downloadedTempPath = null; // Ownership transferred to install
        }
        else
        {
            tempPath = Path.Combine(
                Path.GetTempPath(), $"MySecondBrain-Install-{Guid.NewGuid():N}.msix");
            ownTempFile = true;

            await using (var fileStream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                await updatePackage.CopyToAsync(fileStream, ct);
            }
        }

        try
        {
            _logger.LogInformation("Launching MSIX installer from {TempPath}", tempPath);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true,
                Verb = "open"
            });

            if (process is null)
            {
                _logger.LogError("Failed to start MSIX installer process for {TempPath}", tempPath);
                throw new InvalidOperationException($"Could not launch MSIX installer: {tempPath}");
            }

            // Schedule cleanup after the installer process exits + a grace period
            _ = CleanupTempFileWithDelayAsync(tempPath, TimeSpan.FromMinutes(30));
        }
        catch
        {
            if (ownTempFile)
                TryDeleteTempFile(tempPath);
            throw;
        }
    }

    private static async Task CleanupTempFileWithDelayAsync(string path, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            TryDeleteTempFile(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
