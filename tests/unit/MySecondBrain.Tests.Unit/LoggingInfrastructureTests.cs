using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySecondBrain.UI;
using Serilog;

namespace MySecondBrain.Tests.Unit;

[Collection("LoggingTests")]
public class LoggingInfrastructureTests : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly string _logDir;

    public LoggingInfrastructureTests()
    {
        // Ensure the log directory exists before configuring Serilog
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(_logDir);

        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        // Match App.OnExit() shutdown order: flush Serilog before disposing container
        Log.CloseAndFlush();
        (_provider as IDisposable)?.Dispose();

        // Best-effort cleanup of today's test log files
        var today = DateTime.Now.ToString("yyyyMMdd");
        foreach (var file in Directory.GetFiles(_logDir, $"msb-{today}*.log"))
        {
            TryDeleteFile(file);
        }
    }

    [Fact]
    public void CanResolve_ILogger_FromSerilog()
    {
        var logger = _provider.GetRequiredService<ILogger<LoggingInfrastructureTests>>();
        Assert.NotNull(logger);

        // Verify the static Log.Logger is backed by Serilog (set by App.ConfigureServices)
        Assert.Contains("Serilog", Log.Logger.GetType().FullName!);

        // Write a test log message — should not throw
        logger.LogInformation("Test log from {TestName}", nameof(CanResolve_ILogger_FromSerilog));
    }

    [Fact]
    public void LogFile_IsCreated_AtExpectedPath()
    {
        var logger = _provider.GetRequiredService<ILogger<LoggingInfrastructureTests>>();
        var testMessage = $"Unique test message: {Guid.NewGuid()}";
        logger.LogInformation(testMessage);

        // Flush buffered log entries before reading the file.
        // Called again in Dispose() — intentional; Serilog handles repeated CloseAndFlush() gracefully.
        Log.CloseAndFlush();

        var today = DateTime.Now.ToString("yyyyMMdd");
        // Serilog may create sequenced files (msb-YYYYMMDD_001.log) if the base
        // file is locked by a zombie process from a previous run. Search all matches.
        var logFiles = Directory.GetFiles(_logDir, $"msb-{today}*.log");

        Assert.True(logFiles.Length > 0,
            $"No log files found matching msb-{today}*.log in {_logDir}");

        // Find the file containing our test message, skipping locked files.
        // Store the content from the first successful read to avoid a TOCTOU double-read.
        string? matchingFile = null;
        string? matchingContent = null;
        foreach (var file in logFiles)
        {
            if (TryReadFileContent(file) is { } fileContent && fileContent.Contains(testMessage))
            {
                matchingFile = file;
                matchingContent = fileContent;
                break;
            }
        }

        Assert.True(matchingFile != null,
            $"Test message not found in any readable file among: {string.Join(", ", logFiles)}");

        Assert.Contains("ThreadId", matchingContent);
        Assert.Contains("MachineName", matchingContent);
        Assert.Contains("AppVersion", matchingContent);
    }

    /// <summary>
    /// Attempts to read a file's content, returning null if the file is locked.
    /// </summary>
    private static string? TryReadFileContent(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (IOException)
        {
            // File locked by another process — cannot read, skip it
            return null;
        }
    }

    /// <summary>
    /// Best-effort file deletion. Swallows IOExceptions from locked files.
    /// </summary>
    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (IOException)
        {
            // File locked by another process — cannot delete, skip it
        }
    }
}
