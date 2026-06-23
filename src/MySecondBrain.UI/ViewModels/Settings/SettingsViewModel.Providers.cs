using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// API Key management: list, test, save, edit, delete, copy commands.
/// </summary>
public partial class SettingsViewModel
{
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
            _ => null
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

    partial void OnSelectedProviderTypeChanged(ProviderType value)
    {
        IsOpenAiCompatibleSelected = value == ProviderType.OpenAICompatible;
        TestResultMessage = string.Empty;
        IsTestSuccess = false;
    }

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
}
