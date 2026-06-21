using System.Collections.ObjectModel;
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
    public int MaxOutputTokens { get; set; } = 4096;
    public int MaxContextWindow { get; set; } = 128000;
    public bool ThinkingEnabled { get; set; }
    public decimal? PricingInputPer1K { get; set; }
    public decimal? PricingOutputPer1K { get; set; }
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
    private readonly ILogger<SettingsViewModel> _logger;

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
        ILogger<SettingsViewModel> logger)
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
        _logger = logger;
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
    // Initialization
    // ================================================================

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await RefreshKeyListAsync();
        await RefreshAvailableApiKeysAsync();
        await RefreshModelConfigListAsync();
        await RefreshPersonaListAsync();
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
            var endpointUrl = providerType == ProviderType.OpenAICompatible
                ? CustomEndpointUrlValue
                : null;

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
            MaxOutputTokens = 4096,
            MaxContextWindow = 128000,
            ContextOverflowStrategy = "SlidingWindow",
        };
        _isNewModelConfig = true;
        SelectedModelConfigProvider = ProviderType.OpenAI;
        SelectedModelConfigApiKey = null;
        AvailableModels = [];
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
                $"Model configuration '{item.DisplayName}' is used by persona(s): {personaNames}. Delete anyway?",
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
            StatusMessage = "Model configuration deleted.";
            _logger.LogInformation("Deleted model configuration {ConfigId}", item.Id);
        }
        catch (InvalidOperationException ex)
        {
            // Repository may also throw if referenced by personas
            StatusMessage = ex.Message;
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

        try
        {
            ct.ThrowIfCancellationRequested();

            var apiKey = await _apiKeyRepo.GetByIdAsync(apiKeyId);
            if (apiKey is null)
            {
                StatusMessage = "API key not found for model fetching.";
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

            AvailableModels = new ObservableCollection<string>(models.Select(m => m.Id));
            _logger.LogDebug("Fetched {Count} models for {Provider}", models.Count, providerType);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Model fetch cancelled for {Provider}", providerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch models for {Provider}", providerType);
            StatusMessage = "Failed to fetch models. Check API key validity.";
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
}
