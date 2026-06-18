# Feature Reference: Logging Infrastructure

## Global & Shared Documentation

| Library | NuGet Package | Version | Purpose |
|---------|--------------|---------|---------|
| Serilog | `Serilog` | `*` | Core structured logging engine |
| Serilog.Extensions.Logging | `Serilog.Extensions.Logging` | `*` | Bridge: `AddSerilog()` on `ILoggingBuilder` |
| Serilog.Sinks.File | `Serilog.Sinks.File` | `*` | Rolling file sink |
| Serilog.Enrichers.Thread | `Serilog.Enrichers.Thread` | `*` | `WithThreadId()` enricher |
| Serilog.Enrichers.Environment | `Serilog.Enrichers.Environment` | `*` | `WithMachineName()` enricher |
| Microsoft.Extensions.Logging | `Microsoft.Extensions.Logging` | `8.0.*` | Already referenced in `Services.csproj` |

Full Serilog documentation saved to [`agent-workspace/external-docs/serilog.md`](../external-docs/serilog.md).

---

## Step-Specific Documentation

### Step 1: Replace Console/Debug Logging with Serilog

- **Library:** `Serilog.Extensions.Logging` v*
- **Import:**
  ```csharp
  using Serilog;
  using Serilog.Extensions.Logging;
  using System.Reflection;
  ```
- **Snippet — AppVersion Resolution:**
  ```csharp
  var appVersion = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0";
  ```
- **Snippet — Log File Path:**
  ```csharp
  var logPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "MySecondBrain", "logs", "msb-.log");
  ```
- **Snippet — LoggerConfiguration:**
  ```csharp
  var loggerConfig = new LoggerConfiguration()
  #if DEBUG
      .MinimumLevel.Debug()
  #else
      .MinimumLevel.Information()
  #endif
      .Enrich.WithThreadId()
      .Enrich.WithMachineName()
      .Enrich.WithProperty("AppVersion", appVersion)
      .WriteTo.File(logPath,
          rollingInterval: RollingInterval.Day,
          retainedFileCountLimit: 30);
  
  #if DEBUG
  loggerConfig = loggerConfig.WriteTo.Console();
  #endif
  
  var serilogLogger = loggerConfig.CreateLogger();
  ```
- **Snippet — Replace Logging Block in ConfigureServices:**
  ```csharp
  // REPLACES the old block:
  // services.AddLogging(builder =>
  // {
  //     builder.AddConsole();
  //     builder.AddDebug();
  //     builder.SetMinimumLevel(LogLevel.Information);
  // });
  
  services.AddLogging(builder =>
  {
      builder.ClearProviders();
      builder.AddSerilog(serilogLogger, dispose: true);
  });
  ```
- **Snippet — OnExit Cleanup:**
  ```csharp
  protected override void OnExit(ExitEventArgs e)
  {
      Log.CloseAndFlush();  // ADD THIS LINE before Dispose
      (_serviceProvider as IDisposable)?.Dispose();
      base.OnExit(e);
  }
  ```
- **Packages to add to `MySecondBrain.UI.csproj`:**
  ```xml
  <PackageReference Include="Serilog" Version="*" />
  <PackageReference Include="Serilog.Extensions.Logging" Version="*" />
  <PackageReference Include="Serilog.Sinks.File" Version="*" />
  <PackageReference Include="Serilog.Enrichers.Thread" Version="*" />
  <PackageReference Include="Serilog.Enrichers.Environment" Version="*" />
  ```

---

### Step 2: Add Unit Tests for Serilog Configuration

- **Library:** `xunit` v2.*, `Microsoft.Extensions.DependencyInjection` v8.0.* (already available)
- **Import:**
  ```csharp
  using System;
  using System.IO;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging;
  using MySecondBrain.UI;
  using Xunit;
  ```
- **Snippet — Test: CanResolve_ILogger_FromSerilog:**
  ```csharp
  [Fact]
  public void CanResolve_ILogger_FromSerilog()
  {
      var logger = _provider.GetRequiredService<ILogger<LoggingInfrastructureTests>>();
      Assert.NotNull(logger);
      
      // Verify the logger is backed by Serilog
      var loggerType = logger.GetType().FullName;
      Assert.Contains("Serilog", loggerType);
      
      // Write a test log message — should not throw
      logger.LogInformation("Test log from {TestName}", nameof(CanResolve_ILogger_FromSerilog));
  }
  ```
- **Snippet — Test: LogFile_IsCreated_AtExpectedPath:**
  ```csharp
  [Fact]
  public void LogFile_IsCreated_AtExpectedPath()
  {
      var logger = _provider.GetRequiredService<ILogger<LoggingInfrastructureTests>>();
      var testMessage = $"Unique test message: {Guid.NewGuid()}";
      logger.LogInformation(testMessage);
  
      // Flush buffered log entries before reading the file
      Log.CloseAndFlush();
  
      var today = DateTime.Now.ToString("yyyyMMdd");
      var logFile = Path.Combine(_logDir, $"msb-{today}.log");
  
      Assert.True(File.Exists(logFile), $"Log file not found at: {logFile}");
      
      var content = File.ReadAllText(logFile);
      Assert.Contains(testMessage, content);
      Assert.Contains("ThreadId", content);
      Assert.Contains("MachineName", content);
      Assert.Contains("AppVersion", content);
  }
  ```
- **Note:** Test cleanup should delete the log file created during the test. The `Log.CloseAndFlush()` must be called before reading the file to ensure buffered content is written. In test context, `Log.CloseAndFlush()` should be called in a finalizer or `IDisposable` test class.

- **Snippet — Test Class with Cleanup:**
  ```csharp
  public class LoggingInfrastructureTests : IDisposable
  {
      private readonly IServiceProvider _provider;
      private readonly string _logDir;
  
      public LoggingInfrastructureTests()
      {
          var services = new ServiceCollection();
          App.ConfigureServices(services);
          _provider = services.BuildServiceProvider(new ServiceProviderOptions
          {
              ValidateOnBuild = true
          });
          _logDir = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
              "MySecondBrain", "logs");
      }
  
      public void Dispose()
      {
          Log.CloseAndFlush();
          (_provider as IDisposable)?.Dispose();
          
          // Clean up today's test log file
          var today = DateTime.Now.ToString("yyyyMMdd");
          var logFile = Path.Combine(_logDir, $"msb-{today}.log");
          if (File.Exists(logFile))
              File.Delete(logFile);
      }
      
      // ... test methods
  }
  ```
