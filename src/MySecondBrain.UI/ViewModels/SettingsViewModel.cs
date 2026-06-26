using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;

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

    [ObservableProperty]
    private bool _isCopied;

    public static string MaskPlaintext(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || plaintext.Length <= 7)
            return "***";
        return plaintext[..3] + "..." + plaintext[^4..];
    }
}

/// <summary>
/// Display wrapper for text actions in the settings list.
/// </summary>
public partial class TextActionDisplayItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string? ModelConfigId { get; set; }
    public string ModelConfigName { get; set; } = string.Empty;
    public string? Hotkey { get; set; }
    public string CaptureScope { get; set; } = "selection";
    public string ApplyMode { get; set; } = "replaceSelection";
    public bool IsBuiltIn { get; set; }

    public string TruncatedSystemPrompt => SystemPrompt.Length > 80
        ? SystemPrompt[..77] + "..."
        : SystemPrompt;

    public string CaptureScopeBadges
    {
        get
        {
            var scopes = CaptureScope.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return string.Join(", ", scopes.Select(MapCaptureScope));
        }
    }

    public string ApplyModeLabel => ApplyMode switch
    {
        "replaceSelection" => "Replace Selection",
        "insertAtCursor" => "Insert at Cursor",
        "replaceFocusedElement" => "Replace Element",
        "appendToFocusedElement" => "Append to Element",
        "prependToFocusedElement" => "Prepend to Element",
        "clipboardOnly" => "Clipboard Only",
        "showOnly" => "Show Only",
        _ => ApplyMode
    };

    private static string MapCaptureScope(string scope) => scope switch
    {
        "selection" => "Selection",
        "focusedElement" => "Focused Element",
        "surroundingContext" => "Surrounding Context",
        "fullDocument" => "Full Document",
        "screenshot" => "Screenshot",
        _ => scope
    };
}

/// <summary>
/// Display wrapper for hotkey assignments in the settings list.
/// </summary>
public partial class HotkeyAssignmentDisplayItem : ObservableObject
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Source { get; set; } = "TextAction";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureScopeBadges))]
    private string _captureScope = "selection";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyModeLabel))]
    private string _applyMode = "replaceSelection";
    [ObservableProperty]
    private string? _hotkey;

    [ObservableProperty]
    private bool _isRecording;

    public string ApplyModeLabel => ApplyMode switch
    {
        "replaceSelection" => "Replace Selection",
        "insertAtCursor" => "Insert at Cursor",
        "replaceFocusedElement" => "Replace Element",
        "appendToFocusedElement" => "Append to Element",
        "prependToFocusedElement" => "Prepend to Element",
        "clipboardOnly" => "Clipboard Only",
        "showOnly" => "Show Only",
        _ => ApplyMode
    };

    public string CaptureScopeBadges
    {
        get
        {
            var scopes = CaptureScope.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return string.Join(", ", scopes.Select(MapCaptureScope));
        }
    }

    private static string MapCaptureScope(string scope) => scope switch
    {
        "selection" => "Selection",
        "focusedElement" => "Focused Element",
        "surroundingContext" => "Surrounding Context",
        "fullDocument" => "Full Document",
        "screenshot" => "Screenshot",
        _ => scope
    };
}

/// <summary>
/// Main SettingsViewModel — core initialization, navigation, and shared helpers.
/// API keys, profiles, appearance, and all other category logic are in partial files under Settings/.
/// </summary>
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
    private readonly ITextActionRepository _textActionRepo;
    private readonly AppDbContext _db;

    private bool _suppressFontPersistence;

    private static string LogsFolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");

    private CancellationTokenSource? _fetchModelsCts;
    private DispatcherTimer? _copyFeedbackTimer;
    private bool _isInitialized;

    /// <summary>
    /// Static holder for a pending navigation target set by MainWindowViewModel
    /// before navigating to Settings. Read and cleared in InitializeAsync.
    /// </summary>
    private static SettingsCategory? PendingNavigationCategory { get; set; }

    /// <summary>
    /// Called by MainWindowViewModel before navigating to Settings to request
    /// a specific category be pre-selected on load.
    /// </summary>
    public static void SetPendingNavigationCategory(SettingsCategory category)
    {
        PendingNavigationCategory = category;
    }

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
        ITextActionRepository textActionRepo,
        AppDbContext db)
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
        _textActionRepo = textActionRepo;
        _db = db;

        CurrentVersion = (_updateChecker.CurrentVersion ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version)
            ?.ToString() ?? "1.0.0.0";

        WeakReferenceMessenger.Default.Register<RefreshApiKeysMessage>(this, async (_, _) =>
        {
            if (!_isInitialized)
            {
                _logger.LogDebug("[DIAG] RefreshApiKeysMessage received before InitializeAsync completed — skipping redundant refresh");
                return;
            }
            _logger.LogDebug("[DIAG] RefreshApiKeysMessage received by #{VM} — reloading API key list", this.GetHashCode());
            await RefreshKeyListAsync();
            await RefreshAvailableApiKeysAsync();
        });

        _logger.LogDebug("[DIAG] SettingsViewModel #{HashCode} created + registered for RefreshApiKeysMessage", this.GetHashCode());
    }

    // ================================================================
    // Category selection
    // ================================================================

    [ObservableProperty]
    private SettingsCategory _selectedSettingsCategory = SettingsCategory.Providers;

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
        new("ℹ️", "System Info", SettingsCategory.SystemInfo),
    ];

    partial void OnSelectedSettingsCategoryChanged(SettingsCategory value)
    {
        StatusMessage = string.Empty;

        // Auto-load data location sizes when user switches to System Info category
        if (value == SettingsCategory.SystemInfo)
        {
            _ = LoadDataLocationsAsync();
        }
    }

    // ================================================================
    // Status bar
    // ================================================================

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ================================================================
    // System Info — App Data Locations
    // ================================================================

    private const string AppDataFolder = "MySecondBrain";
    private const string LocalAppDataEnvVar = "%LOCALAPPDATA%";

    /// <summary>
    /// Returns the expanded %LOCALAPPDATA%/MySecondBrain path.
    /// </summary>
    private static string AppDataPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolder);

    [ObservableProperty]
    private ObservableCollection<DataLocationInfo> _dataLocations = [];

    /// <summary>
    /// Builds the list of data locations and kicks off size calculation.
    /// </summary>
    [RelayCommand]
    private async Task LoadDataLocationsAsync()
    {
        var appData = AppDataPath;
        var wikiDir = await _settingsRepo.GetAsync("WikiDirectoryPath");
        var backupDir = await _settingsRepo.GetAsync("BackupDirectory");

        var items = new List<DataLocationInfo>();

        // 1. Main database
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\msb.db",
            Path.Combine(appData, "msb.db"),
            "Main SQLite database — all user data (chats, messages, personas, settings, API keys)",
            LocationEditability.AppManaged, _logger)
        { IsDirectory = false, AutomationKey = "MsbDb" });

        // 2. WAL file
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\msb.db-wal",
            Path.Combine(appData, "msb.db-wal"),
            "SQLite Write-Ahead Log — temporary journal file",
            LocationEditability.AppManaged, _logger)
        { IsDirectory = false, AutomationKey = "MsbDbWal" });

        // 3. SHM file
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\msb.db-shm",
            Path.Combine(appData, "msb.db-shm"),
            "SQLite Shared Memory — temporary index file",
            LocationEditability.AppManaged, _logger)
        { IsDirectory = false, AutomationKey = "MsbDbShm" });

        // 4. Logs directory
        var logsDir = Path.Combine(appData, "logs");
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\logs\",
            logsDir,
            "Serilog rolling JSON log files — one file per day, retained 30 days",
            LocationEditability.Caution, _logger)
        { IsDirectory = true, AutomationKey = "LogsDir" });

        // 5. Workspace directory
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\workspace\{{chat-id}}",
            Path.Combine(appData, "workspace"),
            "Per-chat sandbox directories for bash/code execution",
            LocationEditability.AppManaged, _logger)
        { IsDirectory = true, AutomationKey = "WorkspaceDir" });

        // 6. Artifacts directory
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\artifacts\{{chat-id}}",
            Path.Combine(appData, "artifacts"),
            "AI-generated files surfaced via present_files — per-chat subdirectory",
            LocationEditability.Caution, _logger)
        { IsDirectory = true, AutomationKey = "ArtifactsDir" });

        // 7. Skills directory
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\skills\",
            Path.Combine(appData, "skills"),
            "User-added community Agent Skills — add by copying folders here",
            LocationEditability.UserEditable, _logger)
        { IsDirectory = true, AutomationKey = "SkillsDir" });

        // 8. Settings file
        items.Add(new DataLocationInfo(
            $@"{LocalAppDataEnvVar}\{AppDataFolder}\settings.json",
            Path.Combine(appData, "settings.json"),
            "Application settings (persisted preferences)",
            LocationEditability.AppManaged, _logger)
        { IsDirectory = false, AutomationKey = "SettingsFile" });

        // 9. Wiki directory (user-configured)
        if (!string.IsNullOrEmpty(wikiDir))
        {
            items.Add(new DataLocationInfo(
                wikiDir,
                wikiDir,
                "Personal wiki .md files — user-configured directory",
                LocationEditability.UserEditable, _logger)
            { IsDirectory = true, AutomationKey = "WikiDir" });
        }
        else
        {
            items.Add(new DataLocationInfo(
                "[Not configured]",
                null,
                "Wiki directory not configured — set in Settings → Wiki",
                LocationEditability.UserEditable, _logger)
            { IsDirectory = true, AutomationKey = "WikiDir" });
        }

        // 10. Backup directory (user-configured)
        if (!string.IsNullOrEmpty(backupDir))
        {
            items.Add(new DataLocationInfo(
                backupDir,
                backupDir,
                "Backup destination — user-configured directory or cloud storage",
                LocationEditability.UserEditable, _logger)
            { IsDirectory = true, AutomationKey = "BackupDir" });
        }
        else
        {
            items.Add(new DataLocationInfo(
                "[Not configured]",
                null,
                "Backup not configured — set in Settings → Backup",
                LocationEditability.UserEditable, _logger)
            { IsDirectory = true, AutomationKey = "BackupDir" });
        }

        DataLocations = new ObservableCollection<DataLocationInfo>(items);

        // Kick off parallel size calculations (fire-and-forget)
        foreach (var loc in DataLocations)
        {
            _ = loc.CalculateSizeAsync();
        }
    }

    /// <summary>
    /// Recalculates disk sizes for all data locations (manual refresh).
    /// </summary>
    [RelayCommand]
    private async Task RecalculateDataLocationsAsync()
    {
        var tasks = new List<Task>();
        foreach (var loc in DataLocations)
        {
            loc.SizeOnDisk = "Calculating…";
            loc.IsSizeCalculated = false;
            tasks.Add(loc.CalculateSizeAsync());
        }
        await Task.WhenAll(tasks);
    }

    // ================================================================
    // Initialization
    // ================================================================

    [RelayCommand]
    private async Task InitializeAsync()
    {
        // If a pending category was set (from MainWindow "?" icon → App Data Locations),
        // use that; otherwise default to Providers.
        SelectedSettingsCategory = PendingNavigationCategory ?? SettingsCategory.Providers;
        PendingNavigationCategory = null;

        await RefreshKeyListAsync();
        await RefreshAvailableApiKeysAsync();
        await RefreshModelConfigListAsync();
        await RefreshPersonaListAsync();
        await LoadDiagnosticsSettingsAsync();
        await LoadNewSettingsAsync();
        await LoadStep3SettingsAsync();
        await RefreshTextActionListAsync();
        await RefreshHotkeyAssignmentsAsync();

        _isInitialized = true;
    }

    private async Task LoadNewSettingsAsync()
    {
        var savedAppTheme = await _settingsRepo.GetAsync("AppTheme");
        if (savedAppTheme is not null && Enum.TryParse<AppTheme>(savedAppTheme, out var parsedTheme))
            AppTheme = parsedTheme;
        else
            AppTheme = _themeProvider.CurrentAppTheme;

        var savedChatTheme = await _settingsRepo.GetAsync("ChatTheme");
        if (savedChatTheme is not null && Enum.TryParse<ChatTheme>(savedChatTheme, out var parsedChatTheme))
            ChatTheme = parsedChatTheme;
        else
            ChatTheme = _themeProvider.CurrentChatTheme;

        _suppressFontPersistence = true;
        try
        {
            var savedFontFamily = await _settingsRepo.GetAsync("FontFamily");
            if (savedFontFamily is not null) FontFamily = savedFontFamily;
            var savedFontSize = await _settingsRepo.GetAsync("FontSize");
            if (savedFontSize is not null && double.TryParse(savedFontSize, out var parsedSize))
                FontSize = parsedSize;
            var savedFontWeight = await _settingsRepo.GetAsync("FontWeight");
            if (savedFontWeight is not null) FontWeight = savedFontWeight;
        }
        finally { _suppressFontPersistence = false; }
        PersistFontSettings();

        var savedSound = await _settingsRepo.GetAsync("SoundOnCompletion");
        if (savedSound is not null) SoundOnCompletion = savedSound == "true" || savedSound == "True";
        var savedStreaming = await _settingsRepo.GetAsync("DisableStreaming");
        if (savedStreaming is not null) DisableStreaming = savedStreaming == "true" || savedStreaming == "True";
        var savedCrossTab = await _settingsRepo.GetAsync("CrossTabCompletionAlert");
        if (savedCrossTab is not null) CrossTabCompletionAlert = savedCrossTab == "true" || savedCrossTab == "True";
        var savedRestoreSession = await _settingsRepo.GetAsync("RestoreLastSession");
        if (savedRestoreSession is not null) RestoreLastSession = savedRestoreSession == "true" || savedRestoreSession == "True";
        var savedMinimizeToTray = await _settingsRepo.GetAsync("MinimizeToTray");
        if (savedMinimizeToTray is not null) MinimizeToTray = savedMinimizeToTray == "true" || savedMinimizeToTray == "True";
        try
        {
            using var startupKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            LaunchOnWindowsStartup = startupKey?.GetValue("MySecondBrain") is not null;
        }
        catch { LaunchOnWindowsStartup = false; }
        var savedFrequency = await _settingsRepo.GetAsync("UpdateCheckFrequency");
        if (savedFrequency is not null && UpdateCheckFrequencyOptions.Contains(savedFrequency))
            UpdateCheckFrequency = savedFrequency;
    }

    private async Task LoadDiagnosticsSettingsAsync()
    {
        var savedLogLevel = await _settingsRepo.GetAsync("LogLevel");
        if (savedLogLevel is not null && LogLevelOptions.Contains(savedLogLevel))
            LogLevel = savedLogLevel;
        var savedLlm = await _settingsRepo.GetAsync("LogCategory_LLMApiCalls");
        if (savedLlm is not null) LogCategory_LLMApiCalls = savedLlm == "true" || savedLlm == "True";
        var savedTier1 = await _settingsRepo.GetAsync("LogCategory_Tier1HotkeyPipeline");
        if (savedTier1 is not null) LogCategory_Tier1HotkeyPipeline = savedTier1 == "true" || savedTier1 == "True";
        var savedTier2 = await _settingsRepo.GetAsync("LogCategory_Tier2CommandBar");
        if (savedTier2 is not null) LogCategory_Tier2CommandBar = savedTier2 == "true" || savedTier2 == "True";
        var savedDb = await _settingsRepo.GetAsync("LogCategory_Database");
        if (savedDb is not null) LogCategory_Database = savedDb == "true" || savedDb == "True";
        var savedWiki = await _settingsRepo.GetAsync("LogCategory_WikiFileSystem");
        if (savedWiki is not null) LogCategory_WikiFileSystem = savedWiki == "true" || savedWiki == "True";
        var savedWs = await _settingsRepo.GetAsync("LogCategory_WebSocket");
        if (savedWs is not null) LogCategory_WebSocket = savedWs == "true" || savedWs == "True";
        var savedStartup = await _settingsRepo.GetAsync("LogCategory_StartupShutdown");
        if (savedStartup is not null) LogCategory_StartupShutdown = savedStartup == "true" || savedStartup == "True";
        var savedSys = await _settingsRepo.GetAsync("LogCategory_SystemIntegration");
        if (savedSys is not null) LogCategory_SystemIntegration = savedSys == "true" || savedSys == "True";
    }

    private async Task LoadStep3SettingsAsync()
    {
        var savedRtl = await _settingsRepo.GetAsync("AutoDetectRtl");
        if (savedRtl is not null) AutoDetectRtl = savedRtl == "true" || savedRtl == "True";
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "msb.db");
        if (File.Exists(dbPath))
        {
            var fi = new FileInfo(dbPath);
            DatabaseFileSize = FormatFileSize(fi.Length);
        }
        else { DatabaseFileSize = "Database not found"; }
        var savedLastCompaction = await _settingsRepo.GetAsync("LastCompaction");
        if (savedLastCompaction is not null) LastCompaction = savedLastCompaction;
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
        if (savedGit is not null) GitVersionControlEnabled = savedGit == "true" || savedGit == "True";
        if (!string.IsNullOrEmpty(WikiDirectoryPath) && GitVersionControlEnabled)
        {
            var gitDir = Path.Combine(WikiDirectoryPath, ".git");
            GitStatusMessage = Directory.Exists(gitDir)
                ? "✓ Git repository detected"
                : "Git repository will be initialized on next re-index.";
        }
        try
        {
            var isValid = await _backupProvider.ValidateCredentialsAsync(CancellationToken.None);
            BackupProviderStatus = isValid ? $"✓ {_backupProvider.ProviderName}: Configured" : $"{_backupProvider.ProviderName}: Not configured";
        }
        catch { BackupProviderStatus = $"{_backupProvider.ProviderName}: Not configured"; }
        var savedSchedule = await _settingsRepo.GetAsync("BackupSchedule");
        if (savedSchedule is not null && (savedSchedule == "Daily" || savedSchedule == "Weekly" || savedSchedule == "ManualOnly"))
            BackupSchedule = savedSchedule;
        var savedLastBackup = await _settingsRepo.GetAsync("LastBackupTime");
        if (savedLastBackup is not null) LastBackupTime = savedLastBackup;
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
        if (savedSttModel is not null) SttModel = savedSttModel;
        var savedBudget = await _settingsRepo.GetAsync("MonthlyBudgetLimit");
        if (savedBudget is not null && decimal.TryParse(savedBudget, out var parsedBudget))
            MonthlyBudgetLimit = parsedBudget;
        var savedThreshold = await _settingsRepo.GetAsync("WarningThreshold");
        if (savedThreshold is not null && int.TryParse(savedThreshold, out var parsedThreshold))
            WarningThreshold = Math.Clamp(parsedThreshold, 50, 100);
        var savedBlockApi = await _settingsRepo.GetAsync("BlockApiOnLimit");
        if (savedBlockApi is not null) BlockApiOnLimit = savedBlockApi == "true" || savedBlockApi == "True";
        var savedHideLocked = await _settingsRepo.GetAsync("HideLockedChats");
        if (savedHideLocked is not null) HideLockedChats = savedHideLocked == "true" || savedHideLocked == "True";
    }

    // ================================================================
    // Re-run Onboarding & shared commands
    // ================================================================

    [RelayCommand]
    private void ReRunOnboarding()
    {
        WeakReferenceMessenger.Default.Send(new ReRunOnboardingMessage());
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
            item.IsCopied = true;
            ScheduleCopyFeedbackReset(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy API key to clipboard");
        }
    }

    private void ScheduleCopyFeedbackReset(ApiKeyDisplayItem item)
    {
        _copyFeedbackTimer?.Stop();
        _copyFeedbackTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(1500),
            DispatcherPriority.Normal,
            (_, _) =>
            {
                item.IsCopied = false;
                _copyFeedbackTimer?.Stop();
                _copyFeedbackTimer = null;
            },
            Dispatcher.CurrentDispatcher);
        _copyFeedbackTimer.Start();
    }

    [RelayCommand]
    private void ClearStatus()
    {
        StatusMessage = string.Empty;
    }
}
