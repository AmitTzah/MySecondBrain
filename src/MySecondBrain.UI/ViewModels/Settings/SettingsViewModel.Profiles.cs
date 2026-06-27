using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Model Configuration and Persona management: list, CRUD commands, model fetching.
/// </summary>
public partial class SettingsViewModel
{
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

    [ObservableProperty]
    private ProviderType _selectedModelConfigProvider = ProviderType.OpenAI;

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
            config.Temperature = Math.Clamp(config.Temperature, 0.0, 2.0);
            config.UpdatedAt = DateTimeOffset.UtcNow;

            // Sync the ProviderType from the selected API key's provider
            if (SelectedModelConfigApiKey is not null)
            {
                config.ProviderType = SelectedModelConfigApiKey.ProviderType;

                // Copy endpoint URL from API key for remapped types (DeepSeek, MiMo, etc.)
                var selectedKey = await _apiKeyRepo.GetByIdAsync(SelectedModelConfigApiKey.Id);
                if (selectedKey is not null && !string.IsNullOrEmpty(selectedKey.CustomEndpointUrl))
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

            await RefreshAvailableApiKeysAsync();
            SelectedModelConfigApiKey = AvailableApiKeys.FirstOrDefault(k => k.Id == config.ApiKeyId);

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
        _fetchModelsCts?.Cancel();
        _fetchModelsCts = new CancellationTokenSource();
        var ct = _fetchModelsCts.Token;

        _logger.LogInformation("[ListModels] SettingsVM: fetching models for providerType={ProviderType}, apiKeyId={KeyId}",
            providerType, apiKeyId);

        IsFetchingModels = true;
        AvailableModels = [];
        FetchModelsErrorMessage = string.Empty;

        try
        {
            ct.ThrowIfCancellationRequested();

            var apiKey = await _apiKeyRepo.GetByIdAsync(apiKeyId);
            if (apiKey is null)
            {
                _logger.LogWarning("[ListModels] SettingsVM: API key {KeyId} not found", apiKeyId);
                FetchModelsErrorMessage = "API key not found for model fetching.";
                IsFetchingModels = false;
                return;
            }

            // Resolve endpoint: stored CustomEndpointUrl takes priority,
            // then fall back to the well-known endpoint for this provider type.
            var endpointUrl = !string.IsNullOrEmpty(apiKey.CustomEndpointUrl)
                ? apiKey.CustomEndpointUrl
                : GetProviderEndpoint(apiKey.ProviderType);

            _logger.LogDebug("[ListModels] SettingsVM: resolved API key — provider={Provider}, label={Label}, hasStoredEndpoint={HasStoredEndpoint}, finalEndpoint={FinalEndpoint}",
                apiKey.ProviderType, apiKey.Label ?? "(none)", !string.IsNullOrEmpty(apiKey.CustomEndpointUrl), endpointUrl ?? "(null)");

            ct.ThrowIfCancellationRequested();

            var tempConfig = new ModelConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                ProviderType = providerType,
                ApiKeyId = apiKeyId,
                ModelIdentifier = string.Empty,
                EndpointUrl = endpointUrl,
            };

            _logger.LogDebug("[ListModels] SettingsVM: calling LLMProviderService.ListModelsAsync with endpoint={Endpoint}",
                apiKey.CustomEndpointUrl ?? "(null)");

            var models = await _llmProviderService.ListModelsAsync(tempConfig, ct);
            ct.ThrowIfCancellationRequested();

            if (models.Count == 0)
            {
                _logger.LogWarning("[ListModels] SettingsVM: 0 models returned for {Provider} — check endpoint URL and API key",
                    providerType);
                FetchModelsErrorMessage = "No models returned by the provider. Check API key validity.";
            }
            else
            {
                _logger.LogInformation("[ListModels] SettingsVM: got {Count} models for {Provider}: {ModelList}",
                    models.Count, providerType, string.Join(", ", models.Take(10).Select(m => m.Id)));
                AvailableModels = new ObservableCollection<string>(models.Select(m => m.Id));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[ListModels] SettingsVM: fetch cancelled for {Provider}", providerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListModels] SettingsVM: failed to fetch models for {Provider}", providerType);
            FetchModelsErrorMessage = "Failed to fetch models. Check API key validity.";
        }
        finally
        {
            IsFetchingModels = false;
        }
    }

    partial void OnSelectedModelConfigProviderChanged(ProviderType value)
    {
        // Only clear the API key if it is incompatible with the new provider.
        // When this change originates from OnSelectedModelConfigApiKeyChanged (the
        // API key selection syncs the provider), the key's ProviderType matches the
        // new value and we must not nullify it — doing so creates a re-entrant
        // cascade that destroys the selection.
        if (SelectedModelConfigApiKey is not null &&
            SelectedModelConfigApiKey.ProviderType != value)
        {
            SelectedModelConfigApiKey = null;
        }

        AvailableModels = [];
    }

    partial void OnSelectedModelConfigApiKeyChanged(ApiKeyDisplayItem? value)
    {
        if (value is not null)
        {
            // Sync the provider type selector to match the selected API key's provider.
            // This ensures the model config saves with the correct ProviderType
            // (e.g. DeepSeek, MiMo) instead of the default (OpenAI).
            SelectedModelConfigProvider = value.ProviderType;
            _ = FetchModelsForProviderAsync(value.ProviderType, value.Id);
        }
        else
        {
            AvailableModels = [];
        }
    }

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
}
