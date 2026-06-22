# Feature Reference: Settings, Onboarding & Diagnostics

## Global & Shared Documentation

### Key Existing Infrastructure (Summary)

| Component | Location | What It Provides |
|-----------|----------|-----------------|
| `ISettingsRepository` | `src/MySecondBrain.Core/Interfaces/ISettingsRepository.cs` | `GetAsync<T>(string key)`, `SetAsync<T>(string key, T value)`, `DeleteAsync(string key)` — typed key-value persistence |
| `SettingsRepository` | `src/MySecondBrain.Data/Repositories/SettingsRepository.cs` | EF Core implementation with JSON serialization for complex types |
| `AppSetting` entity | `src/MySecondBrain.Data/Entities/AppSetting.cs` | Table with `Key` (PK, max 256), `Value`, `ValueType`, `UpdatedAt` |
| `IThemeProvider` | `src/MySecondBrain.Core/Interfaces/IThemeProvider.cs` | `SetAppTheme()`, `SetFontSettings()`, `SetChatTheme()`, `GetChatMessageTemplate()` |
| `WpfThemeProvider` | `src/MySecondBrain.UI/Services/WpfThemeProvider.cs` | Theme/font persistence to `ISettingsRepository`, `DynamicResource` swap |
| `IEncryptionService` | `src/MySecondBrain.Core/Interfaces/IEncryptionService.cs` | `ProtectString()`, `UnprotectString()` — DPAPI |
| `IApiKeyRepository` | `src/MySecondBrain.Core/Interfaces/IApiKeyRepository.cs` | `GetAllAsync()`, `GetByIdAsync()`, `CreateAsync()`, `UpdateAsync()`, `DeleteAsync()` |
| `IPersonaRepository` | `src/MySecondBrain.Core/Interfaces/IPersonaRepository.cs` | Full CRUD for Personas |
| `IModelConfigurationRepository` | `src/MySecondBrain.Core/Interfaces/IModelConfigurationRepository.cs` | Full CRUD for Model Configurations |
| `ILLMProviderService` | `src/MySecondBrain.Core/Interfaces/ILLMProviderService.cs` | `ValidateApiKeyAsync()`, `ListModelsAsync()` |
| `IConfirmationService` | `src/MySecondBrain.Core/Interfaces/IConfirmationService.cs` | `ConfirmAsync(string title, string message)`, `ShowInfoAsync()` |
| `IUpdateChecker` | `src/MySecondBrain.Core/Interfaces/IUpdateChecker.cs` | `CheckForUpdatesAsync()`, `CurrentVersion` |
| `IGlobalHotkeyService` | `src/MySecondBrain.Core/Interfaces/IGlobalHotkeyService.cs` | `RegisterHotkey()`, `UnregisterHotkey()`, `GetRegisteredHotkeys()`, `DetectConflict()` |
| `IWikiService` | `src/MySecondBrain.Core/Interfaces/IWikiService.cs` | `IndexAllAsync()`, `InitializeGitAsync()` |
| `ISystemTrayService` | `src/MySecondBrain.Core/Interfaces/ISystemTrayService.cs` | System tray icon with context menu |
| `IClipboardService` | `src/MySecondBrain.Core/Interfaces/IClipboardService.cs` | `SetText()`, `GetText()` |
| `App.xaml.cs` | `src/MySecondBrain.UI/App.xaml.cs` | DI bootstrap, Serilog config, migration, theme restore, hotkey start |
| `SettingsViewModel` | `src/MySecondBrain.UI/ViewModels/SettingsViewModel.cs` | Already has Providers+Profiles categories, 15-item sidebar, `DataTrigger` content switching |

---

## Step-Specific Documentation

### Step 1: Diagnostics Infrastructure & Category UI

- **Library:** Serilog (already installed — `Serilog` 4.*, `Serilog.Extensions.Logging` 8.*, `Serilog.Sinks.File` 6.*)
- **API Reference:** `Serilog.Core.IDestructuringPolicy` — see snippet below
- **Snippet — ApiKeyDestructuringPolicy:**

```csharp
// Registered in LoggerConfiguration via .Destructure.With<ApiKeyDestructuringPolicy>()
public class ApiKeyDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        // Redact API keys matching common prefixes and length patterns
        if (value is string s && IsApiKey(s))
        {
            result = new ScalarValue("[REDACTED]");
            return true;
        }
        result = null;
        return false;
    }

    private static bool IsApiKey(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 20)
            return false;

        // Common API key prefixes
        return value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("sk-ant-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("AIza", StringComparison.Ordinal)
            || value.StartsWith("sk-proj-", StringComparison.OrdinalIgnoreCase);
    }
}
```

- **Snippet — Serilog LoggerConfiguration with Destructure:**

```csharp
// In App.xaml.cs ConfigureServices, add to the existing LoggerConfiguration chain:
var loggerConfig = new LoggerConfiguration()
#if DEBUG
    .MinimumLevel.Debug()
#else
    .MinimumLevel.Information()
#endif
    .Destructure.With<ApiKeyDestructuringPolicy>()  // ← NEW
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("AppVersion", appVersion)
    .WriteTo.File(
        formatter: new JsonFormatter(),
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30);
```

- **Snippet — RuntimeLogFilter pattern:**

```csharp
// Wraps ILogger<T> to filter log calls at runtime based on ISettingsRepository
// ⚠️ DESIGN NOTE: This snippet uses .Result (sync-over-async) on ISettingsRepository calls
// because ILogger<T>.IsEnabled and ILogger<T>.Log are synchronous methods and cannot be made
// async. ISettingsRepository reads from an in-memory EF Core cache after first load, so .Result
// should not deadlock in practice. If deadlocks occur, switch to a cached in-memory snapshot
// of settings values refreshed on a background timer.
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
        if (logLevel < minLevel) return false;
        return IsCategoryEnabled(typeof(T).Name);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    private LogLevel GetConfiguredMinLevel()
    {
        var saved = _settings.GetAsync("LogLevel").Result;
        return saved switch
        {
            "Debug" => LogLevel.Debug,
            "Verbose" => LogLevel.Trace,
            _ => LogLevel.Information
        };
    }

    private bool IsCategoryEnabled(string sourceContext)
    {
        var categoryKey = sourceContext switch
        {
            // Map SourceContext patterns to LogCategory_* settings keys
            var s when s.Contains("LLM") || s.Contains("Provider") => "LogCategory_LLMApiCalls",
            var s when s.Contains("Tier1") || s.Contains("Hotkey") => "LogCategory_Tier1HotkeyPipeline",
            var s when s.Contains("Tier2") || s.Contains("CommandBar") => "LogCategory_Tier2CommandBar",
            // ... etc for all 8 categories ...
            _ => null
        };

        if (categoryKey is null) return true; // Uncategorized = always log if level passes

        var enabled = _settings.GetAsync(categoryKey).Result;
        return enabled is null || enabled == "true" || enabled == "True";
    }
}
```

- **Snippet — Open Logs Folder:**

```csharp
[RelayCommand]
private void OpenLogsFolder()
{
    var logsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain", "logs");
    Directory.CreateDirectory(logsPath);
    Process.Start(new ProcessStartInfo
    {
        FileName = "explorer.exe",
        Arguments = logsPath,
        UseShellExecute = true
    });
}
```

- **Snippet — Clear Logs:**

```csharp
[RelayCommand]
private async Task ClearLogsAsync()
{
    var confirmed = await _confirmationService.ConfirmAsync(
        "Clear Logs",
        "Delete all log files in the logs folder? This action cannot be undone.");

    if (!confirmed) return;

    var logsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain", "logs");

    try
    {
        var logFiles = Directory.GetFiles(logsPath, "*.*")
            .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var failedCount = 0;
        foreach (var file in logFiles)
        {
            try { File.Delete(file); }
            catch { failedCount++; }
        }

        StatusMessage = failedCount == 0
            ? "All log files cleared."
            : $"Could not clear all log files. {failedCount} files could not be deleted.";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to clear logs");
        StatusMessage = "Could not access logs folder.";
    }
}
```

---

### Step 2: Appearance, Notifications, Startup, Updates Categories

**No specific external reference needed.** These categories use existing infrastructure:
- `IThemeProvider` for theme/font changes
- `ISettingsRepository` for persistence
- `IUpdateChecker` for update check
- Standard WPF controls: `ComboBox`, `CheckBox`, `RadioButton`, `Slider`, `ToggleButton`
- `FontWeightConverter` pattern already established in `App.xaml.cs` for font weight persistence
- Registry write for `LaunchOnWindowsStartup`: `Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)`

---

### Step 3: Language, Maintenance, Wiki, Backup, Tools, Pricing, Security Categories

**No specific external reference needed.** These categories use existing infrastructure:
- `IWikiService` for wiki operations
- `IBackupProvider` for backup status (placeholder until Feature 16)
- `AppDbContext.Database.ExecuteSqlRawAsync("VACUUM;")` for SQLite VACUUM
- `Microsoft.Win32.OpenFolderDialog` or `System.Windows.Forms.FolderBrowserDialog` for wiki directory picker
- `System.IO.FileInfo` for database file size
- Standard WPF binding patterns

- **Snippet — VACUUM implementation:**

```csharp
// In SettingsViewModel.CompactDatabaseCommand:
[RelayCommand]
private async Task CompactDatabaseAsync()
{
    var dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MySecondBrain", "msb.db");

    var beforeSize = new FileInfo(dbPath).Length;
    DatabaseFileSize = FormatFileSize(beforeSize);

    IsCompacting = true;
    StatusMessage = "Compacting database...";

    try
    {
        await _db.Database.ExecuteSqlRawAsync("VACUUM;");
        var afterSize = new FileInfo(dbPath).Length;
        var reclaimed = beforeSize - afterSize;

        ReclaimableSpace = FormatFileSize(reclaimed);
        DatabaseFileSizeAfter = FormatFileSize(afterSize);
        LastCompaction = DateTimeOffset.UtcNow.ToString("g");
        await _settingsRepo.SetAsync("LastCompaction", LastCompaction);

        StatusMessage = $"Compaction complete. Reclaimed {FormatFileSize(reclaimed)}.";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "VACUUM failed");
        StatusMessage = "Compaction failed. Check available disk space.";
    }
    finally
    {
        IsCompacting = false;
    }
}

private static string FormatFileSize(long bytes) => bytes switch
{
    < 1024 => $"{bytes} B",
    < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
    < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
    _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
};
```

---

### Step 4: Text Actions & Hotkeys Categories

**No specific external reference needed.** These categories use:
- Existing `TextAction` entity in `src/MySecondBrain.Data/Entities/TextAction.cs`
- Existing seed data (10 built-in TextActions) in `AppDbContext.OnModelCreating`
- Key recorder pattern: a WPF `Popup` or `Border` overlay that listens to `KeyDown` events, captures `Key.SystemKey` + `Keyboard.Modifiers`, displays combo string
- `EnumMatchConverter` pattern (already exists at `src/MySecondBrain.UI/Converters/EnumMatchConverter.cs`) for RadioButton → enum binding
- `CaptureScope` and `ApplyMode` are stored as strings (not integer enums), matching the existing string-based enum convention per [database.md §3.3](agent-workspace/knowledge/database.md#33-string-based-enums-for-flexibility)

---

### Step 5: Onboarding Wizard — Full Implementation

**No specific external reference needed.** The wizard is a pure WPF implementation using:
- `DataTrigger` on `CurrentStep` (int) for content switching — same pattern as Settings category switching
- `ObservableObject` + `[ObservableProperty]` + `[RelayCommand]` (CommunityToolkit.Mvvm)
- `PasswordBox` + code-behind bridge (same pattern as `SettingsView.xaml.cs`)
- `System.Windows.Forms.FolderBrowserDialog` for folder picker
- `WeakReferenceMessenger` for cross-window communication (wizard → main window)
- Persistence via `ISettingsRepository` for step completion flags

---

### Step 6: First-Launch Detection, Startup Integration & Polish

- **Library:** `CommunityToolkit.Mvvm` (already installed) — `WeakReferenceMessenger` for cross-window messaging
- **Pattern:** `OnboardingWizardWindow` as a standalone WPF `Window` (not `UserControl`)
- **Snippet — First-Launch Detection in App.xaml.cs `OnStartup`:**

```csharp
// After DI build, migration, and theme restore:
var settings = _serviceProvider.GetRequiredService<ISettingsRepository>();
var onboardingCompleted = await settings.GetAsync("Onboarding_Completed");

if (onboardingCompleted != "true")
{
    // First launch or incomplete onboarding — show wizard
    var wizardVm = _serviceProvider.GetRequiredService<OnboardingWizardViewModel>();
    var wizardWindow = new OnboardingWizardWindow
    {
        DataContext = wizardVm,
        Owner = null // Standalone — no parent window yet
    };

    // When wizard completes, show MainWindow
    wizardVm.LaunchStudioRequested += () =>
    {
        wizardWindow.Dispatcher.Invoke(() =>
        {
            wizardWindow.Close();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        });
    };

    wizardWindow.Show();
    // DO NOT show MainWindow yet — wizard is the only window
}
else
{
    // Onboarding complete — normal launch
    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
    mainWindow.Show();
}
```

- **Snippet — Re-run Onboarding from Settings:**

```csharp
// In SettingsViewModel:
[RelayCommand]
private void ReRunOnboarding()
{
    WeakReferenceMessenger.Default.Send(new ReRunOnboardingMessage());
}

// In App.xaml.cs (registered in OnStartup):
WeakReferenceMessenger.Default.Register<ReRunOnboardingMessage>(this, (r, m) =>
{
    mainWindow.Dispatcher.Invoke(() =>
    {
        var wizardVm = _serviceProvider.GetRequiredService<OnboardingWizardViewModel>();
        var wizardWindow = new OnboardingWizardWindow
        {
            DataContext = wizardVm,
            Owner = mainWindow
        };
        wizardWindow.ShowDialog(); // Modal — blocks Settings until wizard completes
    });
});
```

- **Snippet — Messenger Message Records (add to Core/Models/ or ViewModels/):**

```csharp
public record LaunchStudioMessage;
public record ReRunOnboardingMessage;
```
