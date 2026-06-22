using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using WpfApplication = System.Windows.Application;

namespace MySecondBrain.UI.ViewModels;

public partial class OnboardingWizardViewModel : ObservableObject
{
    /// <summary>
    /// Fired when the user confirms they want to close the wizard mid-setup.
    /// The window subscribes to this to perform the actual close.
    /// </summary>
    public event Action? CloseConfirmed;

    /// <summary>
    /// Fired when the user clicks "Launch Studio" on the finish screen.
    /// App.xaml.cs subscribes to this to close the wizard and open the main window.
    /// </summary>
    public event Action? LaunchStudioRequested;

    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly ISettingsRepository _settingsRepo;

    /// <summary>
    /// True when onboarding is being re-run from Settings (Onboarding_Completed was "true").
    /// Used to suppress Welcome screen and prevent Back from step 0.
    /// </summary>
    private bool _isReRun;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly IPersonaRepository _personaRepo;
    private readonly ITextActionRepository _textActionRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly ILLMProviderService _llmProviderService;
    private readonly IConfirmationService _confirmationService;
    private readonly IWikiService _wikiService;
    private readonly ILogger<OnboardingWizardViewModel> _logger;

    private const string Step1Key = "Onboarding_Step1_Completed";
    private const string Step2Key = "Onboarding_Step2_Completed";
    private const string Step3Key = "Onboarding_Step3_Completed";
    private const string Step4Key = "Onboarding_Step4_Completed";

    // Default hotkeys for wizard display
    private static readonly Dictionary<string, string> s_defaultHotkeys = new()
    {
        { "__rewrite__", "Alt+Q" },
        { "__summarize__", "Alt+W" },
        { "__explain__", "Alt+E" },
        { "__translate__", "Alt+R" },
        { "__continue__", "Alt+C" },
        { "__commandbar__", "Alt+Space" },
    };

    public OnboardingWizardViewModel(
        IApiKeyRepository apiKeyRepo,
        ISettingsRepository settingsRepo,
        IModelConfigurationRepository modelConfigRepo,
        IPersonaRepository personaRepo,
        ITextActionRepository textActionRepo,
        IEncryptionService encryptionService,
        ILLMProviderService llmProviderService,
        IConfirmationService confirmationService,
        IWikiService wikiService,
        ILogger<OnboardingWizardViewModel> logger)
    {
        _apiKeyRepo = apiKeyRepo;
        _settingsRepo = settingsRepo;
        _modelConfigRepo = modelConfigRepo;
        _personaRepo = personaRepo;
        _textActionRepo = textActionRepo;
        _encryptionService = encryptionService;
        _llmProviderService = llmProviderService;
        _confirmationService = confirmationService;
        _wikiService = wikiService;
        _logger = logger;

        // Build starter personas
        StarterPersonas =
        [
            new StarterPersonaCard
            {
                Id = "general",
                DisplayName = "General Assistant",
                SystemPrompt = "You are a helpful, thoughtful assistant. Provide clear, accurate, and concise responses. When you don't know something, say so honestly.",
                PromptPreview = "You are a helpful, thoughtful assistant. Provide clear, accurate, and concise responses..."
            },
            new StarterPersonaCard
            {
                Id = "code",
                DisplayName = "Code Helper",
                SystemPrompt = "You are an expert software developer. Write clean, well-documented code. Explain your reasoning. Prefer practical solutions over theoretical ones.",
                PromptPreview = "You are an expert software developer. Write clean, well-documented code. Explain your reasoning..."
            },
            new StarterPersonaCard
            {
                Id = "writing",
                DisplayName = "Writing Coach",
                SystemPrompt = "You are an experienced writing coach and editor. Help improve clarity, flow, and impact. Be constructive and specific in feedback. Preserve the author's voice.",
                PromptPreview = "You are an experienced writing coach and editor. Help improve clarity, flow, and impact..."
            },
        ];

        // Build default hotkey assignments
        BuildDefaultHotkeys();

        // Resume logic
        _ = InitializeAsync();
    }

    // ================================================================
    // State Machine
    // ================================================================

    [ObservableProperty]
    private int _currentStep = -1;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(IsWelcomeScreen));
        OnPropertyChanged(nameof(IsFinishScreen));
        OnPropertyChanged(nameof(IsStepScreen));
        OnPropertyChanged(nameof(NextButtonText));
    }

    public bool CanGoBack => _isReRun ? CurrentStep > 0 : CurrentStep > -1;
    public bool CanGoNext => CurrentStep >= 0 && CurrentStep < 4;
    public bool CanSkip => CurrentStep >= 0 && CurrentStep < 4;
    public bool IsWelcomeScreen => CurrentStep == -1;
    public bool IsFinishScreen => CurrentStep == 4;
    public bool IsStepScreen => CurrentStep >= 0 && CurrentStep < 4;
    public string NextButtonText => CurrentStep == 3 ? "Finish" : "Next";

    // ================================================================
    // Step completion flags (cached, persisted to ISettingsRepository)
    // ================================================================

    [ObservableProperty]
    private bool _step1Completed;

    [ObservableProperty]
    private bool _step2Completed;

    [ObservableProperty]
    private bool _step3Completed;

    [ObservableProperty]
    private bool _step4Completed;

    // ================================================================
    // Step 0 — API Keys
    // ================================================================

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

    // ================================================================
    // Step 1 — Persona
    // ================================================================

    public ObservableCollection<StarterPersonaCard> StarterPersonas { get; }

    [ObservableProperty]
    private StarterPersonaCard? _selectedStarterPersona;

    [ObservableProperty]
    private string _personaDisplayName = string.Empty;

    [ObservableProperty]
    private string _personaSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _personaChatMode = "Standard";

    [ObservableProperty]
    private bool _isCreatingFromScratch;

    public IReadOnlyList<string> ChatModeOptions { get; } = ["Standard", "TextCompletion"];

    partial void OnSelectedStarterPersonaChanged(StarterPersonaCard? value)
    {
        if (value is null || _isCreatingFromScratch) return;
        PersonaDisplayName = value.DisplayName;
        PersonaSystemPrompt = value.SystemPrompt;
    }

    // ================================================================
    // Step 2 — Wiki
    // ================================================================

    [ObservableProperty]
    private string _wikiDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _wikiFileCount = string.Empty;

    [ObservableProperty]
    private bool _gitVersionControlEnabled;

    [ObservableProperty]
    private bool _gitAutoCommitEnabled = true;

    [ObservableProperty]
    private bool _isWikiCreating;

    [ObservableProperty]
    private string _wikiStatusMessage = string.Empty;

    // ================================================================
    // Step 3 — Hotkeys
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<WizardHotkeyItem> _hotkeyAssignments = [];

    private WizardHotkeyItem? _changingHotkeyItem;

    [ObservableProperty]
    private string _recordingHotkeyCombo = string.Empty;

    [ObservableProperty]
    private bool _isRecordingHotkey;

    // ================================================================
    // Step 4 — Finish Summary
    // ================================================================

    public int ConfiguredKeyCount { get; set; }
    public string ConfiguredPersonaName { get; set; } = string.Empty;
    public string ConfiguredWikiPath { get; set; } = string.Empty;
    public int ConfiguredHotkeyCount { get; set; }

    // ================================================================
    // Navigation Commands
    // ================================================================

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (_isReRun && CurrentStep <= 0)
        {
            // On re-run, prevent going back to Welcome screen (step -1).
            // Stay at step 0 instead — Back button is already disabled via CanGoBack,
            // this is a safety guard.
            CurrentStep = 0;
            return;
        }

        if (CurrentStep <= 0)
        {
            CurrentStep = -1;
            return;
        }
        CurrentStep--;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        if (CurrentStep == 3)
        {
            await PersistStep4CompletedAsync();
            Step4Completed = true;
            CurrentStep = 4;
            RefreshSummary();
            return;
        }

        if (CurrentStep >= 0 && CurrentStep < 3)
            await PersistStepCompletedAsync(CurrentStep);

        CurrentStep++;
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        if (CurrentStep < 0 || CurrentStep >= 4) return;

        if (CurrentStep == 3)
        {
            // Use default hotkeys — already set
            await PersistStep4CompletedAsync();
            Step4Completed = true;
            CurrentStep = 4;
            RefreshSummary();
            return;
        }

        if (CurrentStep >= 0 && CurrentStep < 3)
            await PersistStepCompletedAsync(CurrentStep);

        CurrentStep++;
    }

    [RelayCommand]
    private void GetStarted()
    {
        var startingStep = DetermineFirstIncompleteStep();
        CurrentStep = startingStep;
    }

    [RelayCommand]
    private void GoToStep(int step)
    {
        if (step < 0 || step > 4) return;
        // Only allow going to completed steps or the current step
        if (step < CurrentStep || step == CurrentStep)
        {
            CurrentStep = step;
        }
    }

    [RelayCommand]
    private void RequestClose()
    {
        if (_confirmationService.Confirm(
            "You haven't finished setup. Your progress is saved. You can continue later or re-run the wizard from Settings.\n\nContinue Setup / Close Anyway",
            "Onboarding Incomplete"))
        {
            // Save completed step flags and notify window to close
            _ = PersistAllCompletedFlagsAsync();
            CloseConfirmed?.Invoke();
        }
    }

    // ================================================================
    // Step 0 Commands — API Keys
    // ================================================================

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

    // ================================================================
    // Step 1 Commands — Persona
    // ================================================================

    [RelayCommand]
    private void SelectPersona(StarterPersonaCard? card)
    {
        if (card is null) return;

        // Deselect all, select the clicked one
        foreach (var p in StarterPersonas)
            p.IsSelected = p.Id == card.Id;

        SelectedStarterPersona = card;
        IsCreatingFromScratch = false;
        PersonaDisplayName = card.DisplayName;
        PersonaSystemPrompt = card.SystemPrompt;
    }

    [RelayCommand]
    private async Task SavePersonaAsync()
    {
        if (string.IsNullOrWhiteSpace(PersonaDisplayName))
        {
            _confirmationService.Confirm("Display name is required.", "Validation");
            return;
        }

        try
        {
            var persona = new Persona
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = PersonaDisplayName,
                SystemPrompt = PersonaSystemPrompt,
                DefaultChatMode = PersonaChatMode,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await _personaRepo.CreateAsync(persona);
            _logger.LogInformation("Onboarding: created persona '{Name}'", persona.DisplayName);

            await PersistStepCompletedAsync(1);
            Step2Completed = true;
            CurrentStep = 2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save persona during onboarding");
            _confirmationService.Confirm($"Could not save persona: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void CreateFromScratch()
    {
        IsCreatingFromScratch = true;
        SelectedStarterPersona = null;
        PersonaDisplayName = string.Empty;
        PersonaSystemPrompt = string.Empty;
    }

    // ================================================================
    // Step 2 Commands — Wiki
    // ================================================================

    [RelayCommand]
    private async Task ChooseExistingWikiFolderAsync()
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
                    WikiStatusMessage = "Selected directory does not exist.";
                    return;
                }

                WikiDirectoryPath = path;
                await _settingsRepo.SetAsync("WikiDirectoryPath", path);

                var mdCount = Directory.GetFiles(path, "*.md", SearchOption.AllDirectories).Length;
                WikiFileCount = $"{mdCount} .md file(s) found";
                WikiStatusMessage = "Indexing wiki files...";

                try
                {
                    await _wikiService.IndexAllAsync(CancellationToken.None);
                    WikiStatusMessage = "Wiki indexed successfully.";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wiki indexing failed during onboarding");
                    WikiStatusMessage = "Wiki directory set. Indexing will run in background.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to choose wiki folder");
            WikiStatusMessage = "Could not open folder picker.";
        }
    }

    [RelayCommand]
    private async Task CreateNewWikiFolderAsync()
    {
        if (IsWikiCreating) return;
        IsWikiCreating = true;

        try
        {
            var wikiPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MySecondBrain-Wiki");

            Directory.CreateDirectory(wikiPath);

            var indexPath = Path.Combine(wikiPath, "index.md");
            if (!File.Exists(indexPath))
            {
                await File.WriteAllTextAsync(indexPath,
                    "# My Wiki\n\nWelcome to your personal wiki. Add .md files here to build your second brain.\n");
            }

            WikiDirectoryPath = wikiPath;
            WikiFileCount = "1 .md file (index.md)";
            WikiStatusMessage = "Created with starter index.md";

            await _settingsRepo.SetAsync("WikiDirectoryPath", wikiPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create wiki folder");
            WikiStatusMessage = "Could not create wiki folder.";
        }
        finally
        {
            IsWikiCreating = false;
        }
    }

    partial void OnGitVersionControlEnabledChanged(bool value)
    {
        _ = _settingsRepo.SetAsync("GitVersionControlEnabled", value ? "true" : "false");
    }

    // ================================================================
    // Step 3 Commands — Hotkeys
    // ================================================================

    [RelayCommand]
    private void ChangeWizardHotkey(WizardHotkeyItem? item)
    {
        if (item is null) return;
        _changingHotkeyItem = item;
        RecordingHotkeyCombo = item.Hotkey;
        IsRecordingHotkey = true;
    }

    /// <summary>
    /// Called from code-behind when a hotkey combination is recorded.
    /// </summary>
    public void ApplyRecordedHotkey(string combo)
    {
        var item = _changingHotkeyItem;
        if (item is null) return;

        if (string.IsNullOrWhiteSpace(combo))
        {
            IsRecordingHotkey = false;
            _changingHotkeyItem = null;
            return;
        }

        // Check for conflict
        var conflict = HotkeyAssignments.FirstOrDefault(
            h => h.Hotkey == combo && h.ActionId != item.ActionId);
        if (conflict is not null)
        {
            if (!_confirmationService.Confirm(
                $"Hotkey '{combo}' is already assigned to '{conflict.ActionName}'. Assign anyway?",
                "Hotkey Conflict"))
            {
                IsRecordingHotkey = false;
                _changingHotkeyItem = null;
                return;
            }
        }

        item.Hotkey = combo;
        item.IsRecording = false;
        IsRecordingHotkey = false;
        _changingHotkeyItem = null;
    }

    public void CancelHotkeyRecording()
    {
        if (_changingHotkeyItem is not null)
        {
            _changingHotkeyItem.IsRecording = false;
            _changingHotkeyItem = null;
        }
        IsRecordingHotkey = false;
    }

    [RelayCommand]
    private void ResetWizardHotkeysToDefaults()
    {
        foreach (var item in HotkeyAssignments)
        {
            if (s_defaultHotkeys.TryGetValue(item.ActionId, out var hotkey))
                item.Hotkey = hotkey;
        }
    }

    // ================================================================
    // Step 4 Commands — Finish
    // ================================================================

    [RelayCommand]
    private async Task LaunchStudio()
    {
        // Persist hotkey changes first so they show in Settings Hotkeys tab
        await SaveHotkeysToRepositoryAsync();

        // Save all keys to the repository
        await SaveKeysToRepositoryAsync();

        // Mark onboarding as fully completed so it won't show on next launch
        await _settingsRepo.SetAsync("Onboarding_Completed", "true");

        // Fire event so App.xaml.cs can close wizard and open Studio
        LaunchStudioRequested?.Invoke();

        // Also send messenger message for loose coupling
        WeakReferenceMessenger.Default.Send(new LaunchStudioMessage());
    }

    [RelayCommand]
    private void ImportFromChatGpt()
    {
        _confirmationService.Confirm(
            "Import from ChatGPT or Claude is coming soon. Stay tuned for updates!",
            "Coming Soon");
    }

    // ================================================================
    // Initialization & Resume
    // ================================================================

    private async Task InitializeAsync()
    {
        try
        {
            Step1Completed = (await _settingsRepo.GetAsync(Step1Key)) == "true";
            Step2Completed = (await _settingsRepo.GetAsync(Step2Key)) == "true";
            Step3Completed = (await _settingsRepo.GetAsync(Step3Key)) == "true";
            Step4Completed = (await _settingsRepo.GetAsync(Step4Key)) == "true";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load onboarding completion flags");
        }

        // If onboarding was previously completed, this is a re-run from Settings.
        // Skip the Welcome screen and go directly to step 0 (API Keys).
        string? onboardingCompleted = null;
        try { onboardingCompleted = await _settingsRepo.GetAsync("Onboarding_Completed"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to load onboarding completed flag"); }

        if (onboardingCompleted == "true")
        {
            _isReRun = true;
            CurrentStep = 0;
            return;
        }

        var startingStep = DetermineFirstIncompleteStep();

        if (startingStep == 0)
        {
            // First launch — no steps completed yet. Show Welcome screen (step -1).
            // "Get Started" button navigates to step 0.
            CurrentStep = -1;
        }
        else if (startingStep == 4)
        {
            // All steps completed but Onboarding_Completed not set (edge case).
            // Show Welcome as a graceful fallback.
            CurrentStep = -1;
        }
        else
        {
            // Resume mid-wizard at first incomplete step.
            CurrentStep = startingStep;
        }
    }

    private int DetermineFirstIncompleteStep()
    {
        if (!Step1Completed) return 0;
        if (!Step2Completed) return 1;
        if (!Step3Completed) return 2;
        if (!Step4Completed) return 3;
        return 4;
    }

    // ================================================================
    // Persistence Helpers
    // ================================================================

    private async Task PersistStepCompletedAsync(int step)
    {
        var key = step switch
        {
            0 => Step1Key,
            1 => Step2Key,
            2 => Step3Key,
            3 => Step4Key,
            _ => null,
        };

        if (key is null) return;
        try { await _settingsRepo.SetAsync(key, "true"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist step {Step}", step); }
    }

    private async Task PersistStep4CompletedAsync()
    {
        await PersistStepCompletedAsync(3);
    }

    private async Task PersistAllCompletedFlagsAsync()
    {
        if (Step1Completed) await PersistStepCompletedAsync(0);
        if (Step2Completed) await PersistStepCompletedAsync(1);
        if (Step3Completed) await PersistStepCompletedAsync(2);
        if (Step4Completed) await PersistStepCompletedAsync(3);
    }

    private async Task SaveKeysToRepositoryAsync()
    {
        // Get existing keys to avoid duplicates on re-run
        IReadOnlyList<ApiKey>? existingKeys = null;
        try { existingKeys = await _apiKeyRepo.GetAllAsync(); }
        catch { /* proceed — will create new keys */ }

        foreach (var key in WizardApiKeys)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key.PlaintextKey)) continue;

                // Skip if already saved (same provider + same encrypted value)
                if (existingKeys is not null)
                {
                    var existing = existingKeys.FirstOrDefault(ek =>
                        ek.ProviderType == key.ProviderType &&
                        !string.IsNullOrEmpty(ek.Label) &&
                        ek.Label == key.DisplayName);
                    if (existing is not null)
                    {
                        _logger.LogDebug("Onboarding: API key for {Provider} already exists, skipping", key.ProviderType);
                        continue;
                    }
                }

                var encrypted = _encryptionService.ProtectString(key.PlaintextKey);
                var apiKey = new ApiKey
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProviderType = key.ProviderType,
                    EncryptedValue = encrypted,
                    Label = key.DisplayName,
                    IsValid = key.IsValid || key.IsTested,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                await _apiKeyRepo.CreateAsync(apiKey);
                _logger.LogInformation("Onboarding: saved API key for {Provider}", key.ProviderType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save API key during onboarding");
            }
        }
    }

    /// <summary>
    /// Persists hotkey changes made in the wizard step 3 to TextAction entities
    /// so they appear in the Settings Hotkeys tab after Launch Studio.
    /// Skips the "__commandbar__" action since it is not a TextAction.
    /// </summary>
    private async Task SaveHotkeysToRepositoryAsync()
    {
        IReadOnlyList<TextAction>? existingActions = null;
        try { existingActions = await _textActionRepo.GetAllAsync(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load TextActions for hotkey persistence");
            return;
        }

        foreach (var wizardItem in HotkeyAssignments)
        {
            try
            {
                // Skip Command Bar — it's not a TextAction entity
                if (wizardItem.ActionId == "__commandbar__") continue;

                // Match by DisplayName since wizard ActionNames align with TextAction DisplayNames
                var textAction = existingActions.FirstOrDefault(a =>
                    a.DisplayName == wizardItem.ActionName);

                if (textAction is null)
                {
                    _logger.LogDebug("Onboarding: no TextAction found for '{Name}', skipping",
                        wizardItem.ActionName);
                    continue;
                }

                // Only update if the hotkey actually changed from what's in the DB
                if (textAction.Hotkey == wizardItem.Hotkey) continue;

                textAction.Hotkey = wizardItem.Hotkey;
                textAction.UpdatedAt = DateTimeOffset.UtcNow;
                await _textActionRepo.UpdateAsync(textAction);
                _logger.LogInformation("Onboarding: saved hotkey '{Hotkey}' for '{Name}'",
                    wizardItem.Hotkey, wizardItem.ActionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save hotkey for wizard item '{Name}'",
                    wizardItem.ActionName);
            }
        }
    }

    private void BuildDefaultHotkeys()
    {
        var hotkeys = new List<WizardHotkeyItem>
        {
            new() { ActionId = "__rewrite__", ActionName = "Rewrite", Hotkey = s_defaultHotkeys["__rewrite__"], CaptureScope = "selection", ApplyMode = "replaceSelection" },
            new() { ActionId = "__summarize__", ActionName = "Summarize", Hotkey = s_defaultHotkeys["__summarize__"], CaptureScope = "selection", ApplyMode = "showOnly" },
            new() { ActionId = "__explain__", ActionName = "Explain", Hotkey = s_defaultHotkeys["__explain__"], CaptureScope = "selection", ApplyMode = "showOnly" },
            new() { ActionId = "__translate__", ActionName = "Translate", Hotkey = s_defaultHotkeys["__translate__"], CaptureScope = "selection", ApplyMode = "replaceSelection" },
            new() { ActionId = "__continue__", ActionName = "Continue Writing", Hotkey = s_defaultHotkeys["__continue__"], CaptureScope = "focusedElement", ApplyMode = "insertAtCursor" },
            new() { ActionId = "__commandbar__", ActionName = "Command Bar", Hotkey = s_defaultHotkeys["__commandbar__"], CaptureScope = "global", ApplyMode = "showOnly" },
        };
        HotkeyAssignments = new ObservableCollection<WizardHotkeyItem>(hotkeys);
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

    private void RefreshSummary()
    {
        ConfiguredKeyCount = WizardApiKeys.Count;
        ConfiguredPersonaName = PersonaDisplayName;
        ConfiguredWikiPath = WikiDirectoryPath;
        ConfiguredHotkeyCount = HotkeyAssignments.Count;
        OnPropertyChanged(nameof(ConfiguredKeyCount));
        OnPropertyChanged(nameof(ConfiguredPersonaName));
        OnPropertyChanged(nameof(ConfiguredWikiPath));
        OnPropertyChanged(nameof(ConfiguredHotkeyCount));
    }
}
