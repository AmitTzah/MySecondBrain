# Serilog External Documentation

> Retrieved via Context7 MCP — `/serilog/serilog` and `/serilog/serilog-aspnetcore`
> Feature 3: Logging Infrastructure

---

## 1. Serilog Core Setup

Source: https://github.com/serilog/serilog/blob/dev/README.md

### Basic Configuration with Console + Rolling File

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("log.txt",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true)
    .CreateLogger();

try
{
    Log.Information("Hello, {Name}!", name);
}
catch (Exception ex)
{
    Log.Error(ex, "Unhandled exception");
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

---

## 2. Enrichment — WithProperty, WithThreadId, WithMachineName

Source: https://github.com/serilog/serilog/wiki/Configuration-Basics

### Constant Property Enrichment

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console()
    .CreateLogger();
```

### Custom ThreadId Enricher

```csharp
class ThreadIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "ThreadId", Thread.CurrentThread.ManagedThreadId));
    }
}
```

Note: `Serilog.Enrichers.Thread` NuGet package provides `.Enrich.WithThreadId()` without needing a custom enricher.

### MachineName Enricher

`Serilog.Enrichers.Environment` NuGet package provides `.Enrich.WithMachineName()`.

---

## 3. Rolling File Sink with Retention

Source: https://github.com/serilog/serilog/wiki/Configuration-Basics

```csharp
.WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
```

Parameters available:
- `rollingInterval`: `RollingInterval.Day` (daily roll)
- `retainedFileCountLimit`: `30` (keep 30 files)
- `rollOnFileSizeLimit`: `true` (also roll on size)

---

## 4. Serilog.Extensions.Logging — Bridge for non-ASP.NET Apps

Source: Context7 `/serilog/serilog-aspnetcore` (adapted for WPF/console)

### Using AddSerilog() with ILoggingBuilder

For WPF/console apps using `Microsoft.Extensions.Logging`, use the `Serilog.Extensions.Logging` NuGet package (NOT `Serilog.AspNetCore`):

```csharp
using Serilog;
using Serilog.Extensions.Logging;

var serilogLogger = new LoggerConfiguration()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
#if DEBUG
    .WriteTo.Console()
#endif
    .CreateLogger();

services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(serilogLogger, dispose: true);
});
```

This replaces all default providers (Console, Debug, EventSource, etc.) and routes all `ILogger<T>` calls through Serilog.

### Cleanup on Exit

```csharp
protected override void OnExit(ExitEventArgs e)
{
    Log.CloseAndFlush();
    (_serviceProvider as IDisposable)?.Dispose();
    base.OnExit(e);
}
```

---

## 5. LoggerConfiguration Class API

Source: https://github.com/serilog/serilog/blob/dev/test/Serilog.ApprovalTests/Serilog.approved.txt

```csharp
public class LoggerConfiguration
{
    public LoggerConfiguration() { }
    public LoggerAuditSinkConfiguration AuditTo { get; }
    public LoggerDestructuringConfiguration Destructure { get; }
    public LoggerEnrichmentConfiguration Enrich { get; }
    public LoggerFilterConfiguration Filter { get; }
    public LoggerMinimumLevelConfiguration MinimumLevel { get; }
    public LoggerSettingsConfiguration ReadFrom { get; }
    public LoggerSinkConfiguration WriteTo { get; }
    public Logger CreateLogger() { }
}
```

---

## 6. NuGet Packages Required

| Package | Purpose | Version Strategy |
|---------|---------|-----------------|
| `Serilog` | Core logging engine | `*` (latest stable) |
| `Serilog.Extensions.Logging` | Bridge to M.E.L `ILoggerFactory` | `*` |
| `Serilog.Sinks.File` | Rolling file sink | `*` |
| `Serilog.Enrichers.Thread` | `WithThreadId()` enricher | `*` |
| `Serilog.Enrichers.Environment` | `WithMachineName()` enricher | `*` |

All packages use `Version="*"` per project's third-party OSS versioning strategy.

---

## 7. Minimum Level & Debug vs Release

```csharp
var config = new LoggerConfiguration()
#if DEBUG
    .MinimumLevel.Debug()
#else
    .MinimumLevel.Information()
#endif
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("AppVersion", appVersion)
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);

#if DEBUG
config = config.WriteTo.Console();
#endif

Log.Logger = config.CreateLogger();
```
