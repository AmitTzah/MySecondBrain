using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Step 0 — API Keys: add, test, and manage provider API keys.
/// </summary>
public partial class OnboardingWizardViewModel
{
    [ObservableProperty]
    private ProviderType _selectedApiKeyProvider = ProviderType.OpenAI;

    [ObservableProperty]
    private string _apiKeyInputValue = string.Empty;

    [ObservableProperty]
    private string _apiKeyDisplayNameInputValue = string.Empty;

    [ObservableProperty]
    private bool _isTestingAll;

    [ObservableProperty]
    private ObservableCollection<WizardApiKeyItem> _wizardApiKeys = [];

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

    [RelayCommand]
    private void AddApiKey()
    {
        var plaintext = ApiKeyInputValue;
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            _confirmationService.Confirm("Please enter an API key.", "Validation");
            return;
        }

        // Check for duplicate
        if (WizardApiKeys.Any(k =>
            k.PlaintextKey == plaintext ||
            (k.ProviderType == SelectedApiKeyProvider && k.PlaintextKey == plaintext)))
        {
            _confirmationService.Confirm("This API key was already added.", "Duplicate Key");
            return;
        }

        var item = new WizardApiKeyItem
        {
            Id = Guid.NewGuid().ToString("N"),
            ProviderType = SelectedApiKeyProvider,
            PlaintextKey = plaintext,
            DisplayName = string.IsNullOrWhiteSpace(ApiKeyDisplayNameInputValue)
                ? null
                : ApiKeyDisplayNameInputValue,
            Status = "Not tested",
        };

        WizardApiKeys.Add(item);
        ApiKeyInputValue = string.Empty;
        ApiKeyDisplayNameInputValue = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveApiKeyAsync(WizardApiKeyItem? item)
    {
        if (item is null) return;
        if (!_confirmationService.Confirm("Remove this API key?", "Confirm"))
            return;
        WizardApiKeys.Remove(item);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task TestApiKeyAsync(WizardApiKeyItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.PlaintextKey)) return;

        item.IsTesting = true;
        item.Status = "Testing...";

        try
        {
            var endpointUrl = GetProviderEndpoint(item.ProviderType);
            var isValid = await _llmProviderService.ValidateApiKeyAsync(
                item.ProviderType, item.PlaintextKey, endpointUrl, CancellationToken.None);

            item.IsValid = isValid;
            item.IsTested = true;
            item.Status = isValid ? "✓ Validated" : "✕ Invalid";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API key test failed for {Provider}", item.ProviderType);
            item.IsTested = false;
            item.Status = "⚠ Could not reach provider";
        }
        finally
        {
            item.IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task TestAllApiKeysAsync()
    {
        if (IsTestingAll) return;
        IsTestingAll = true;
        try
        {
            foreach (var key in WizardApiKeys.Where(k => !k.IsTested && !k.IsTesting))
            {
                await TestApiKeyAsync(key);
            }
        }
        finally
        {
            IsTestingAll = false;
        }
    }

    /// <summary>
    /// Returns a well-known endpoint URL for providers that require one
    /// (DeepSeek, Mistral, Moonshot, MiMo), or null for providers with
    /// built-in endpoints (OpenAI, Anthropic, Google).
    /// OpenAICompatible returns null in the onboarding wizard since there
    /// is no custom endpoint input field — it will use the default.
    /// </summary>
    private static string? GetProviderEndpoint(ProviderType type) => type switch
    {
        ProviderType.OpenAICompatible => null,
        ProviderType.DeepSeek => "https://api.deepseek.com",
        ProviderType.Mistral => "https://api.mistral.ai",
        ProviderType.Moonshot => "https://api.moonshot.ai/v1",
        ProviderType.MiMo => "https://api.xiaomimimo.com/v1",
        _ => null
    };
}
