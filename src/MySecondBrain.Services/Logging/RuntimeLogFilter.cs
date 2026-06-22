using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.Services.Logging;

/// <summary>
/// A wrapper around ILogger that checks ISettingsRepository at runtime
/// to determine whether a log call should be emitted based on the configured
/// minimum log level and per-category toggle switches.
///
/// DESIGN NOTE: This class uses .Result (sync-over-async) on ISettingsRepository calls
/// because ILogger.IsEnabled and ILogger.Log are synchronous methods and cannot
/// be made async. ISettingsRepository reads from an in-memory EF Core cache after first
/// load, so .Result should not deadlock in practice. If deadlocks occur, switch to a
/// cached in-memory snapshot of settings values refreshed on a background timer.
/// </summary>
public class RuntimeLogFilter<T> : ILogger<T>
{
    private readonly ILogger<T> _inner;
    private readonly ISettingsRepository _settings;

    public RuntimeLogFilter(ILogger<T> inner, ISettingsRepository settings)
    {
        _inner = inner;
        _settings = settings;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
    {
        var minLevel = GetConfiguredMinLevel();
        if (logLevel < minLevel)
            return false;

        return IsCategoryEnabled(typeof(T).Name);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    private LogLevel GetConfiguredMinLevel()
    {
        try
        {
            var saved = _settings.GetAsync("LogLevel").Result;
            return saved switch
            {
                "Debug" => LogLevel.Debug,
                "Verbose" => LogLevel.Trace,
                _ => LogLevel.Information
            };
        }
        catch
        {
            return LogLevel.Information; // Safe fallback if repository is unavailable
        }
    }

    private bool IsCategoryEnabled(string sourceContext)
    {
        var categoryKey = sourceContext switch
        {
            var s when s.Contains("LLM") || s.Contains("Provider") => "LogCategory_LLMApiCalls",
            var s when s.Contains("Tier1") || s.Contains("Hotkey") => "LogCategory_Tier1HotkeyPipeline",
            var s when s.Contains("Tier2") || s.Contains("CommandBar") => "LogCategory_Tier2CommandBar",
            var s when s.Contains("Db") || s.Contains("Database") || s.Contains("Repository") => "LogCategory_Database",
            var s when s.Contains("Wiki") || s.Contains("FileSystem") => "LogCategory_WikiFileSystem",
            var s when s.Contains("WebSocket") => "LogCategory_WebSocket",
            var s when s.Contains("Startup") || s.Contains("App") => "LogCategory_StartupShutdown",
            var s when s.Contains("System") || s.Contains("Tray") || s.Contains("Hwnd") || s.Contains("Dpi")
                => "LogCategory_SystemIntegration",
            _ => null
        };

        if (categoryKey is null)
            return true; // Uncategorized = always log if level passes

        try
        {
            var enabled = _settings.GetAsync(categoryKey).Result;
            return enabled is null || enabled == "true" || enabled == "True";
        }
        catch
        {
            return true; // Safe fallback: log enabled if repository is unavailable
        }
    }
}
