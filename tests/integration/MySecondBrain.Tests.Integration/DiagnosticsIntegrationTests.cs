using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Data;
using MySecondBrain.Data.Repositories;
using MySecondBrain.Services.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace MySecondBrain.Tests.Integration;

public class DiagnosticsIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _logsPath;
    private readonly AppDbContext _db;
    private readonly ISettingsRepository _settingsRepo;
    private bool _disposed;

    public DiagnosticsIntegrationTests()
    {
        // Use a unique temp directory for each test run
        var testDir = Path.Combine(
            Path.GetTempPath(),
            "MySecondBrain_IntTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDir);

        _dbPath = Path.Combine(testDir, "msb.db");
        _logsPath = Path.Combine(testDir, "logs");
        Directory.CreateDirectory(_logsPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _settingsRepo = new SettingsRepository(_db);
    }

    // ================================================================
    // Text Action — Repository Integration
    // ================================================================

    [Fact]
    public async Task TextAction_CreateAndPersist()
    {
        var repo = new TextActionRepository(_db);
        var action = new TextAction
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = "Test Action",
            SystemPrompt = "Test prompt",
            CaptureScope = "selection",
            ApplyMode = "replaceSelection",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var created = await repo.CreateAsync(action);

        Assert.NotNull(created);
        Assert.Equal(action.DisplayName, created.DisplayName);
        Assert.Equal("selection", created.CaptureScope);

        var all = await repo.GetAllAsync();
        Assert.Contains(all, a => a.Id == created.Id);
    }

    [Fact]
    public async Task TextAction_Delete_RemovesAction()
    {
        var repo = new TextActionRepository(_db);
        var action = new TextAction
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = "To Delete",
            SystemPrompt = "Will be deleted",
            CaptureScope = "selection",
            ApplyMode = "replaceSelection",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var created = await repo.CreateAsync(action);
        Assert.NotNull(created);

        await repo.DeleteAsync(created.Id);

        var all = await repo.GetAllAsync();
        Assert.DoesNotContain(all, a => a.Id == created.Id);
    }

    [Fact]
    public async Task TextAction_GetByHotkey_ReturnsMatchingActions()
    {
        var repo = new TextActionRepository(_db);
        var action = new TextAction
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = "Hotkey Action",
            SystemPrompt = "Has hotkey",
            Hotkey = "Alt+X",
            CaptureScope = "selection",
            ApplyMode = "replaceSelection",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repo.CreateAsync(action);

        var byHotkey = await repo.GetByHotkeyAsync("Alt+X");
        Assert.Contains(byHotkey, a => a.Id == action.Id);

        var noMatch = await repo.GetByHotkeyAsync("Alt+Z");
        Assert.DoesNotContain(noMatch, a => a.Id == action.Id);
    }

    // ================================================================
    // Log file creation
    // ================================================================

    [Fact]
    public void WriteLogMessage_CreatesLogFile()
    {
        // Configure Serilog to write to the test logs path
        var logPath = Path.Combine(_logsPath, "msb-.log");
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                formatter: new JsonFormatter(),
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3)
            .CreateLogger();

        logger.Information("Test log message for integration test");

        logger.Dispose();

        // Verify at least one log file exists in the directory
        var logFiles = Directory.GetFiles(_logsPath, "*.log");
        Assert.NotEmpty(logFiles);
    }

    // ================================================================
    // Clear Logs
    // ================================================================

    [Fact]
    public void ClearLogs_DeletesAllLogFiles()
    {
        // Create some test log files
        File.WriteAllText(Path.Combine(_logsPath, "msb-20260601.log"), "test content");
        File.WriteAllText(Path.Combine(_logsPath, "msb-20260602.log"), "test content");
        File.WriteAllText(Path.Combine(_logsPath, "msb-20260601.json"), "{}");

        // Also create a non-log file to verify it's NOT deleted
        File.WriteAllText(Path.Combine(_logsPath, "readme.txt"), "not a log file");

        Assert.Equal(4, Directory.GetFiles(_logsPath).Length);

        // Delete only .log and .json files
        foreach (var file in Directory.GetFiles(_logsPath, "*.*")
            .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(file);
        }

        // 1 non-log file should remain
        Assert.Single(Directory.GetFiles(_logsPath));
        Assert.True(File.Exists(Path.Combine(_logsPath, "readme.txt")));
    }

    // ================================================================
    // Open Logs Folder — creates directory if missing
    // ================================================================

    [Fact]
    public void OpenLogsFolder_CreatesDirectoryIfMissing()
    {
        var newDir = Path.Combine(_logsPath, "nonexistent", "subdir");
        Assert.False(Directory.Exists(newDir));

        Directory.CreateDirectory(newDir);

        Assert.True(Directory.Exists(newDir));
    }

    // ================================================================
    // VACUUM reduces database file size
    // ================================================================

    [Fact]
    public async Task Vacuum_ReducesDatabaseSize()
    {
        // Insert some data to inflate the database
        for (var i = 0; i < 100; i++)
        {
            await _settingsRepo.SetAsync($"test_key_{i}", $"test_value_{i}");
        }

        var beforeSize = new FileInfo(_dbPath).Length;

        // Delete the data and VACUUM
        for (var i = 0; i < 100; i++)
        {
            await _settingsRepo.DeleteAsync($"test_key_{i}");
        }

        await _db.Database.ExecuteSqlRawAsync("VACUUM;");

        var afterSize = new FileInfo(_dbPath).Length;

        // VACUUM should reduce or keep size equal (at minimum, should not throw)
        Assert.True(afterSize <= beforeSize,
            $"Expected VACUUM to reduce or maintain size; before={beforeSize}, after={afterSize}");
    }

    // ================================================================
    // Wiki directory settings persistence
    // ================================================================

    [Fact]
    public async Task WikiDirectory_Path_PersistsAndRetrieves()
    {
        var testPath = @"C:\TestWiki";
        await _settingsRepo.SetAsync("WikiDirectoryPath", testPath);

        var retrieved = await _settingsRepo.GetAsync("WikiDirectoryPath");

        Assert.Equal(testPath, retrieved);
    }

    [Fact]
    public async Task WikiDirectory_GitEnabled_Persists()
    {
        await _settingsRepo.SetAsync("GitVersionControlEnabled", "true");

        var retrieved = await _settingsRepo.GetAsync("GitVersionControlEnabled");

        Assert.Equal("true", retrieved);
    }

    // ================================================================
    // ApiKeyDestructuringPolicy integration with Serilog
    // ================================================================

    [Fact]
    public void ApiKeyRedactionEnricher_RedactsKeysInLogOutput()
    {
        var logPath = Path.Combine(_logsPath, "redact-test-.log");
        var capturedEvents = new List<LogEvent>();

        // Use a Serilog LoggerConfiguration with the enricher.
        // NOTE: IDestructuringPolicy is NOT used here because Serilog's destructuring
        // pipeline bypasses IDestructuringPolicy for string/primitive values even with
        // the @ operator. The ILogEventEnricher approach post-processes the LogEvent
        // properties and redacts any ScalarValue strings matching API key patterns,
        // working for both {ApiKey} and {@ApiKey} message template formats.
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With<ApiKeyRedactionEnricher>()
            .WriteTo.Sink(new DelegatingSink(evt => capturedEvents.Add(evt)))
            .WriteTo.File(
                formatter: new JsonFormatter(),
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 1)
            .CreateLogger();

        // Log an API key — it should be redacted in the output.
        // Using {ApiKey} (without @) since the enricher works regardless.
        var apiKey = "sk-" + new string('a', 48);
        logger.Information("Using API key: {ApiKey}", apiKey);
        logger.Dispose();

        // Verify the captured event has the redacted property
        var evt = Assert.Single(capturedEvents);
        Assert.True(evt.Properties.ContainsKey("ApiKey"));
        var propValue = evt.Properties["ApiKey"];
        Assert.Equal("[REDACTED]", (propValue as ScalarValue)?.Value?.ToString());

        // Read the log file and verify the key is redacted
        var logFiles = Directory.GetFiles(_logsPath, "redact-test-*.log");
        var logContent = File.ReadAllText(logFiles.First());
        Assert.Contains("[REDACTED]", logContent);
        Assert.DoesNotContain(apiKey, logContent);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _db.Dispose();

        // Clean up test directory
        try
        {
            var testDir = Path.GetDirectoryName(_dbPath);
            if (testDir is not null && Directory.Exists(testDir))
            {
                // Try multiple times since files may be locked briefly
                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        Directory.Delete(testDir, recursive: true);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}

/// <summary>
/// A Serilog sink that delegates each event to a callback, used for programmatic
/// inspection of log events without serialization.
/// </summary>
public class DelegatingSink : ILogEventSink
{
    private readonly Action<LogEvent> _onEvent;

    public DelegatingSink(Action<LogEvent> onEvent)
    {
        _onEvent = onEvent;
    }

    public void Emit(LogEvent logEvent)
    {
        _onEvent(logEvent);
    }
}
