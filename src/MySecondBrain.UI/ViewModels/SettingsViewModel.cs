using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

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
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        ISettingsRepository settingsRepo,
        IThemeProvider themeProvider,
        IApiKeyRepository apiKeyRepo,
        IEncryptionService encryptionService,
        ILLMProviderService llmProviderService,
        IClipboardService clipboardService,
        IConfirmationService confirmationService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsRepo = settingsRepo;
        _themeProvider = themeProvider;
        _apiKeyRepo = apiKeyRepo;
        _encryptionService = encryptionService;
        _llmProviderService = llmProviderService;
        _clipboardService = clipboardService;
        _confirmationService = confirmationService;
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
            StatusMessage = "API key copied to clipboard.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy API key to clipboard");
            StatusMessage = "Failed to copy API key.";
        }
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
            ApiKeyInputValue = string.Empty;
            TestResultMessage = string.Empty;
            IsTestSuccess = key.IsValid;
            IsEditingKey = true;

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

    [RelayCommand]
    private void ClearStatus()
    {
        StatusMessage = string.Empty;
    }
}
