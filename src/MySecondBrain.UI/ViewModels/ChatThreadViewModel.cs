using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
// Resolve ambiguity with System.Windows.Forms.Message (UseWindowsForms=true)
using Message = MySecondBrain.Core.Models.Message;

namespace MySecondBrain.UI.ViewModels;

public partial class ChatThreadViewModel : ObservableObject
{
    private readonly IChatThreadService _chatService;
    private readonly IPersonaRepository _personaRepo;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly ISkillService _skillService;
    private readonly IConfirmationService _confirmationService;
    private readonly IThemeProvider _themeProvider;
    private readonly SystemPromptCoordinator _systemPromptCoordinator;
    private readonly ILogger<ChatThreadViewModel> _logger;

    private const string RecentPersonaIdsKey = "RecentPersonaIds";
    private const string LastSelectedPersonaIdKey = "LastSelectedPersonaId";
    private const int MaxRecentPersonas = 5;

    /// <summary>
    /// Prevents re-entrant calls to SetActivePersonaAsync when ActivePersona
    /// is set from both the command path and the OnActivePersonaChanged path.
    /// </summary>
    private bool _isSettingActivePersona;

    // ── Tab management ────────────────────────────────────────────────

    /// <summary>Active CancellationTokenSource for the current SendMessageAsync.</summary>
    private CancellationTokenSource? _activeCts;

    /// <summary>Last closed tab — preserved for Ctrl+Shift+T reopen.</summary>
    private ChatTabItem? _lastClosedTab;

    /// <summary>Periodic timer for auto-saving drafts every 5 seconds.</summary>
    private PeriodicTimer? _draftTimer;
    private CancellationTokenSource? _draftTimerCts;

    /// <summary>Tracks the tab subscribed to PropertyChanged for cleanup on switch.</summary>
    private ChatTabItem? _subscribedTab;

    public ChatThreadViewModel(
        IChatThreadService chatService,
        IPersonaRepository personaRepo,
        IModelConfigurationRepository modelConfigRepo,
        ISettingsRepository settingsRepo,
        ISkillService skillService,
        IConfirmationService confirmationService,
        IThemeProvider themeProvider,
        ILogger<ChatThreadViewModel> logger)
    {
        _chatService = chatService;
        _personaRepo = personaRepo;
        _modelConfigRepo = modelConfigRepo;
        _settingsRepo = settingsRepo;
        _skillService = skillService;
        _confirmationService = confirmationService;
        _themeProvider = themeProvider;
        _systemPromptCoordinator = new SystemPromptCoordinator(skillService);
        _logger = logger;

        // Initialize per-chat tool/skill/memory toggles with global defaults
        InitializeToolToggles();
        InitializeSkillToggles();
        InitializeMemoryToggle();

        // Register for cross-tab completion alerts
        WeakReferenceMessenger.Default.Register<GenerationCompletedMessage>(this, (r, m) =>
        {
            var tab = ChatTabs.FirstOrDefault(t => t.Thread.Id == m.Value);
            if (tab is not null && tab != ActiveTab)
                tab.HasCompletionAlert = true;
        });
    }

    // ================================================================
    // Tab management
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<ChatTabItem> _chatTabs = [];

    [ObservableProperty]
    private ChatTabItem? _activeTab;

    /// <summary>
    /// True when the active tab is streaming. Aggregate property
    /// that updates when ActiveTab changes or ActiveTab.IsStreaming changes.
    /// </summary>
    public bool IsStreaming => ActiveTab?.IsStreaming ?? false;

    partial void OnActiveTabChanged(ChatTabItem? value)
    {
        // Reset completion alert when switching to a tab
        if (value is not null)
            value.HasCompletionAlert = false;

        OnPropertyChanged(nameof(IsStreaming));

        // Unsubscribe from the previous tab to prevent leaks
        if (_subscribedTab is not null)
            _subscribedTab.PropertyChanged -= OnActiveTabPropertyChanged;

        // Subscribe to the new tab's streaming changes
        if (value is not null)
            value.PropertyChanged += OnActiveTabPropertyChanged;

        _subscribedTab = value;
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatTabItem.IsStreaming))
            OnPropertyChanged(nameof(IsStreaming));
    }

    // ================================================================
    // Observable properties — toggles & global state
    // ================================================================

    [ObservableProperty]
    private bool _thinkingEnabled;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private string _streamingContent = string.Empty;

    [ObservableProperty]
    private string _thinkingContent = string.Empty;

    [ObservableProperty]
    private int _contextTokens;

    [ObservableProperty]
    private int _contextMaxTokens = 128000;

    [ObservableProperty]
    private decimal _cumulativeCost;

    // ================================================================
    // Per-chat toolbar toggle state
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<ToolToggleItem> _toolToggles = [];

    [ObservableProperty]
    private ObservableCollection<SkillToggleItem> _skillToggles = [];

    [ObservableProperty]
    private bool _memoryEnabled;

    public IReadOnlySet<string> EnabledToolNames =>
        ToolToggles
            .Where(t => t.IsEnabled)
            .Select(t => t.Name)
            .ToHashSet();

    public IReadOnlySet<string> EnabledSkillNames =>
        SkillToggles
            .Where(s => s.IsEnabled)
            .Select(s => s.Name)
            .ToHashSet();

    public void SetAllSkillsEnabled(bool enabled)
    {
        foreach (var skill in SkillToggles)
            skill.IsEnabled = enabled;
    }

    private void InitializeToolToggles()
    {
        var tools = new (string name, string displayName)[]
        {
            ("bash", "Bash"),
            ("text_editor", "Text Editor"),
            ("web_search", "Web Search"),
            ("web_fetch", "Web Fetch"),
            ("wiki_search", "Wiki Search"),
            ("memory", "Memory"),
            ("skill_load", "Skill Load"),
            ("ask_user_input", "Ask User Input"),
            ("present_files", "Present Files"),
            ("image_search", "Image Search"),
        };

        var items = new List<ToolToggleItem>(tools.Length);
        foreach (var (name, displayName) in tools)
        {
            items.Add(new ToolToggleItem
            {
                Name = name,
                DisplayName = displayName,
                IsEnabled = true,
            });
        }

        ToolToggles = new ObservableCollection<ToolToggleItem>(items);
    }

    private void InitializeSkillToggles()
    {
        SkillToggles = [];
    }

    public void PopulateSkillToggles(IReadOnlyList<SkillMetadata> discoveredSkills)
    {
        var items = discoveredSkills
            .Select(s => new SkillToggleItem
            {
                Name = s.Name,
                Description = s.Description,
                IsEnabled = true,
            })
            .ToList();

        SkillToggles = new ObservableCollection<SkillToggleItem>(items);
    }

    private void InitializeMemoryToggle()
    {
        MemoryEnabled = false;
    }

    // ================================================================
    // Observable properties — persona
    // ================================================================

    [ObservableProperty]
    private Persona? _activePersona;

    [ObservableProperty]
    private ObservableCollection<Persona> _personaList = [];

    [ObservableProperty]
    private ModelConfiguration? _activeModelConfig;

    [ObservableProperty]
    private string _personaPickerSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Persona> _filteredPersonaList = [];

    partial void OnActivePersonaChanged(Persona? value)
    {
        if (value is null || _isSettingActivePersona)
            return;

        _ = SetActivePersonaAsync(value);
    }

    // ================================================================
    // Initialization
    // ================================================================

    public async Task InitializeAsync()
    {
        try
        {
            await RefreshPersonaListAsync();

            var savedPersonaId = await _settingsRepo.GetAsync(LastSelectedPersonaIdKey);
            Persona? personaToActivate = null;

            if (savedPersonaId is not null)
            {
                personaToActivate = PersonaList.FirstOrDefault(p => p.Id == savedPersonaId);
            }

            personaToActivate ??= await _personaRepo.GetDefaultAsync();

            if (personaToActivate is not null)
            {
                await SetActivePersonaAsync(personaToActivate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ChatThreadViewModel");
        }
    }

    // ================================================================
    // Persona list
    // ================================================================

    private async Task RefreshPersonaListAsync()
    {
        try
        {
            var allPersonas = await _personaRepo.GetAllAsync();
            var sorted = allPersonas
                .OrderBy(p => p.DisplayName)
                .ToList();

            PersonaList = new ObservableCollection<Persona>(sorted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh persona list");
        }
    }

    // ================================================================
    // Recently-used tracking
    // ================================================================

    private async Task TrackRecentPersonaAsync(string personaId)
    {
        try
        {
            var recentIds = await _settingsRepo.GetAsync<List<string>>(RecentPersonaIdsKey) ?? [];

            recentIds.Remove(personaId);
            recentIds.Insert(0, personaId);

            if (recentIds.Count > MaxRecentPersonas)
                recentIds = recentIds.Take(MaxRecentPersonas).ToList();

            await _settingsRepo.SetAsync(RecentPersonaIdsKey, recentIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track recent persona");
        }
    }

    // ================================================================
    // Persona commands
    // ================================================================

    [RelayCommand]
    private async Task SelectPersonaAsync(Persona? persona)
    {
        if (persona is null || persona.Id == ActivePersona?.Id)
            return;

        await SetActivePersonaAsync(persona);
    }

    private async Task SetActivePersonaAsync(Persona persona)
    {
        _isSettingActivePersona = true;
        try
        {
            ActivePersona = persona;

            if (PersonaList.Count > 0)
            {
                var match = PersonaList.FirstOrDefault(p => p.Id == persona.Id);
                if (match is not null)
                {
                    ActivePersona = match;
                    persona = match;
                }
            }

            if (!string.IsNullOrEmpty(persona.DefaultModelConfigId))
            {
                var config = await _modelConfigRepo.GetByIdAsync(persona.DefaultModelConfigId);
                ActiveModelConfig = config;
            }
            else
            {
                ActiveModelConfig = null;
            }

            await TrackRecentPersonaAsync(persona.Id);
            await _settingsRepo.SetAsync(LastSelectedPersonaIdKey, persona.Id);

            _logger.LogDebug("Active persona set to '{Persona}' (config: {Config})",
                persona.DisplayName,
                ActiveModelConfig?.DisplayName ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set active persona '{Persona}'", persona.DisplayName);
        }
        finally
        {
            _isSettingActivePersona = false;
        }
    }

    [RelayCommand]
    private void PreparePersonaPicker()
    {
        PersonaPickerSearchText = string.Empty;
        FilteredPersonaList = new ObservableCollection<Persona>(PersonaList);
    }

    [RelayCommand]
    private void FilterPersonaPicker()
    {
        var search = PersonaPickerSearchText?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(search))
        {
            FilteredPersonaList = new ObservableCollection<Persona>(PersonaList);
        }
        else
        {
            var filtered = PersonaList
                .Where(p => p.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
            FilteredPersonaList = new ObservableCollection<Persona>(filtered);
        }
    }

    // ================================================================
    // Tab commands
    // ================================================================

    [RelayCommand]
    private async Task NewChatAsync()
    {
        try
        {
            var persona = ActivePersona ?? PersonaList.FirstOrDefault();
            if (persona is null) return;

            var thread = await _chatService.CreateThreadAsync(null, false, persona);
            var tab = new ChatTabItem(thread);
            ChatTabs.Add(tab);
            ActiveTab = tab;

            // Start auto-save timer on first tab creation
            if (_draftTimer is null)
                StartDraftTimer();

            _logger.LogDebug("Created new chat tab '{ThreadId}'", thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new chat tab");
        }
    }

    [RelayCommand]
    private void CloseTab(ChatTabItem? tab)
    {
        if (tab is null) return;

        // If streaming, show confirmation
        if (tab.IsStreaming)
        {
            var confirmed = _confirmationService.Confirm(
                "A response is still being generated in this tab. Are you sure you want to close it?",
                "Generation in Progress");
            if (!confirmed) return;

            // Cancel the active CTS if this is the active tab
            if (tab == ActiveTab)
                _activeCts?.Cancel();
        }

        // Preserve for reopen
        _lastClosedTab = tab;

        ChatTabs.Remove(tab);

        // Select another tab if available
        if (ActiveTab is null && ChatTabs.Count > 0)
            ActiveTab = ChatTabs[^1];

        // Stop draft timer when all tabs are closed
        if (ChatTabs.Count == 0)
            StopDraftTimer();

        _logger.LogDebug("Closed chat tab '{ThreadId}'", tab.Thread.Id);
    }

    [RelayCommand]
    private async Task ReopenLastClosedTabAsync()
    {
        if (_lastClosedTab is null) return;

        // Re-create the thread if it was soft-deleted
        try
        {
            var thread = await _chatService.GetThreadAsync(_lastClosedTab.Thread.Id);
            if (thread is null || thread.IsDeleted)
            {
                await _chatService.RestoreThreadAsync(_lastClosedTab.Thread.Id);
                _lastClosedTab.Thread.IsDeleted = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore thread for reopen");
        }

        ChatTabs.Add(_lastClosedTab);
        ActiveTab = _lastClosedTab;
        _lastClosedTab = null;
    }

    // ================================================================
    // Message commands
    // ================================================================

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (ActiveTab is null || string.IsNullOrWhiteSpace(ActiveTab.TextboxContent))
            return;

        var content = ActiveTab.TextboxContent;
        ActiveTab.TextboxContent = string.Empty;
        ActiveTab.IsStreaming = true;

        try
        {
            using var cts = new CancellationTokenSource();
            _activeCts = cts;

            var message = await _chatService.SendMessageAsync(ActiveTab.Thread.Id, content, cts.Token);

            // Load messages for the active branch
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            ActiveTab.Messages = new ObservableCollection<Message>(messages);

            // Update aggregate state
            UpdateContextAndCost();

            // Notify cross-tab
            WeakReferenceMessenger.Default.Send(
                new GenerationCompletedMessage(ActiveTab.Thread.Id));
        }
        catch (OperationCanceledException)
        {
            // Partial response preserved by the service
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            ActiveTab.Messages = new ObservableCollection<Message>(messages);
            _logger.LogDebug("Message generation cancelled — partial response preserved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            // Show error state — could be extended with error banner
        }
        finally
        {
            ActiveTab.IsStreaming = false;
            _activeCts = null;
        }
    }

    [RelayCommand]
    private void StopGeneration()
    {
        _activeCts?.Cancel();
    }

    [RelayCommand]
    private async Task RegenerateAsync()
    {
        if (ActiveTab is null || ActiveTab.Messages.Count == 0)
            return;

        if (ActiveTab.IsStreaming) return;

        var lastMsg = ActiveTab.Messages[^1];
        if (lastMsg.Role != "assistant") return;

        ActiveTab.IsStreaming = true;

        try
        {
            using var cts = new CancellationTokenSource();
            _activeCts = cts;

            await _chatService.RegenerateAsync(lastMsg.Id, cts.Token);

            // Reload messages
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            ActiveTab.Messages = new ObservableCollection<Message>(messages);
            UpdateContextAndCost();

            // Notify cross-tab
            WeakReferenceMessenger.Default.Send(
                new GenerationCompletedMessage(ActiveTab.Thread.Id));
        }
        catch (OperationCanceledException)
        {
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            ActiveTab.Messages = new ObservableCollection<Message>(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate message");
        }
        finally
        {
            ActiveTab.IsStreaming = false;
            _activeCts = null;
        }
    }

    [RelayCommand]
    private async Task ContinueGenerationAsync()
    {
        if (ActiveTab is null) return;

        if (ActiveTab.IsStreaming) return;

        ActiveTab.IsStreaming = true;

        try
        {
            using var cts = new CancellationTokenSource();
            _activeCts = cts;

            await _chatService.ContinueGenerationAsync(ActiveTab.Thread.Id, cts.Token);

            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            ActiveTab.Messages = new ObservableCollection<Message>(messages);
            UpdateContextAndCost();

            // Notify cross-tab
            WeakReferenceMessenger.Default.Send(
                new GenerationCompletedMessage(ActiveTab.Thread.Id));
        }
        catch (OperationCanceledException)
        {
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            ActiveTab.Messages = new ObservableCollection<Message>(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to continue generation");
        }
        finally
        {
            ActiveTab.IsStreaming = false;
            _activeCts = null;
        }
    }

    // ================================================================
    // Toggle commands
    // ================================================================

    [RelayCommand]
    private void ToggleThinking()
    {
        ThinkingEnabled = !ThinkingEnabled;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void ToggleMemory()
    {
        MemoryEnabled = !MemoryEnabled;
    }

    // ================================================================
    // Global commands (delegate to IThemeProvider / ISettingsRepository)
    // ================================================================

    [RelayCommand]
    private void IncreaseFont()
    {
        var current = _themeProvider.FontSize;
        if (current >= 24) return;
        _themeProvider.SetFontSettings(_themeProvider.FontFamily, current + 1, _themeProvider.FontWeight);
    }

    [RelayCommand]
    private void DecreaseFont()
    {
        var current = _themeProvider.FontSize;
        if (current <= 10) return;
        _themeProvider.SetFontSettings(_themeProvider.FontFamily, current - 1, _themeProvider.FontWeight);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var newTheme = _themeProvider.CurrentAppTheme == AppTheme.Light
            ? AppTheme.Dark
            : AppTheme.Light;
        _themeProvider.SetAppTheme(newTheme);
    }

    [RelayCommand]
    private void TogglePinWindow()
    {
        var current = System.Windows.Application.Current?.MainWindow;
        if (current is null) return;

        var isPinned = !current.Topmost;
        current.Topmost = isPinned;
        _ = _settingsRepo.SetAsync("PinWindow", isPinned ? "true" : "false");
    }

    // ================================================================
    // Auto-save drafts
    // ================================================================

    private void StartDraftTimer()
    {
        _draftTimerCts = new CancellationTokenSource();
        _draftTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RunDraftTimerAsync(_draftTimerCts.Token);
    }

    private async Task RunDraftTimerAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Capture timer locally to avoid null race with StopDraftTimer
                    var timer = _draftTimer;
                    if (timer is null)
                        break;

                    if (!await timer.WaitForNextTickAsync(ct))
                        break;

                    if (ActiveTab is null || string.IsNullOrWhiteSpace(ActiveTab.TextboxContent))
                        continue;

                    await _chatService.SaveDraftAsync(
                        ActiveTab.Thread.Id,
                        ActiveTab.TextboxContent,
                        ActiveTab.CursorPosition);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-save draft for tab '{ThreadId}'",
                        ActiveTab?.Thread.Id ?? "(null)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Draft timer encountered an unexpected error and will stop");
        }
    }

    private void StopDraftTimer()
    {
        _draftTimerCts?.Cancel();
        _draftTimer?.Dispose();
        _draftTimer = null;
        _draftTimerCts = null;
    }

    // ================================================================
    // Helpers
    // ================================================================

    private void UpdateContextAndCost()
    {
        // Context tokens and cost are computed from the service layer;
        // this is a placeholder that can be enriched when the service
        // exposes token/cost per-thread.
        var lastMsg = ActiveTab?.Messages.LastOrDefault();
        if (lastMsg?.EstimatedCost is not null)
            CumulativeCost += lastMsg.EstimatedCost.Value;
    }

    /// <summary>
    /// Called when the ViewModel is no longer needed to release resources.
    /// </summary>
    public void Cleanup()
    {
        StopDraftTimer();
        _activeCts?.Cancel();
        _activeCts?.Dispose();

        WeakReferenceMessenger.Default.Unregister<GenerationCompletedMessage>(this);
    }

    // ================================================================
    // System prompt assembly — delegated to SystemPromptCoordinator
    // ================================================================

    /// <summary>
    /// Resolves {{variables}} in a system prompt template.
    /// Delegates to <see cref="SystemPromptCoordinator.ResolveSystemPrompt"/> for variable replacement.
    /// Supported: {{date}}, {{time}}, {{user_name}}.
    /// </summary>
    public static string ResolveSystemPrompt(string template)
    {
        return SystemPromptCoordinator.ResolveSystemPrompt(template);
    }

    /// <summary>
    /// Build the additive system prompt for the current chat state.
    /// Delegates to <see cref="SystemPromptCoordinator"/>.
    /// </summary>
    public string? GetSystemPrompt(string workspacePath)
    {
        return _systemPromptCoordinator.GetSystemPrompt(
            ActivePersona?.SystemPrompt,
            EnabledToolNames,
            EnabledSkillNames,
            workspacePath);
    }

    /// <summary>
    /// Build the filtered list of tool names for the API tools array.
    /// Delegates to <see cref="SystemPromptCoordinator"/>.
    /// </summary>
    public IReadOnlyList<string> GetFilteredToolNames()
    {
        return SystemPromptCoordinator.GetFilteredToolNames(
            EnabledToolNames,
            EnabledSkillNames.Count);
    }

    /// <summary>
    /// Build the skill catalog XML block for the system prompt.
    /// Delegates to <see cref="SystemPromptCoordinator"/>.
    /// </summary>
    public string GetSkillCatalogXml()
    {
        return _systemPromptCoordinator.GetSkillCatalogXml(EnabledSkillNames);
    }
}
