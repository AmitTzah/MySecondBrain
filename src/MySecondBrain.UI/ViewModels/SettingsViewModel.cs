using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Display wrapper for model configurations in the settings list.
/// </summary>
public partial class ModelConfigurationDisplayItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string ModelIdentifier { get; set; } = string.Empty;
    public string? ApiKeyId { get; set; }
    public double Temperature { get; set; } = 1.0;
    public int MaxOutputTokens { get; set; } = 131072;
    public int MaxContextWindow { get; set; } = 1000000;
    public bool ThinkingEnabled { get; set; }
    public decimal? PricingInputPer1K { get; set; }
    public decimal? PricingOutputPer1K { get; set; }
    public decimal? PricingCacheHitPer1K { get; set; }
    public decimal? PricingCacheMissPer1K { get; set; }
    public string ContextOverflowStrategy { get; set; } = "SlidingWindow";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ProviderLabel { get; set; } = string.Empty;

    public string Summary
    {
        get
        {
            var parts = new[] { DisplayName, ProviderLabel, ModelIdentifier };
            var nonEmpty = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            return nonEmpty.Count switch
            {
                0 => string.Empty,
                1 => nonEmpty[0],
                2 => $"{nonEmpty[0]} — {nonEmpty[1]}",
                _ => $"{nonEmpty[0]} — {nonEmpty[1]} / {nonEmpty[2]}",
            };
        }
    }
}

/// <summary>
/// Display wrapper for personas in the settings list.
/// </summary>
public partial class PersonaDisplayItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? DefaultModelConfigId { get; set; }
    public string DefaultChatMode { get; set; } = "Standard";
    public bool IsBuiltIn { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string DefaultModelConfigName { get; set; } = string.Empty;
}

/// <summary>
/// Display wrapper for settings category sidebar items.
/// </summary>
public record SettingsCategoryItem(string Icon, string Label, SettingsCategory Category);

/// <summary>
/// Display wrapper for API keys in the settings list.
/// Masks the key value and stores the encrypted value for copy operations.
/// </summary>
public partial class ApiKeyDisplayItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string ProviderLabel { get; set; } = string.Empty;

    public string MaskedValue => "••••••••";

    /// <summary>
    /// Transient flag set briefly after the key is copied to show a checkmark on the copy button.
    /// Reset after a short delay by the ViewModel.
    /// </summary>
    [ObservableProperty]
    private bool _isCopied;

    /// <summary>
    /// Computes a masked key from the decrypted plaintext.
    /// Used by the ViewModel to set the display mask after decryption.
    /// </summary>
    public static string MaskPlaintext(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || plaintext.Length <= 7)
            return "***";

        return plaintext[..3] + "..." + plaintext[^4..];
    }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IThemeProvider _themeProvider;
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILLMProviderService _llmProviderService;
    private readonly IClipboardService _clipboardService;
    private readonly IConfirmationService _confirmationService;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly IPersonaRepository _personaRepo;
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IWikiService _wikiService;
    private readonly IBackupProvider _backupProvider;
    private readonly Data.AppDbContext _db;

    /// <summary>
    /// Path to the application's logs directory under %LOCALAPPDATA%\MySecondBrain\logs\.
    /// </summary>
    private static string LogsFolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");

    /// <summary>
    /// Cancellation token source for model auto-fetch, cancelled on each new request to avoid stale results.
    /// </summary>
    private CancellationTokenSource? _fetchModelsCts;

    /// <summary>
    /// Timer used to reset the copy button checkmark after a short delay.
    /// </summary>
    private System.Windows.Threading.DispatcherTimer? _copyFeedbackTimer;

    public SettingsViewModel(
        ISettingsRepository settingsRepo,
        IThemeProvider themeProvider,
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILLMProviderService llmProviderService,
        IClipboardService clipboardService,
        IConfirmationService confirmationService,
        IModelConfigurationRepository modelConfigRepo,
        IPersonaRepository personaRepo,
        IUpdateChecker updateChecker,
        ILogger<SettingsViewModel> logger,
        IWikiService wikiService,
        IBackupProvider backupProvider,
        Data.AppDbContext db)
    {
        _settingsRepo = settingsRepo;
        _themeProvider = themeProvider;
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _llmProviderService = llmProviderService;
        _clipboardService = clipboardService;
        _confirmationService = confirmationService;
        _modelConfigRepo = modelConfigRepo;
        _personaRepo = personaRepo;
        _updateChecker = updateChecker;
        _logger = logger;
        _wikiService = wikiService;
        _backupProvider = backupProvider;
        _db = db;

        CurrentVersion = (_updateChecker.CurrentVersion ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version)
            ?.ToString() ?? "1.0.0.0";
    }

    // ================================================================
    // Category selection
    // ================================================================

    [ObservableProperty]
    private SettingsCategory _selectedSettingsCategory = SettingsCategory.Providers;

    /// <summary>
    /// Settings category display items for the sidebar ListBox.
    /// </summary>
    public IReadOnlyList<SettingsCategoryItem> CategoryItems { get; } =
    [
        new("🔑", "Providers", SettingsCategory.Providers),
        new("👤", "Profiles", SettingsCategory.Profiles),
        new("🎨", "Appearance", SettingsCategory.Appearance),
        new("🌐", "Language", SettingsCategory.Language),
        new("📝", "Wiki", SettingsCategory.Wiki),
        new("☁️", "Backup", SettingsCategory.Backup),
        new("⚡", "Text Actions", SettingsCategory.TextActions),
        new("⌨️", "Hotkeys", SettingsCategory.Hotkeys),
        new("🔧", "Tools", SettingsCategory.Tools),
        new("🔔", "Notifications", SettingsCategory.Notifications),
        new("🚀", "Startup", SettingsCategory.Startup),
        new("🔄", "Updates", SettingsCategory.Updates),
        new("💰", "Pricing", SettingsCategory.Pricing),
        new("🔒", "Security", SettingsCategory.Security),
        new("🛠️", "Maintenance", SettingsCategory.Maintenance),
        new("🔬", "Diagnostics", SettingsCategory.Diagnostics),
    ];

    partial void OnSelectedSettingsCategoryChanged(SettingsCategory value)
    {
        StatusMessage = string.Empty;
    }

    // ================================================================
    // API Key list
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<ApiKeyDisplayItem> _apiKeys = [];

    // ================================================================
    // Form state
    // ================================================================

    [ObservableProperty]
    private bool _isEditingKey;

    [ObservableProperty]
    private ApiKey? _editingApiKey;

    [ObservableProperty]
    private ProviderType _selectedProviderType = ProviderType.OpenAI;

    [ObservableProperty]
    private string _apiKeyInputValue = string.Empty;

    [ObservableProperty]
    private string _displayNameInputValue = string.Empty;

    [ObservableProperty]
    private string _customProviderNameValue = string.Empty;

    [ObservableProperty]
    private string _customEndpointUrlValue = string.Empty;

    [ObservableProperty]
    private bool _isOpenAiCompatibleSelected;

    // ================================================================
    // Test key state
    // ================================================================

    [ObservableProperty]
    private string _testResultMessage = string.Empty;

    [ObservableProperty]
    private bool _isTestSuccess;

    [ObservableProperty]
    private bool _isTesting;

    // ================================================================
    // Status bar
    // ================================================================

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ================================================================
    // Provider types for dropdown
    // ================================================================

    public IReadOnlyList<ProviderType> AllProviderTypes { get; } =
    [
        ProviderType.OpenAI,
        ProviderType.Anthropic,
        ProviderType.Google,
        ProviderType.DeepSeek,
        ProviderType.MiMo,
        ProviderType.Moonshot,
        ProviderType.Mistral,
        ProviderType.OpenAICompatible,
    ];

    // ================================================================
    // Diagnostics — Log Level
    // ================================================================

    [ObservableProperty]
    private string _logLevel = "Information";

    public IReadOnlyList<string> LogLevelOptions { get; } =
    [
        "Information",
        "Debug",
        "Verbose",
    ];

    partial void OnLogLevelChanged(string value)
    {
        _ = _settingsRepo.SetAsync("LogLevel", value);
    }

    // ================================================================
    // Diagnostics — Log Category Toggles
    // ================================================================

    /// <summary>
    /// LLM API calls to providers (default: ON).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_LLMApiCalls = true;

    /// <summary>
    /// Tier 1 hotkey pipeline for global hotkey processing (default: ON).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_Tier1HotkeyPipeline = true;

    /// <summary>
    /// Tier 2 command bar queries (default: ON).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_Tier2CommandBar = true;

    /// <summary>
    /// Database operations via EF Core (default: OFF).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_Database;

    /// <summary>
    /// Wiki filesystem watcher and indexing (default: OFF).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_WikiFileSystem;

    /// <summary>
    /// WebSocket server connections (default: OFF).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_WebSocket;

    /// <summary>
    /// Startup/shutdown lifecycle events (default: OFF).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_StartupShutdown;

    /// <summary>
    /// System integration: tray, HWND, DPI, clipboard (default: OFF).
    /// </summary>
    [ObservableProperty]
    private bool _logCategory_SystemIntegration;

    partial void OnLogCategory_LLMApiCallsChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_LLMApiCalls", value ? "true" : "false");

    partial void OnLogCategory_Tier1HotkeyPipelineChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_Tier1HotkeyPipeline", value ? "true" : "false");

    partial void OnLogCategory_Tier2CommandBarChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_Tier2CommandBar", value ? "true" : "false");

    partial void OnLogCategory_DatabaseChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_Database", value ? "true" : "false");

    partial void OnLogCategory_WikiFileSystemChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_WikiFileSystem", value ? "true" : "false");

    partial void OnLogCategory_WebSocketChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_WebSocket", value ? "true" : "false");

    partial void OnLogCategory_StartupShutdownChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_StartupShutdown", value ? "true" : "false");

    partial void OnLogCategory_SystemIntegrationChanged(bool value)
        => _ = _settingsRepo.SetAsync("LogCategory_SystemIntegration", value ? "true" : "false");

    // ================================================================
    // Appearance — AppTheme
    // ================================================================

    [ObservableProperty]
    private AppTheme _appTheme = AppTheme.Dark;

    partial void OnAppThemeChanged(AppTheme value)
    {
        _themeProvider.SetAppTheme(value);
        _ = _settingsRepo.SetAsync("AppTheme", value.ToString());
    }

    // ================================================================
    // Appearance — ChatTheme
    // ================================================================

    [ObservableProperty]
    private ChatTheme _chatTheme = ChatTheme.Classic;

    public IReadOnlyList<ChatTheme> ChatThemeOptions { get; } =
    [
        ChatTheme.Classic,
        ChatTheme.Compact,
        ChatTheme.Bubble,
    ];

    partial void OnChatThemeChanged(ChatTheme value)
    {
        _themeProvider.SetChatTheme(value);
        _ = _settingsRepo.SetAsync("ChatTheme", value.ToString());
    }

    // ================================================================
    // Appearance — Font settings
    // ================================================================

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private double _fontSize = 13.0;

    [ObservableProperty]
    private string _fontWeight = "Normal";

    /// <summary>
    /// Common font families for the font picker ComboBox.
    /// </summary>
    public IReadOnlyList<string> FontFamilyOptions { get; } =
    [
        "Segoe UI",
        "Consolas",
        "Calibri",
        "Arial",
        "Courier New",
        "Georgia",
        "Times New Roman",
        "Verdana",
        "Trebuchet MS",
        "Lucida Console",
    ];

    /// <summary>
    /// Preview text that updates live with current font settings.
    /// </summary>
    public static string FontPreviewText => "The quick brown fox jumps over the lazy dog. 0123456789";

    partial void OnFontFamilyChanged(string value)
    {
        PersistFontSettings();
    }

    partial void OnFontSizeChanged(double value)
    {
        if (value < 10.0 || value > 24.0)
        {
            // Clamp and re-set (triggers OnFontSizeChanged with clamped value)
            FontSize = Math.Clamp(value, 10.0, 24.0);
            return;
        }
        PersistFontSettings();
    }

    partial void OnFontWeightChanged(string value)
    {
        PersistFontSettings();
    }

    private void PersistFontSettings()
    {
        var wpfWeight = FontWeightStringToWpf(FontWeight);
        _themeProvider.SetFontSettings(FontFamily, FontSize, wpfWeight);
        _ = _settingsRepo.SetAsync("FontFamily", FontFamily);
        _ = _settingsRepo.SetAsync("FontSize", FontSize.ToString("F1"));
        _ = _settingsRepo.SetAsync("FontWeight", FontWeight);
    }

    /// <summary>
    /// Converts a persisted font weight string to WPF FontWeight using the
    /// standard FontWeightConverter pattern established in App.xaml.cs.
    /// </summary>
    private static System.Windows.FontWeight FontWeightStringToWpf(string weight)
    {
        return weight switch
        {
            "Bold" => System.Windows.FontWeights.Bold,
            _ => System.Windows.FontWeights.Normal,
        };
    }

    public IReadOnlyList<string> FontWeightOptions { get; } =
    [
        "Normal",
        "Bold",
    ];

    // ================================================================
    // Notifications
    // ================================================================

    [ObservableProperty]
    private bool _soundOnCompletion;

    partial void OnSoundOnCompletionChanged(bool value)
        => _ = _settingsRepo.SetAsync("SoundOnCompletion", value ? "true" : "false");

    [ObservableProperty]
    private bool _disableStreaming;

    partial void OnDisableStreamingChanged(bool value)
        => _ = _settingsRepo.SetAsync("DisableStreaming", value ? "true" : "false");

    [ObservableProperty]
    private bool _crossTabCompletionAlert = true;

    partial void OnCrossTabCompletionAlertChanged(bool value)
        => _ = _settingsRepo.SetAsync("CrossTabCompletionAlert", value ? "true" : "false");

    // ================================================================
    // Startup
    // ================================================================

    [ObservableProperty]
    private bool _launchOnWindowsStartup;

    partial void OnLaunchOnWindowsStartupChanged(bool value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null)
                return;

            if (value)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("MySecondBrain", $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue("MySecondBrain") is not null)
                    key.DeleteValue("MySecondBrain");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Windows startup registry key");
        }
    }

    [ObservableProperty]
    private bool _restoreLastSession;

    partial void OnRestoreLastSessionChanged(bool value)
        => _ = _settingsRepo.SetAsync("RestoreLastSession", value ? "true" : "false");

    [ObservableProperty]
    private bool _minimizeToTray = true;

    partial void OnMinimizeToTrayChanged(bool value)
        => _ = _settingsRepo.SetAsync("MinimizeToTray", value ? "true" : "false");

    // ================================================================
    // Updates
    // ================================================================

    [ObservableProperty]
    private string _updateCheckFrequency = "OnStartup";

    public IReadOnlyList<string> UpdateCheckFrequencyOptions { get; } =
    [
        "OnStartup",
        "Daily",
        "Weekly",
        "ManualOnly",
    ];

    partial void OnUpdateCheckFrequencyChanged(string value)
        => _ = _settingsRepo.SetAsync("UpdateCheckFrequency", value);

    /// <summary>
    /// Current application version read from the entry assembly.
    /// </summary>
    public string CurrentVersion { get; }

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Checking for updates...";

        try
        {
            var result = await _updateChecker.CheckForUpdatesAsync(CancellationToken.None);

            if (result.ErrorMessage is not null)
            {
                UpdateStatusMessage = $"Update check failed: {result.ErrorMessage}";
            }
            else if (result.UpdateAvailable && result.Update is not null)
            {
                UpdateStatusMessage =
                    $"Update {result.Update.Version} is available. " +
                    $"Release date: {result.Update.ReleaseDate:yyyy-MM-dd}. " +
                    $"{(result.Update.IsMandatory ? "This is a mandatory update." : "")}";
            }
            else
            {
                UpdateStatusMessage = "You're up to date!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            UpdateStatusMessage = "Could not check for updates. Check your internet connection.";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    // ================================================================
    // Language — AutoDetectRtl
    // ================================================================

    [ObservableProperty]
    private bool _autoDetectRtl = true;

    partial void OnAutoDetectRtlChanged(bool value)
        => _ = _settingsRepo.SetAsync("AutoDetectRtl", value ? "true" : "false");

    // ================================================================
    // Maintenance — Database compaction
    // ================================================================

    [ObservableProperty]
    private string _databaseFileSize = string.Empty;

    [ObservableProperty]
    private string _reclaimableSpace = string.Empty;

    [ObservableProperty]
    private string _lastCompaction = string.Empty;

    [ObservableProperty]
    private bool _isCompacting;

    [ObservableProperty]
    private bool _isBusy;

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    [RelayCommand]
    private async Task CompactDatabaseAsync()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "msb.db");

        if (!File.Exists(dbPath))
        {
            StatusMessage = "Database file not found.";
            return;
        }

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
            DatabaseFileSize = FormatFileSize(afterSize);
            LastCompaction = DateTimeOffset.UtcNow.ToString("g");
            await _settingsRepo.SetAsync("LastCompaction", LastCompaction);

            StatusMessage = reclaimed > 0
                ? $"Compaction complete. Reclaimed {FormatFileSize(reclaimed)}."
                : "Compaction complete. No reclaimable space.";
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

    // ================================================================
    // Wiki — Directory, indexing, git
    // ================================================================

    [ObservableProperty]
    private string _wikiDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _indexingStatus = string.Empty;

    [ObservableProperty]
    private bool _gitVersionControlEnabled;

    partial void OnGitVersionControlEnabledChanged(bool value)
        => _ = _settingsRepo.SetAsync("GitVersionControlEnabled", value ? "true" : "false");

    [RelayCommand]
    private async Task ChangeWikiDirectoryAsync()
    {
        try
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select wiki directory containing .md files",
                UseDescriptionForTitle = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var path = dialog.SelectedPath;
                if (!Directory.Exists(path))
                {
                    StatusMessage = "Selected directory does not exist.";
                    return;
                }

                WikiDirectoryPath = path;
                await _settingsRepo.SetAsync("WikiDirectoryPath", path);
                StatusMessage = $"Wiki directory changed to {path}";

                // Trigger re-index
                await ReindexWikiAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change wiki directory");
            StatusMessage = "Could not open folder picker.";
        }
    }

    [RelayCommand]
    private async Task ReindexWikiAsync()
    {
        if (string.IsNullOrEmpty(WikiDirectoryPath))
        {
            StatusMessage = "No wiki directory configured. Set one first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Indexing wiki files...";

        try
        {
            await _wikiService.IndexAllAsync(CancellationToken.None);

            var mdCount = Directory.GetFiles(WikiDirectoryPath, "*.md", SearchOption.AllDirectories).Length;
            IndexingStatus = $"✓ {mdCount} .md files indexed";
            StatusMessage = $"Wiki re-indexed: {mdCount} files found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki re-index failed");
            StatusMessage = "Wiki re-index failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ================================================================
    // Backup — Provider status, schedule, manual backup
    // ================================================================

    [ObservableProperty]
    private string _backupProviderStatus = string.Empty;

    [ObservableProperty]
    private string _backupSchedule = "Daily";

    [ObservableProperty]
    private string _lastBackupTime = string.Empty;

    partial void OnBackupScheduleChanged(string value)
        => _ = _settingsRepo.SetAsync("BackupSchedule", value);

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        IsBusy = true;
        StatusMessage = "Starting backup...";

        try
        {
            using var memoryStream = new MemoryStream();
            // Create a simple backup payload
            var writer = new StreamWriter(memoryStream);
            await writer.WriteAsync($"MySecondBrain backup - {DateTimeOffset.UtcNow:O}");
            await writer.FlushAsync();
            memoryStream.Position = 0;

            var result = await _backupProvider.UploadAsync(
                memoryStream,
                $"backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
                CancellationToken.None);

            LastBackupTime = DateTimeOffset.UtcNow.ToString("g");
            await _settingsRepo.SetAsync("LastBackupTime", LastBackupTime);
            StatusMessage = $"Backup completed. {result.SizeBytes / 1024.0:F1} KB uploaded (ID: {result.BackupId[..Math.Min(8, result.BackupId.Length)]}...)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            StatusMessage = "Backup failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ConfigureBackup()
    {
        StatusMessage = "Backup configuration coming in Feature 16.";
    }

    // ================================================================
    // Tools — Auto-approval defaults, STT provider
    // ================================================================

    [ObservableProperty]
    private string _webSearchAutoApproval = "Ask";

    [ObservableProperty]
    private string _terminalAutoApproval = "Ask";

    [ObservableProperty]
    private string _fileGenerateAutoApproval = "Ask";

    [ObservableProperty]
    private string _fileEditAutoApproval = "Ask";

    [ObservableProperty]
    private string _sttProvider = "OpenAI Whisper";

    [ObservableProperty]
    private string _sttModel = string.Empty;

    public IReadOnlyList<string> ToolApprovalOptions { get; } =
    [
        "Ask",
        "AutoApprove",
        "Disabled",
    ];

    public IReadOnlyList<string> TerminalApprovalOptions { get; } =
    [
        "Ask",
        "Disabled",
    ];

    public IReadOnlyList<string> SttProviderOptions { get; } =
    [
        "OpenAI Whisper",
        "Local Whisper",
        "Windows Speech",
    ];

    partial void OnWebSearchAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("WebSearchAutoApproval", value);

    partial void OnTerminalAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("TerminalAutoApproval", value);

    partial void OnFileGenerateAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("FileGenerateAutoApproval", value);

    partial void OnFileEditAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("FileEditAutoApproval", value);

    partial void OnSttProviderChanged(string value)
        => _ = _settingsRepo.SetAsync("SttProvider", value);

    partial void OnSttModelChanged(string value)
        => _ = _settingsRepo.SetAsync("SttModel", value);

    [RelayCommand]
    private void TestMicrophone()
    {
        StatusMessage = "Microphone test — not yet implemented.";
    }

    // ================================================================
    // Pricing — Budget limits and alerts
    // ================================================================

    [ObservableProperty]
    private decimal? _monthlyBudgetLimit;

    [ObservableProperty]
    private int _warningThreshold = 80;

    [ObservableProperty]
    private bool _blockApiOnLimit;

    partial void OnMonthlyBudgetLimitChanged(decimal? value)
        => _ = _settingsRepo.SetAsync("MonthlyBudgetLimit", value?.ToString("F2") ?? string.Empty);

    partial void OnWarningThresholdChanged(int value)
    {
        var clamped = Math.Clamp(value, 50, 100);
        if (clamped != value)
        {
            WarningThreshold = clamped;
            return;
        }
        _ = _settingsRepo.SetAsync("WarningThreshold", value.ToString());
    }

    partial void OnBlockApiOnLimitChanged(bool value)
        => _ = _settingsRepo.SetAsync("BlockApiOnLimit", value ? "true" : "false");

    // ================================================================
    // Security — Encryption, locked chats, password
    // ================================================================

    public string EncryptionStatus => "✓ API keys encrypted via Windows DPAPI";

    [ObservableProperty]
    private bool _lockedChatPasswordSet;

    [ObservableProperty]
    private bool _hideLockedChats;

    partial void OnHideLockedChatsChanged(bool value)
        => _ = _settingsRepo.SetAsync("HideLockedChats", value ? "true" : "false");

    [RelayCommand]
    private void SetGlobalPassword()
    {
        if (LockedChatPasswordSet)
        {
            StatusMessage = "Change password — not yet implemented.";
        }
        else
        {
            StatusMessage = "Set global password — placeholder dialog.";
        }
    }

    // ================================================================
    // Initialization
    // ================================================================

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await RefreshKeyListAsync();
        await RefreshAvailableApiKeysAsync();
        await RefreshModelConfigListAsync();
        await RefreshPersonaListAsync();
        await LoadDiagnosticsSettingsAsync();
        await LoadNewSettingsAsync();
        await LoadStep3SettingsAsync();
    }

    /// <summary>
    /// Loads Appearance, Notifications, Startup, and Updates settings from the repository.
    /// </summary>
    private async Task LoadNewSettingsAsync()
    {
        // Appearance — AppTheme
        var savedAppTheme = await _settingsRepo.GetAsync("AppTheme");
        if (savedAppTheme is not null && Enum.TryParse<AppTheme>(savedAppTheme, out var parsedTheme))
            AppTheme = parsedTheme;
        else
            AppTheme = _themeProvider.CurrentAppTheme;

        // Appearance — ChatTheme
        var savedChatTheme = await _settingsRepo.GetAsync("ChatTheme");
        if (savedChatTheme is not null && Enum.TryParse<ChatTheme>(savedChatTheme, out var parsedChatTheme))
            ChatTheme = parsedChatTheme;
        else
            ChatTheme = _themeProvider.CurrentChatTheme;

        // Appearance — Font settings
        var savedFontFamily = await _settingsRepo.GetAsync("FontFamily");
        if (savedFontFamily is not null)
            FontFamily = savedFontFamily;

        var savedFontSize = await _settingsRepo.GetAsync("FontSize");
        if (savedFontSize is not null && double.TryParse(savedFontSize, out var parsedSize))
            FontSize = parsedSize;

        var savedFontWeight = await _settingsRepo.GetAsync("FontWeight");
        if (savedFontWeight is not null)
            FontWeight = savedFontWeight;

        // Notifications
        var savedSound = await _settingsRepo.GetAsync("SoundOnCompletion");
        if (savedSound is not null)
            SoundOnCompletion = savedSound == "true" || savedSound == "True";

        var savedStreaming = await _settingsRepo.GetAsync("DisableStreaming");
        if (savedStreaming is not null)
            DisableStreaming = savedStreaming == "true" || savedStreaming == "True";

        var savedCrossTab = await _settingsRepo.GetAsync("CrossTabCompletionAlert");
        if (savedCrossTab is not null)
            CrossTabCompletionAlert = savedCrossTab == "true" || savedCrossTab == "True";

        // Startup
        var savedRestoreSession = await _settingsRepo.GetAsync("RestoreLastSession");
        if (savedRestoreSession is not null)
            RestoreLastSession = savedRestoreSession == "true" || savedRestoreSession == "True";

        var savedMinimizeToTray = await _settingsRepo.GetAsync("MinimizeToTray");
        if (savedMinimizeToTray is not null)
            MinimizeToTray = savedMinimizeToTray == "true" || savedMinimizeToTray == "True";

        // Startup — LaunchOnWindowsStartup reads from registry
        try
        {
            using var startupKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            LaunchOnWindowsStartup = startupKey?.GetValue("MySecondBrain") is not null;
        }
        catch
        {
            // Registry access may fail in sandboxed environments; default to false
            LaunchOnWindowsStartup = false;
        }

        // Updates
        var savedFrequency = await _settingsRepo.GetAsync("UpdateCheckFrequency");
        if (savedFrequency is not null && UpdateCheckFrequencyOptions.Contains(savedFrequency))
            UpdateCheckFrequency = savedFrequency;
    }

    /// <summary>
    /// Loads diagnostic settings (log level and category toggles) from the repository.
    /// </summary>
    private async Task LoadDiagnosticsSettingsAsync()
    {
        // Restore log level
        var savedLogLevel = await _settingsRepo.GetAsync("LogLevel");
        if (savedLogLevel is not null && LogLevelOptions.Contains(savedLogLevel))
            LogLevel = savedLogLevel;

        // Restore category toggles (defaults are set in field initializers above)
        var savedLlm = await _settingsRepo.GetAsync("LogCategory_LLMApiCalls");
        if (savedLlm is not null)
            LogCategory_LLMApiCalls = savedLlm == "true" || savedLlm == "True";

        var savedTier1 = await _settingsRepo.GetAsync("LogCategory_Tier1HotkeyPipeline");
        if (savedTier1 is not null)
            LogCategory_Tier1HotkeyPipeline = savedTier1 == "true" || savedTier1 == "True";

        var savedTier2 = await _settingsRepo.GetAsync("LogCategory_Tier2CommandBar");
        if (savedTier2 is not null)
            LogCategory_Tier2CommandBar = savedTier2 == "true" || savedTier2 == "True";

        var savedDb = await _settingsRepo.GetAsync("LogCategory_Database");
        if (savedDb is not null)
            LogCategory_Database = savedDb == "true" || savedDb == "True";

        var savedWiki = await _settingsRepo.GetAsync("LogCategory_WikiFileSystem");
        if (savedWiki is not null)
            LogCategory_WikiFileSystem = savedWiki == "true" || savedWiki == "True";

        var savedWs = await _settingsRepo.GetAsync("LogCategory_WebSocket");
        if (savedWs is not null)
            LogCategory_WebSocket = savedWs == "true" || savedWs == "True";

        var savedStartup = await _settingsRepo.GetAsync("LogCategory_StartupShutdown");
        if (savedStartup is not null)
            LogCategory_StartupShutdown = savedStartup == "true" || savedStartup == "True";

        var savedSys = await _settingsRepo.GetAsync("LogCategory_SystemIntegration");
        if (savedSys is not null)
            LogCategory_SystemIntegration = savedSys == "true" || savedSys == "True";
    }

    /// <summary>
    /// Loads Language, Maintenance, Wiki, Backup, Tools, Pricing, and Security settings.
    /// </summary>
    private async Task LoadStep3SettingsAsync()
    {
        // Language
        var savedRtl = await _settingsRepo.GetAsync("AutoDetectRtl");
        if (savedRtl is not null)
            AutoDetectRtl = savedRtl == "true" || savedRtl == "True";

        // Maintenance — database file size
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "msb.db");
        if (File.Exists(dbPath))
        {
            var fi = new FileInfo(dbPath);
            DatabaseFileSize = FormatFileSize(fi.Length);
        }
        else
        {
            DatabaseFileSize = "Database not found";
        }

        var savedLastCompaction = await _settingsRepo.GetAsync("LastCompaction");
        if (savedLastCompaction is not null)
            LastCompaction = savedLastCompaction;

        // Wiki
        var savedWikiDir = await _settingsRepo.GetAsync("WikiDirectoryPath");
        if (savedWikiDir is not null)
        {
            WikiDirectoryPath = savedWikiDir;
            if (Directory.Exists(savedWikiDir))
            {
                var mdCount = Directory.GetFiles(savedWikiDir, "*.md", SearchOption.AllDirectories).Length;
                IndexingStatus = $"✓ {mdCount} .md files indexed";
            }
        }

        var savedGit = await _settingsRepo.GetAsync("GitVersionControlEnabled");
        if (savedGit is not null)
            GitVersionControlEnabled = savedGit == "true" || savedGit == "True";

        // Backup
        try
        {
            var isValid = await _backupProvider.ValidateCredentialsAsync(CancellationToken.None);
            if (isValid)
                BackupProviderStatus = $"✓ {_backupProvider.ProviderName}: Configured";
            else
                BackupProviderStatus = $"{_backupProvider.ProviderName}: Not configured";
        }
        catch
        {
            BackupProviderStatus = $"{_backupProvider.ProviderName}: Not configured";
        }

        var savedSchedule = await _settingsRepo.GetAsync("BackupSchedule");
        if (savedSchedule is not null && (savedSchedule == "Daily" || savedSchedule == "Weekly" || savedSchedule == "ManualOnly"))
            BackupSchedule = savedSchedule;

        var savedLastBackup = await _settingsRepo.GetAsync("LastBackupTime");
        if (savedLastBackup is not null)
            LastBackupTime = savedLastBackup;

        // Tools
        var savedWebSearch = await _settingsRepo.GetAsync("WebSearchAutoApproval");
        if (savedWebSearch is not null && ToolApprovalOptions.Contains(savedWebSearch))
            WebSearchAutoApproval = savedWebSearch;

        var savedTerminal = await _settingsRepo.GetAsync("TerminalAutoApproval");
        if (savedTerminal is not null && TerminalApprovalOptions.Contains(savedTerminal))
            TerminalAutoApproval = savedTerminal;

        var savedFileGen = await _settingsRepo.GetAsync("FileGenerateAutoApproval");
        if (savedFileGen is not null && ToolApprovalOptions.Contains(savedFileGen))
            FileGenerateAutoApproval = savedFileGen;

        var savedFileEdit = await _settingsRepo.GetAsync("FileEditAutoApproval");
        if (savedFileEdit is not null && ToolApprovalOptions.Contains(savedFileEdit))
            FileEditAutoApproval = savedFileEdit;

        var savedSttProvider = await _settingsRepo.GetAsync("SttProvider");
        if (savedSttProvider is not null && SttProviderOptions.Contains(savedSttProvider))
            SttProvider = savedSttProvider;

        var savedSttModel = await _settingsRepo.GetAsync("SttModel");
        if (savedSttModel is not null)
            SttModel = savedSttModel;

        // Pricing
        var savedBudget = await _settingsRepo.GetAsync("MonthlyBudgetLimit");
        if (savedBudget is not null && decimal.TryParse(savedBudget, out var parsedBudget))
            MonthlyBudgetLimit = parsedBudget;

        var savedThreshold = await _settingsRepo.GetAsync("WarningThreshold");
        if (savedThreshold is not null && int.TryParse(savedThreshold, out var parsedThreshold))
            WarningThreshold = Math.Clamp(parsedThreshold, 50, 100);

        var savedBlockApi = await _settingsRepo.GetAsync("BlockApiOnLimit");
        if (savedBlockApi is not null)
            BlockApiOnLimit = savedBlockApi == "true" || savedBlockApi == "True";

        // Security
        var savedHideLocked = await _settingsRepo.GetAsync("HideLockedChats");
        if (savedHideLocked is not null)
            HideLockedChats = savedHideLocked == "true" || savedHideLocked == "True";
    }

    private async Task RefreshKeyListAsync()
    {
        try
        {
            var keys = await _apiKeyRepo.GetAllAsync();
            var displayItems = new List<ApiKeyDisplayItem>();

            foreach (var key in keys)
            {
                var providerLabel = key.ProviderType switch
                {
                    ProviderType.OpenAICompatible when !string.IsNullOrEmpty(key.CustomProviderName)
                        => key.CustomProviderName,
                    _ => key.ProviderType.ToString()
                };

                displayItems.Add(new ApiKeyDisplayItem
                {
                    Id = key.Id,
                    DisplayName = string.IsNullOrEmpty(key.Label) ? key.ProviderType.ToString() : key.Label,
                    ProviderType = key.ProviderType,
                    EncryptedValue = key.EncryptedValue,
                    IsValid = key.IsValid,
                    ProviderLabel = providerLabel,
                });
            }

            ApiKeys = new ObservableCollection<ApiKeyDisplayItem>(displayItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh API key list");
            StatusMessage = "Failed to load API keys.";
        }
    }

    // ================================================================
    // Commands
    // ================================================================

    [RelayCommand]
    private void AddApiKey()
    {
        EditingApiKey = null;
        SelectedProviderType = ProviderType.OpenAI;
        ApiKeyInputValue = string.Empty;
        DisplayNameInputValue = string.Empty;
        CustomProviderNameValue = string.Empty;
        CustomEndpointUrlValue = string.Empty;
        TestResultMessage = string.Empty;
        IsTestSuccess = false;
        IsOpenAiCompatibleSelected = false;
        IsEditingKey = true;
    }

    [RelayCommand]
    private async Task TestApiKeyAsync()
    {
        var plaintext = ApiKeyInputValue;
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            TestResultMessage = "Please enter an API key first.";
            IsTestSuccess = false;
            return;
        }

        IsTesting = true;
        TestResultMessage = string.Empty;
        IsTestSuccess = false;

        try
        {
            var providerType = SelectedProviderType;
            var endpointUrl = GetProviderEndpoint(providerType);

            _logger.LogDebug(
                "Testing API key for {Provider} (endpoint: {Endpoint})",
                providerType,
                endpointUrl ?? "(default)");

            var isValid = await _llmProviderService.ValidateApiKeyAsync(
                providerType, plaintext, endpointUrl, CancellationToken.None);

            if (isValid)
            {
                IsTestSuccess = true;
                TestResultMessage = "API key validated successfully.";
            }
            else
            {
                IsTestSuccess = false;
                TestResultMessage = "API key validation failed. Check the key and try again.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API key validation threw an unexpected exception");
            IsTestSuccess = false;
            TestResultMessage = $"Validation error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// Resolves the API endpoint URL for the given provider type.
    /// Returns the custom endpoint for OpenAICompatible, a well-known default
    /// for providers like DeepSeek/Mistral/Moonshot/MiMo, or null for providers
    /// with built-in endpoints (OpenAI, Anthropic, Google).
    /// </summary>
    private string? GetProviderEndpoint(ProviderType type)
    {
        return type switch
        {
            ProviderType.OpenAICompatible => CustomEndpointUrlValue,
            ProviderType.DeepSeek => "https://api.deepseek.com",
            ProviderType.Mistral => "https://api.mistral.ai",
            ProviderType.Moonshot => "https://api.moonshot.ai/v1",
            ProviderType.MiMo => "https://api.xiaomimimo.com/v1",
            _ => null // OpenAI, Anthropic, Google use built-in endpoints
        };
    }

    [RelayCommand]
    private async Task SaveApiKeyAsync()
    {
        var plaintext = ApiKeyInputValue;
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            StatusMessage = "Cannot save: API key is empty.";
            return;
        }

        try
        {
            var encrypted = _encryptionService.ProtectString(plaintext);

            if (EditingApiKey is not null)
            {
                EditingApiKey.ProviderType = SelectedProviderType;
                EditingApiKey.EncryptedValue = encrypted;
                EditingApiKey.IsValid = IsTestSuccess;
                EditingApiKey.LastTestedAt = IsTestSuccess ? DateTimeOffset.UtcNow : null;
                EditingApiKey.Label = string.IsNullOrWhiteSpace(DisplayNameInputValue)
                    ? null
                    : DisplayNameInputValue;
                EditingApiKey.CustomProviderName = string.IsNullOrWhiteSpace(CustomProviderNameValue)
                    ? null
                    : CustomProviderNameValue;
                EditingApiKey.CustomEndpointUrl = string.IsNullOrWhiteSpace(CustomEndpointUrlValue)
                    ? null
                    : CustomEndpointUrlValue;

                await _apiKeyRepo.UpdateAsync(EditingApiKey);
                _logger.LogInformation("Updated API key {KeyId}", EditingApiKey.Id);
            }
            else
            {
                var newKey = new ApiKey
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProviderType = SelectedProviderType,
                    EncryptedValue = encrypted,
                    Label = string.IsNullOrWhiteSpace(DisplayNameInputValue)
                        ? null
                        : DisplayNameInputValue,
                    CustomProviderName = string.IsNullOrWhiteSpace(CustomProviderNameValue)
                        ? null
                        : CustomProviderNameValue,
                    CustomEndpointUrl = string.IsNullOrWhiteSpace(CustomEndpointUrlValue)
                        ? null
                        : CustomEndpointUrlValue,
                    IsValid = IsTestSuccess,
                    LastTestedAt = IsTestSuccess ? DateTimeOffset.UtcNow : null,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                await _apiKeyRepo.CreateAsync(newKey);
                _logger.LogInformation("Created new API key for {Provider}", newKey.ProviderType);
            }

            await RefreshKeyListAsync();
            ClearForm();
            StatusMessage = "API key saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save API key");
            StatusMessage = $"Failed to save API key: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearForm();
    }

    [RelayCommand]
    private void CopyKey(ApiKeyDisplayItem? item)
    {
        if (item is null || string.IsNullOrEmpty(item.EncryptedValue))
            return;

        try
        {
            var decrypted = _encryptionService.UnprotectString(item.EncryptedValue);
            _clipboardService.SetText(decrypted);

            // Show checkmark on the copy button as visual feedback
            item.IsCopied = true;
            ScheduleCopyFeedbackReset(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy API key to clipboard");
        }
    }

    /// <summary>
    /// Schedules a timer to reset the IsCopied flag after 1.5 seconds
    /// so the copy button reverts from checkmark back to clipboard icon.
    /// </summary>
    private void ScheduleCopyFeedbackReset(ApiKeyDisplayItem item)
    {
        _copyFeedbackTimer?.Stop();
        _copyFeedbackTimer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(1500),
            System.Windows.Threading.DispatcherPriority.Normal,
            (_, _) =>
            {
                item.IsCopied = false;
                _copyFeedbackTimer?.Stop();
                _copyFeedbackTimer = null;
            },
            System.Windows.Threading.Dispatcher.CurrentDispatcher);
        _copyFeedbackTimer.Start();
    }

    [RelayCommand]
    private async Task DeleteApiKeyAsync(ApiKeyDisplayItem? item)
    {
        if (item is null)
            return;

        if (!_confirmationService.Confirm(
            "Delete this API key? Any Model Configurations using it will need a new key.",
            "Confirm Delete"))
            return;

        try
        {
            await _apiKeyRepo.DeleteAsync(item.Id);
            await RefreshKeyListAsync();
            StatusMessage = "API key deleted.";
            _logger.LogInformation("Deleted API key {KeyId}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete API key {KeyId}", item.Id);
            StatusMessage = "Failed to delete API key.";
        }
    }

    [RelayCommand]
    private async Task EditApiKeyAsync(ApiKeyDisplayItem? item)
    {
        if (item is null)
            return;

        try
        {
            var key = await _apiKeyRepo.GetByIdAsync(item.Id);
            if (key is null)
            {
                StatusMessage = "API key not found.";
                return;
            }

            EditingApiKey = key;
            SelectedProviderType = key.ProviderType;
            DisplayNameInputValue = key.Label ?? string.Empty;
            CustomProviderNameValue = key.CustomProviderName ?? string.Empty;
            CustomEndpointUrlValue = key.CustomEndpointUrl ?? string.Empty;
            IsOpenAiCompatibleSelected = key.ProviderType == ProviderType.OpenAICompatible;
            TestResultMessage = string.Empty;
            IsTestSuccess = key.IsValid;
            IsEditingKey = true;

            // Decrypt the stored key and pre-fill the input so the user can see/replace it
            if (!string.IsNullOrEmpty(key.EncryptedValue))
            {
                try
                {
                    var decrypted = _encryptionService.UnprotectString(key.EncryptedValue);
                    ApiKeyInputValue = decrypted;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt API key {KeyId} for edit pre-fill", key.Id);
                    ApiKeyInputValue = string.Empty;
                }
            }
            else
            {
                ApiKeyInputValue = string.Empty;
            }

            if (key.IsValid)
                TestResultMessage = "Key was valid on last test.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load API key for editing");
            StatusMessage = "Failed to load API key.";
        }
    }

    // ================================================================
    // Provider type change handler
    // ================================================================

    partial void OnSelectedProviderTypeChanged(ProviderType value)
    {
        IsOpenAiCompatibleSelected = value == ProviderType.OpenAICompatible;
        TestResultMessage = string.Empty;
        IsTestSuccess = false;
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Form title — shows "Edit API Key" when editing existing, "Add API Key" when creating new.
    /// </summary>
    public string FormTitle => EditingApiKey is null ? "Add API Key" : "Edit API Key";

    private void ClearForm()
    {
        IsEditingKey = false;
        EditingApiKey = null;
        SelectedProviderType = ProviderType.OpenAI;
        ApiKeyInputValue = string.Empty;
        DisplayNameInputValue = string.Empty;
        CustomProviderNameValue = string.Empty;
        CustomEndpointUrlValue = string.Empty;
        TestResultMessage = string.Empty;
        IsTestSuccess = false;
        IsOpenAiCompatibleSelected = false;
    }

    // ================================================================
    // Model Configuration list
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<ModelConfigurationDisplayItem> _modelConfigurations = [];

    // ================================================================
    // Persona list
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<PersonaDisplayItem> _personas = [];

    // ================================================================
    // Model Config form state
    // ================================================================

    [ObservableProperty]
    private bool _isEditingModelConfig;

    [ObservableProperty]
    private ModelConfiguration? _editingModelConfig;

    [ObservableProperty]
    private ObservableCollection<string> _availableModels = [];

    [ObservableProperty]
    private ObservableCollection<ApiKeyDisplayItem> _availableApiKeys = [];

    [ObservableProperty]
    private bool _isFetchingModels;

    [ObservableProperty]
    private string _fetchModelsErrorMessage = string.Empty;

    /// <summary>
    /// Provider tracked on the model config editing form for auto-fetch.
    /// </summary>
    [ObservableProperty]
    private ProviderType _selectedModelConfigProvider = ProviderType.OpenAI;

    /// <summary>
    /// API key tracked on the model config editing form for auto-fetch.
    /// </summary>
    [ObservableProperty]
    private ApiKeyDisplayItem? _selectedModelConfigApiKey;

    // ================================================================
    // Persona form state
    // ================================================================

    [ObservableProperty]
    private bool _isEditingPersona;

    [ObservableProperty]
    private Persona? _editingPersona;

    // ================================================================
    // Context overflow strategy options
    // ================================================================

    public IReadOnlyList<string> ContextOverflowStrategyOptions { get; } =
    [
        "SlidingWindow",
        "HardStop",
        "AutoSummarize",
    ];

    // ================================================================
    // Chat mode options
    // ================================================================

    public IReadOnlyList<string> ChatModeOptions { get; } =
    [
        "Standard",
        "TextCompletion",
    ];

    // ================================================================
    // Profile list loading
    // ================================================================

    private async Task RefreshModelConfigListAsync()
    {
        try
        {
            var configs = await _modelConfigRepo.GetAllAsync();
            var displayItems = new List<ModelConfigurationDisplayItem>();

            foreach (var config in configs)
            {
                var providerLabel = config.ProviderType switch
                {
                    ProviderType.OpenAICompatible => "Custom",
                    _ => config.ProviderType.ToString()
                };

                displayItems.Add(new ModelConfigurationDisplayItem
                {
                    Id = config.Id,
                    DisplayName = config.DisplayName,
                    ProviderType = config.ProviderType,
                    ModelIdentifier = config.ModelIdentifier,
                    ApiKeyId = config.ApiKeyId,
                    Temperature = config.Temperature,
                    MaxOutputTokens = config.MaxOutputTokens,
                    MaxContextWindow = config.MaxContextWindow,
                    ThinkingEnabled = config.ThinkingEnabled,
                    PricingInputPer1K = config.PricingInputPer1K,
                    PricingOutputPer1K = config.PricingOutputPer1K,
                    PricingCacheHitPer1K = config.PricingCacheHitPer1K,
                    PricingCacheMissPer1K = config.PricingCacheMissPer1K,
                    ContextOverflowStrategy = config.ContextOverflowStrategy,
                    CreatedAt = config.CreatedAt,
                    UpdatedAt = config.UpdatedAt,
                    ProviderLabel = providerLabel,
                });
            }

            ModelConfigurations = new ObservableCollection<ModelConfigurationDisplayItem>(displayItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh model configuration list");
            StatusMessage = "Failed to load model configurations.";
        }
    }

    private async Task RefreshPersonaListAsync()
    {
        try
        {
            var allPersonas = await _personaRepo.GetAllAsync();
            var allConfigs = await _modelConfigRepo.GetAllAsync();
            var configLookup = allConfigs.ToDictionary(c => c.Id, c => c.DisplayName);

            var displayItems = allPersonas.Select(p => new PersonaDisplayItem
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                SystemPrompt = p.SystemPrompt,
                DefaultModelConfigId = p.DefaultModelConfigId,
                DefaultChatMode = p.DefaultChatMode,
                IsBuiltIn = p.IsBuiltIn,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                DefaultModelConfigName = p.DefaultModelConfigId is not null
                    && configLookup.TryGetValue(p.DefaultModelConfigId, out var name)
                    ? name
                    : string.Empty,
            }).ToList();

            Personas = new ObservableCollection<PersonaDisplayItem>(displayItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh persona list");
            StatusMessage = "Failed to load personas.";
        }
    }

    private async Task RefreshAvailableApiKeysAsync()
    {
        try
        {
            var keys = await _apiKeyRepo.GetAllAsync();
            var displayItems = keys.Select(k => new ApiKeyDisplayItem
            {
                Id = k.Id,
                DisplayName = string.IsNullOrEmpty(k.Label) ? k.ProviderType.ToString() : k.Label,
                ProviderType = k.ProviderType,
                EncryptedValue = k.EncryptedValue,
                IsValid = k.IsValid,
                ProviderLabel = k.ProviderType switch
                {
                    ProviderType.OpenAICompatible when !string.IsNullOrEmpty(k.CustomProviderName)
                        => k.CustomProviderName,
                    _ => k.ProviderType.ToString()
                },
            }).ToList();

            AvailableApiKeys = new ObservableCollection<ApiKeyDisplayItem>(displayItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh available API keys");
        }
    }

    // ================================================================
    // Model Configuration Commands
    // ================================================================

    /// <summary>
    /// Tracks whether the currently-editing model config is new (not yet persisted).
    /// </summary>
    private bool _isNewModelConfig;

    [RelayCommand]
    private async Task AddModelConfigAsync()
    {
        EditingModelConfig = new ModelConfiguration
        {
            Temperature = 1.0,
            MaxOutputTokens = 131072,
            MaxContextWindow = 1000000,
            ContextOverflowStrategy = "SlidingWindow",
        };
        _isNewModelConfig = true;
        SelectedModelConfigProvider = ProviderType.OpenAI;
        SelectedModelConfigApiKey = null;
        AvailableModels = [];
        FetchModelsErrorMessage = string.Empty;
        IsEditingModelConfig = true;

        await RefreshAvailableApiKeysAsync();
    }

    [RelayCommand]
    private async Task SaveModelConfigAsync()
    {
        var config = EditingModelConfig;
        if (config is null)
        {
            StatusMessage = "Cannot save: no model configuration being edited.";
            return;
        }

        if (string.IsNullOrWhiteSpace(config.DisplayName))
        {
            StatusMessage = "Cannot save: display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(config.ModelIdentifier))
        {
            StatusMessage = "Cannot save: model identifier is required.";
            return;
        }

        try
        {
            // Clamp temperature to valid range
            config.Temperature = Math.Clamp(config.Temperature, 0.0, 2.0);
            config.UpdatedAt = DateTimeOffset.UtcNow;

            // Persist EndpointUrl from the selected API key for OpenAICompatible providers
            if (SelectedModelConfigApiKey is not null && SelectedModelConfigApiKey.ProviderType == ProviderType.OpenAICompatible)
            {
                var selectedKey = await _apiKeyRepo.GetByIdAsync(SelectedModelConfigApiKey.Id);
                if (selectedKey is not null)
                {
                    config.EndpointUrl = selectedKey.CustomEndpointUrl;
                }
            }

            if (_isNewModelConfig)
            {
                config.Id = Guid.NewGuid().ToString("N");
                config.CreatedAt = DateTimeOffset.UtcNow;

                await _modelConfigRepo.CreateAsync(config);
                _logger.LogInformation("Created new model configuration '{Name}'", config.DisplayName);
            }
            else
            {
                await _modelConfigRepo.UpdateAsync(config);
                _logger.LogInformation("Updated model configuration '{Name}'", config.DisplayName);
            }

            await RefreshModelConfigListAsync();
            ClearModelConfigForm();
            StatusMessage = "Model configuration saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save model configuration");
            StatusMessage = $"Failed to save model configuration: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DuplicateModelConfigAsync(ModelConfigurationDisplayItem? source)
    {
        if (source is null)
            return;

        try
        {
            var original = await _modelConfigRepo.GetByIdAsync(source.Id);
            if (original is null)
            {
                StatusMessage = "Model configuration not found.";
                return;
            }

            var copy = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = original.DisplayName + " (Copy)",
                ProviderType = original.ProviderType,
                ApiKeyId = original.ApiKeyId,
                ModelIdentifier = original.ModelIdentifier,
                Temperature = original.Temperature,
                MaxOutputTokens = original.MaxOutputTokens,
                MaxContextWindow = original.MaxContextWindow,
                ThinkingEnabled = original.ThinkingEnabled,
                ThinkingTokens = original.ThinkingTokens,
                PricingInputPer1K = original.PricingInputPer1K,
                PricingOutputPer1K = original.PricingOutputPer1K,
                ContextOverflowStrategy = original.ContextOverflowStrategy,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await _modelConfigRepo.CreateAsync(copy);
            await RefreshModelConfigListAsync();
            StatusMessage = $"Duplicated '{original.DisplayName}' as '{copy.DisplayName}'.";
            _logger.LogInformation("Duplicated model configuration '{Original}' as '{Copy}'", original.DisplayName, copy.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate model configuration");
            StatusMessage = $"Failed to duplicate model configuration: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteModelConfigAsync(ModelConfigurationDisplayItem? item)
    {
        if (item is null)
            return;

        // Check if any persona references this model config
        var referencingPersonas = Personas
            .Where(p => p.DefaultModelConfigId == item.Id)
            .ToList();

        if (referencingPersonas.Count > 0)
        {
            var personaNames = string.Join(", ", referencingPersonas.Select(p => $"'{p.DisplayName}'"));
            if (!_confirmationService.Confirm(
                $"This Model Configuration is used by {referencingPersonas.Count} Persona(s): {personaNames}. Deleting it will clear their default model configuration. Continue?",
                "Confirm Delete"))
                return;
        }
        else
        {
            if (!_confirmationService.Confirm(
                $"Delete model configuration '{item.DisplayName}'?",
                "Confirm Delete"))
                return;
        }

        try
        {
            await _modelConfigRepo.DeleteAsync(item.Id);
            await RefreshModelConfigListAsync();
            await RefreshPersonaListAsync();
            StatusMessage = "Model configuration deleted.";
            _logger.LogInformation("Deleted model configuration {ConfigId}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete model configuration {ConfigId}", item.Id);
            StatusMessage = "Failed to delete model configuration.";
        }
    }

    [RelayCommand]
    private async Task EditModelConfigAsync(ModelConfigurationDisplayItem? item)
    {
        if (item is null)
            return;

        try
        {
            var config = await _modelConfigRepo.GetByIdAsync(item.Id);
            if (config is null)
            {
                StatusMessage = "Model configuration not found.";
                return;
            }

            EditingModelConfig = config;
            _isNewModelConfig = false;
            SelectedModelConfigProvider = config.ProviderType;
            IsEditingModelConfig = true;

            // Find and set the matching API key display item
            await RefreshAvailableApiKeysAsync();
            SelectedModelConfigApiKey = AvailableApiKeys.FirstOrDefault(k => k.Id == config.ApiKeyId);

            // Auto-fetch models if we have a matching API key
            if (SelectedModelConfigApiKey is not null)
            {
                await FetchModelsForProviderAsync(config.ProviderType, SelectedModelConfigApiKey.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model configuration for editing");
            StatusMessage = "Failed to load model configuration.";
        }
    }

    [RelayCommand]
    private void CancelModelConfigEdit()
    {
        ClearModelConfigForm();
    }

    // ================================================================
    // Persona Commands
    // ================================================================

    /// <summary>
    /// Tracks whether the currently-editing persona is new (not yet persisted).
    /// </summary>
    private bool _isNewPersona;

    [RelayCommand]
    private void AddPersona()
    {
        EditingPersona = new Persona
        {
            DefaultChatMode = "Standard",
        };
        _isNewPersona = true;
        IsEditingPersona = true;
    }

    [RelayCommand]
    private async Task SavePersonaAsync()
    {
        var persona = EditingPersona;
        if (persona is null)
        {
            StatusMessage = "Cannot save: no persona being edited.";
            return;
        }

        if (string.IsNullOrWhiteSpace(persona.DisplayName))
        {
            StatusMessage = "Cannot save: display name is required.";
            return;
        }

        try
        {
            persona.UpdatedAt = DateTimeOffset.UtcNow;

            if (_isNewPersona)
            {
                persona.Id = Guid.NewGuid().ToString("N");
                persona.CreatedAt = DateTimeOffset.UtcNow;

                await _personaRepo.CreateAsync(persona);
                _logger.LogInformation("Created new persona '{Name}'", persona.DisplayName);
            }
            else
            {
                await _personaRepo.UpdateAsync(persona);
                _logger.LogInformation("Updated persona '{Name}'", persona.DisplayName);
            }

            await RefreshPersonaListAsync();
            ClearPersonaForm();
            StatusMessage = "Persona saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save persona");
            StatusMessage = $"Failed to save persona: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeletePersonaAsync(PersonaDisplayItem? item)
    {
        if (item is null)
            return;

        if (!_confirmationService.Confirm(
            $"Delete persona '{item.DisplayName}'?",
            "Confirm Delete"))
            return;

        try
        {
            await _personaRepo.DeleteAsync(item.Id);
            await RefreshPersonaListAsync();
            StatusMessage = "Persona deleted.";
            _logger.LogInformation("Deleted persona {PersonaId}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete persona {PersonaId}", item.Id);
            StatusMessage = "Failed to delete persona.";
        }
    }

    [RelayCommand]
    private async Task EditPersonaAsync(PersonaDisplayItem? item)
    {
        if (item is null)
            return;

        try
        {
            var persona = await _personaRepo.GetByIdAsync(item.Id);
            if (persona is null)
            {
                StatusMessage = "Persona not found.";
                return;
            }

            EditingPersona = persona;
            _isNewPersona = false;
            IsEditingPersona = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load persona for editing");
            StatusMessage = "Failed to load persona.";
        }
    }

    [RelayCommand]
    private void CancelPersonaEdit()
    {
        ClearPersonaForm();
    }

    // ================================================================
    // Model fetching
    // ================================================================

    [RelayCommand]
    private async Task FetchModelsAsync()
    {
        var apiKey = SelectedModelConfigApiKey;
        if (apiKey is null)
        {
            StatusMessage = "Select an API key first to fetch available models.";
            return;
        }

        await FetchModelsForProviderAsync(apiKey.ProviderType, apiKey.Id);
    }

    private async Task FetchModelsForProviderAsync(ProviderType providerType, string apiKeyId)
    {
        // Cancel any previous fetch to avoid stale results
        _fetchModelsCts?.Cancel();
        _fetchModelsCts = new CancellationTokenSource();
        var ct = _fetchModelsCts.Token;

        IsFetchingModels = true;
        AvailableModels = [];
        FetchModelsErrorMessage = string.Empty;

        try
        {
            ct.ThrowIfCancellationRequested();

            var apiKey = await _apiKeyRepo.GetByIdAsync(apiKeyId);
            if (apiKey is null)
            {
                FetchModelsErrorMessage = "API key not found for model fetching.";
                IsFetchingModels = false;
                return;
            }

            ct.ThrowIfCancellationRequested();

            var tempConfig = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                ProviderType = providerType,
                ApiKeyId = apiKeyId,
                ModelIdentifier = string.Empty,
                EndpointUrl = apiKey.CustomEndpointUrl,
            };

            var models = await _llmProviderService.ListModelsAsync(tempConfig, ct);

            // If cancelled, don't update the UI
            ct.ThrowIfCancellationRequested();

            if (models.Count == 0)
            {
                FetchModelsErrorMessage = "No models returned by the provider. Check API key validity.";
            }
            else
            {
                AvailableModels = new ObservableCollection<string>(models.Select(m => m.Id));
            }

            _logger.LogDebug("Fetched {Count} models for {Provider}", models.Count, providerType);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Model fetch cancelled for {Provider}", providerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch models for {Provider}", providerType);
            FetchModelsErrorMessage = "Failed to fetch models. Check API key validity.";
        }
        finally
        {
            IsFetchingModels = false;
        }
    }

    /// <summary>
    /// Called when the provider selection changes on the model config editing form.
    /// </summary>
    partial void OnSelectedModelConfigProviderChanged(ProviderType value)
    {
        SelectedModelConfigApiKey = null;
        AvailableModels = [];
    }

    /// <summary>
    /// Called when the API key selection changes on the model config editing form.
    /// Auto-fetches models for the selected key's provider.
    /// </summary>
    partial void OnSelectedModelConfigApiKeyChanged(ApiKeyDisplayItem? value)
    {
        if (value is not null)
        {
            _ = FetchModelsForProviderAsync(value.ProviderType, value.Id);
        }
        else
        {
            AvailableModels = [];
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    private void ClearModelConfigForm()
    {
        _fetchModelsCts?.Cancel();
        _fetchModelsCts?.Dispose();
        _fetchModelsCts = null;

        IsEditingModelConfig = false;
        EditingModelConfig = null;
        _isNewModelConfig = false;
        SelectedModelConfigProvider = ProviderType.OpenAI;
        SelectedModelConfigApiKey = null;
        AvailableModels = [];
        FetchModelsErrorMessage = string.Empty;
    }

    private void ClearPersonaForm()
    {
        IsEditingPersona = false;
        EditingPersona = null;
        _isNewPersona = false;
    }

    [RelayCommand]
    private void ClearStatus()
    {
        StatusMessage = string.Empty;
    }

    // ================================================================
    // Diagnostics Commands
    // ================================================================

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(LogsFolderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{LogsFolderPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder");
            StatusMessage = "Could not open logs folder.";
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators (RelayCommand pattern requires async Task)
    [RelayCommand]
    private async Task ClearLogsAsync()
#pragma warning restore CS1998
    {
        var confirmed = _confirmationService.Confirm(
            "Delete all log files in the logs folder? This action cannot be undone.",
            "Clear Logs");

        if (!confirmed)
            return;

        try
        {
            if (!Directory.Exists(LogsFolderPath))
            {
                StatusMessage = "All log files cleared.";
                return;
            }

            var logFiles = Directory.GetFiles(LogsFolderPath, "*.*")
                .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var failedCount = 0;
            foreach (var file in logFiles)
            {
                try { File.Delete(file); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete log file {File}", file);
                    failedCount++;
                }
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
}
