using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.Chat;
using MySecondBrain.Services.Encryption;
using MySecondBrain.UI.Services;
using MySecondBrain.UI.Views;
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
    private readonly LockedChatService _lockedChatService;
    private readonly ILogger<ChatThreadViewModel> _logger;

    /// <summary>
    /// Injected for future progressive streaming integration. When the service
    /// exposes IAsyncEnumerable of StreamChunk, this renderer will receive chunks
    /// and update the active message's FlowDocument incrementally instead of
    /// reloading all messages after completion.
    /// </summary>
    private readonly MarkdownStreamRenderer _streamRenderer;

    private const string RecentPersonaIdsKey = "RecentPersonaIds";
    private const string LastSelectedPersonaIdKey = "LastSelectedPersonaId";
    private const int MaxRecentPersonas = 5;

    /// <summary>
    /// Prevents re-entrant calls to SetActivePersonaAsync when ActivePersona
    /// is set from both the command path and the OnActivePersonaChanged path.
    /// </summary>
    private bool _isSettingActivePersona;

    /// <summary>
    /// Guards against redundant InitializeAsync calls. ChatView.Loaded fires
    /// each time WPF creates a new ChatView instance (on every tab switch),
    /// which would otherwise call SetActivePersonaAsync and overwrite the
    /// per-tab persona restored by OnActiveTabChanged.
    /// </summary>
    private bool _isInitialized;

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

    /// <summary>Tracks the placeholder message being progressively streamed.</summary>
    private Message? _streamingMessage;

    /// <summary>Guards against re-entrant streaming subscription.</summary>
    private bool _isStreamingSubscribed;

    /// <summary>Periodic DispatcherTimer that increments TimeRefreshToken to keep relative timestamps fresh.</summary>
    private System.Windows.Threading.DispatcherTimer? _timeRefreshTimer;

    public ChatThreadViewModel(
        IChatThreadService chatService,
        IPersonaRepository personaRepo,
        IModelConfigurationRepository modelConfigRepo,
        ISettingsRepository settingsRepo,
        ISkillService skillService,
        IConfirmationService confirmationService,
        IThemeProvider themeProvider,
        ILogger<ChatThreadViewModel> logger,
        MarkdownStreamRenderer streamRenderer,
        LockedChatService lockedChatService)
    {
        _chatService = chatService;
        _personaRepo = personaRepo;
        _modelConfigRepo = modelConfigRepo;
        _settingsRepo = settingsRepo;
        _skillService = skillService;
        _confirmationService = confirmationService;
        _themeProvider = themeProvider;
        _streamRenderer = streamRenderer;
        _lockedChatService = lockedChatService;
        _systemPromptCoordinator = new SystemPromptCoordinator(skillService);
        _logger = logger;

        // Initialize per-chat tool/skill/memory toggles with global defaults
        InitializeToolToggles();
        InitializeSkillToggles();
        InitializeMemoryToggle();

        // Subscribe to progressive stream chunks
        if (!_isStreamingSubscribed)
        {
            _chatService.OnStreamChunk += OnStreamChunkReceived;
            _isStreamingSubscribed = true;
        }

        // Register for cross-tab completion alerts
        WeakReferenceMessenger.Default.Register<GenerationCompletedMessage>(this, (r, m) =>
        {
            var tab = ChatTabs.FirstOrDefault(t => t.Thread.Id == m.Value);
            if (tab is not null && tab != ActiveTab)
                tab.HasCompletionAlert = true;
        });

        // Register for network availability changes
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        // Set initial network status
        _networkStatus = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()
            ? NetworkStatus.Online
            : NetworkStatus.Offline;

        // Periodic timer for refreshing relative timestamps (e.g., "just now" → "2 min ago").
        // IMultiValueConverter bindings on TimeRefreshToken force WPF to re-evaluate
        // the RelativeTimeConverter every 30 seconds without rebuilding the message list.
        _timeRefreshTimer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromSeconds(30),
            System.Windows.Threading.DispatcherPriority.Normal,
            (_, _) => TimeRefreshToken++,
            System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher);
        _timeRefreshTimer.Start();
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
        _logger.LogDebug(
            "[ThemeDiag] OnActiveTabChanged: newTab.Title={NewTitle}, newTab.ChatVisualTheme={NewTheme}, ChatTabs.Count={Count}",
            value?.Title ?? "(null)", value?.ChatVisualTheme.ToString() ?? "(null)", ChatTabs.Count);

        // When user clicks a tab from any screen, navigate to Chats
        if (value is not null && System.Windows.Application.Current?.MainWindow?.DataContext is MainWindowViewModel mainVm
            && mainVm.SelectedScreen != ScreenType.Chats)
        {
            mainVm.SelectedScreen = ScreenType.Chats;
        }

        // Reset completion alert when switching to a tab
        if (value is not null)
            value.HasCompletionAlert = false;

        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(IsTemporary));
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(TabStateIcon));

        // Restore per-tab state from the thread
        if (value?.Thread is not null)
        {
            // Restore chat mode from thread property
            if (Enum.TryParse<ChatMode>(value.Thread.ChatMode, out var mode))
                ChatMode = mode;

            // Restore system message
            if (!string.IsNullOrEmpty(value.Thread.SystemMessage))
                EditingSystemMessage = value.Thread.SystemMessage;

            // Restore thinking/mute state
            ThinkingEnabled = value.Thread.ThinkingEnabled;
            IsMuted = value.Thread.IsMuted;
        }

        // Restore per-tab persona and model config to ViewModel display properties
        // so the persona picker and header bar show the correct persona for this tab.
        // Suppress OnActivePersonaChanged to avoid re-saving LastSelectedPersonaIdKey
        // globally on every tab switch — this is a display restore, not a user selection.
        // Guard is wrapped in try/finally so any exception from the property setter
        // (e.g., from a PropertyChanged subscriber) does not permanently block future
        // persona changes.
        if (value is not null)
        {
            try
            {
                _isSettingActivePersona = true;
                ActivePersona = value.ActivePersona;
                ActiveModelConfig = value.ActiveModelConfig;
                // Restore per-tab chat visual theme
                CurrentChatVisualTheme = value.ChatVisualTheme;
            }
            finally
            {
                _isSettingActivePersona = false;
            }
        }

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

    /// <summary>
    /// Handles progressive stream chunks. Updates the active tab's last
    /// assistant message content incrementally so the user sees text
    /// appearing in real-time.
    /// </summary>
    private void OnStreamChunkReceived(StreamChunk chunk)
    {
        if (chunk.ContentDelta is null && !chunk.IsFinal)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var tab = ActiveTab;
            if (tab?.Thread is null) return;

            // First chunk with content — create or update placeholder message
            if (chunk.ContentDelta is not null)
            {
                if (_streamingMessage is null)
                {
                    _streamingMessage = new Message
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ThreadId = tab.Thread.Id,
                        Role = "Assistant",
                        Content = chunk.ContentDelta,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    tab.Messages.Add(_streamingMessage);
                }
                else
                {
                    // Append delta and replace item in collection to trigger UI refresh
                    _streamingMessage.Content += chunk.ContentDelta;
                    var idx = tab.Messages.IndexOf(_streamingMessage);
                    if (idx >= 0)
                    {
                        tab.Messages.RemoveAt(idx);
                        tab.Messages.Insert(idx, _streamingMessage);
                        StreamingContent = _streamingMessage.Content;
                    }
                }
            }

            // Final chunk — clear placeholder tracking
            if (chunk.IsFinal)
            {
                _streamingMessage = null;
                StreamingContent = string.Empty;
            }
        });
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

    /// <summary>
    /// Monotonically-increasing token incremented every 30 seconds by a
    /// DispatcherTimer. XAML MultiBindings on message timestamps include
    /// this token so the RelativeTimeConverter re-evaluates and keeps
    /// relative timestamps fresh (e.g., "just now" → "2 min ago").
    /// </summary>
    [ObservableProperty]
    private long _timeRefreshToken;

    // ================================================================
    // Error state properties
    // ================================================================

    /// <summary>True when the last send/regenerate/continue operation failed.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>Human-readable error message to display in the error banner.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Consecutive failure count — after 3, shows escalation message.</summary>
    [ObservableProperty]
    private int _consecutiveErrorCount;

    /// <summary>
    /// Stores the last failed operation context so Retry can re-execute it.
    /// Null when no retryable failure exists.
    /// </summary>
    private Func<Task>? _retryAction;

    // ================================================================
    // Scroll state properties
    // ================================================================

    /// <summary>True when the user has scrolled up during streaming (auto-scroll paused).</summary>
    [ObservableProperty]
    private bool _isScrolledUp;

    /// <summary>Text for the auto-scroll paused indicator.</summary>
    [ObservableProperty]
    private string _autoScrollIndicatorText = string.Empty;

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

    /// <summary>
    /// Per-tab chat visual theme (Classic/Compact/Bubble).
    /// Mirrors ActiveTab.ChatVisualTheme and restores on tab switch.
    /// When changed, updates the active tab's stored theme and notifies the UI.
    /// </summary>
    [ObservableProperty]
    private ChatTheme _currentChatVisualTheme = ChatTheme.Classic;

    partial void OnCurrentChatVisualThemeChanged(ChatTheme value)
    {
        _logger.LogDebug(
            "[ThemeDiag] ChatThreadViewModel.OnCurrentChatVisualThemeChanged: theme={Theme}, ActiveTab.Title={Title}, ChatTabs.Count={Count}",
            value, ActiveTab?.Title ?? "(null)", ChatTabs.Count);

        if (ActiveTab is not null)
        {
            ActiveTab.ChatVisualTheme = value;
            _logger.LogDebug(
                "[ThemeDiag] Stored ChatVisualTheme={Theme} on tab '{Title}'",
                value, ActiveTab.Title);
        }

        // Per-tab theme: ChatView listens to PropertyChanged on this VM for
        // nameof(CurrentChatVisualTheme) and calls ApplyChatTheme locally.
        // We do NOT call _themeProvider.SetChatTheme here because that fires
        // a global ChatThemeChanged event that would hit ALL ChatView instances.
        _logger.LogDebug("[ThemeDiag] Per-tab theme set (no global SetChatTheme call)");
    }

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
        // ChatView.Loaded fires each time WPF creates a new ChatView instance
        // (e.g., on every tab switch when the ContentPresenter recreates the
        // DataTemplate).  Without this guard, the second+ invocation would read
        // LastSelectedPersonaIdKey, call SetActivePersonaAsync, and overwrite
        // the per-tab persona that OnActiveTabChanged just restored.
        if (_isInitialized)
        {
            _logger.LogDebug("InitializeAsync skipped — already initialized");
            return;
        }

        try
        {
            // Initialize the per-tab theme from the globally persisted preference.
            // New tabs will inherit this value until the user changes it per-tab.
            if (_themeProvider.CurrentChatTheme != CurrentChatVisualTheme)
            {
                _logger.LogDebug("[ThemeDiag] InitializeAsync: seeding CurrentChatVisualTheme from global={Global}",
                    _themeProvider.CurrentChatTheme);
                CurrentChatVisualTheme = _themeProvider.CurrentChatTheme;
            }

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

            _isInitialized = true;
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

            // Apply persona's default chat mode
            if (Enum.TryParse<ChatMode>(persona.DefaultChatMode, out var personaMode))
                ChatMode = personaMode;

            // Sync the persona to the active tab so each tab remembers its own persona.
            // When the user switches away and back, OnActiveTabChanged restores it.
            if (ActiveTab is not null)
            {
                ActiveTab.ActivePersona = ActivePersona;
                ActiveTab.ActiveModelConfig = ActiveModelConfig;
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

    /// <summary>
    /// Creates a new chat tab. Called by the NewChat command and by MainWindow for file viewer tabs.
    /// </summary>
    [RelayCommand]
    private async Task NewChatAsync()
    {
        try
        {
            var persona = ActivePersona ?? PersonaList.FirstOrDefault();
            if (persona is null)
            {
                _logger.LogWarning("NewChatAsync: no persona available (ActivePersona={ActivePersona}, PersonaList.Count={Count})",
                    ActivePersona?.DisplayName ?? "null", PersonaList.Count);
                return;
            }

            _logger.LogDebug("NewChatAsync: creating thread with persona '{Persona}'", persona.DisplayName);
            var thread = await _chatService.CreateThreadAsync(null, false, persona);
            var tab = new ChatTabItem(thread);
            // Store the current persona, model config, and theme on the tab so each tab
            // maintains its own state independently.
            tab.ActivePersona = ActivePersona;
            tab.ActiveModelConfig = ActiveModelConfig;
            tab.ChatVisualTheme = CurrentChatVisualTheme;
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

        // If it's a file viewer tab, just remove it without thread operations
        if (tab.Thread is null)
        {
            _lastClosedTab = tab;
            var wasActiveFileTab = tab == ActiveTab;
            ChatTabs.Remove(tab);
            if (wasActiveFileTab)
                ActiveTab = ChatTabs.Count > 0 ? ChatTabs[^1] : null;
            if (ChatTabs.Count == 0)
                StopDraftTimer();
            return;
        }

        // Preserve for reopen
        _lastClosedTab = tab;

        // If the tab being closed is the active tab, we need to select a new one
        // or clear the active tab if no tabs remain
        var wasActive = tab == ActiveTab;

        ChatTabs.Remove(tab);

        if (wasActive)
        {
            // Select another tab if available, otherwise clear to show empty state
            ActiveTab = ChatTabs.Count > 0 ? ChatTabs[^1] : null;
        }

        // Stop draft timer when all tabs are closed
        if (ChatTabs.Count == 0)
            StopDraftTimer();

        _logger.LogDebug("Closed chat tab '{ThreadId}'", tab.Thread.Id);
    }

    [RelayCommand]
    private async Task ReopenLastClosedTabAsync()
    {
        if (_lastClosedTab is null) return;

        // Guard: file viewer tabs have null Thread — cannot reopen via service
        if (_lastClosedTab.Thread is null)
        {
            ChatTabs.Add(_lastClosedTab);
            ActiveTab = _lastClosedTab;
            _lastClosedTab = null;
            return;
        }

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
        var tab = ActiveTab;
        ActiveTab.TextboxContent = string.Empty;

        // Reset error state on new send
        HasError = false;
        ErrorMessage = string.Empty;

        tab.IsStreaming = true;

        var retryContent = content; // capture for retry
        _retryAction = () => SendWithStreamingAsync(tab, retryContent);

        try
        {
            await SendWithStreamingAsync(tab, content);
        }
        finally
        {
            tab.IsStreaming = false;
            _activeCts = null;
        }
    }

    /// <summary>
    /// Core streaming send logic shared by Send and Retry.
    /// </summary>
    private async Task SendWithStreamingAsync(ChatTabItem tab, string content)
    {
        // ── Add user message to UI immediately before the LLM call ──
        // This ensures the user sees their message right away, even while
        // the assistant response is still streaming. The service persists
        // the user message to the database inside SendMessageAsync, and
        // the full reload below replaces this in-memory copy with the
        // DB-persisted version (same content, with proper IDs).
        var userMessage = new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = tab.Thread.Id,
            Role = "User",
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        tab.Messages.Add(userMessage);

        try
        {
            using var cts = new CancellationTokenSource();
            _activeCts = cts;

            var message = await _chatService.SendMessageAsync(tab.Thread.Id, content, cts.Token);

            // If the user switched tabs during streaming, skip the reload
            if (tab != ActiveTab) return;

            // Load messages for the active branch
            var messages = await _chatService.GetActiveBranchMessagesAsync(tab.Thread.Id);
            tab.Messages = new ObservableCollection<Message>(messages);

            // Update aggregate state
            UpdateContextAndCost();

            // Clear retry on success
            _retryAction = null;
            ConsecutiveErrorCount = 0;

            // Notify cross-tab
            WeakReferenceMessenger.Default.Send(
                new GenerationCompletedMessage(tab.Thread.Id));
        }
        catch (OperationCanceledException)
        {
            // Partial response preserved by the service
            if (tab == ActiveTab)
            {
                var messages = await _chatService.GetActiveBranchMessagesAsync(tab.Thread.Id);
                tab.Messages = new ObservableCollection<Message>(messages);
            }
            _logger.LogDebug("Message generation cancelled — partial response preserved");

            // Clear retry on cancellation (user chose to stop)
            _retryAction = null;
            ConsecutiveErrorCount = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            ConsecutiveErrorCount++;
            HasError = true;
            ErrorMessage = GetUserFacingErrorMessage(ex);
        }
    }

    [RelayCommand]
    private void StopGeneration()
    {
        _activeCts?.Cancel();
        // The OperationCanceledException handler in SendWithStreamingAsync
        // preserves partial response
        HasError = false;
        ErrorMessage = string.Empty;
    }

    // ================================================================
    // Copy commands
    // ================================================================

    /// <summary>
    /// Copy raw Markdown content to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyMd(Message? message)
    {
        if (message is null || string.IsNullOrEmpty(message.Content))
            return;

        try
        {
            System.Windows.Clipboard.SetText(message.Content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy MD to clipboard");
        }
    }

    /// <summary>
    /// Copy rich HTML (and plain text) to clipboard so recipients
    /// like Word, Outlook, or rich text editors get formatted content.
    /// Markdig conversion is run off the UI thread to avoid freezing for large messages.
    /// </summary>
    [RelayCommand]
    private async Task CopyRichAsync(Message? message)
    {
        if (message is null || string.IsNullOrEmpty(message.Content))
            return;

        try
        {
            // Run Markdig conversion off the UI thread to avoid freezing for large messages
            var html = await Task.Run(() => Markdown.ToHtml(message.Content));

            var dataObject = new System.Windows.DataObject();
            dataObject.SetData(System.Windows.DataFormats.Html, HtmlClipboardHelper.WrapHtml(html));
            dataObject.SetData(System.Windows.DataFormats.Text, message.Content);
            System.Windows.Clipboard.SetDataObject(dataObject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy rich content to clipboard");
        }
    }

    // ================================================================
    // Retry command — re-executes the last failed operation
    // ================================================================

    [RelayCommand]
    private async Task RetryAsync()
    {
        if (_retryAction is null) return;

        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            await _retryAction();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry failed");
            HasError = true;
            ErrorMessage = GetUserFacingErrorMessage(ex);
        }
    }

    // ================================================================
    // Error helpers
    // ================================================================

    /// <summary>
    /// Maps exception types to user-friendly error messages.
    /// </summary>
    private static string GetUserFacingErrorMessage(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx when httpEx.StatusCode is System.Net.HttpStatusCode.TooManyRequests
                => "Rate limit exceeded. Please wait a moment and try again.",
            HttpRequestException httpEx when httpEx.StatusCode is System.Net.HttpStatusCode.Unauthorized
                => "Invalid API key. Please check your API key in Settings.",
            HttpRequestException httpEx when httpEx.StatusCode is System.Net.HttpStatusCode.PaymentRequired
                => "Insufficient credits or quota exceeded.",
            HttpRequestException
                => "Network error. Please check your internet connection and try again.",
            TaskCanceledException
                => "Request timed out. The server took too long to respond.",
            InvalidOperationException
                => ex.Message,
            _ => $"An unexpected error occurred: {ex.Message}"
        };
    }

    // Note: MessageTokenDisplayConverter (in Converters/) handles per-message
    // token/cost/time display via IValueConverter. The static method was removed
    // in favor of the converter to avoid logic duplication.

    [RelayCommand]
    private async Task RegenerateAsync()
    {
        if (ActiveTab is null || ActiveTab.Messages.Count == 0)
            return;

        if (ActiveTab.IsStreaming) return;

        var lastMsg = ActiveTab.Messages[^1];
        if (lastMsg.Role != "assistant") return;

        var tab = ActiveTab;
        tab.IsStreaming = true;

        try
        {
            using var cts = new CancellationTokenSource();
            _activeCts = cts;

            await _chatService.RegenerateAsync(lastMsg.Id, cts.Token);

            // If the user switched tabs during streaming, skip the reload
            if (tab != ActiveTab) return;

            // Reload messages
            var messages = await _chatService.GetActiveBranchMessagesAsync(tab.Thread.Id);
            tab.Messages = new ObservableCollection<Message>(messages);
            UpdateContextAndCost();

            // Notify cross-tab
            WeakReferenceMessenger.Default.Send(
                new GenerationCompletedMessage(ActiveTab.Thread.Id));
        }
        catch (OperationCanceledException)
        {
            if (tab == ActiveTab)
            {
                var messages = await _chatService.GetActiveBranchMessagesAsync(tab.Thread.Id);
                tab.Messages = new ObservableCollection<Message>(messages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate message");
            HasError = true;
            ErrorMessage = GetUserFacingErrorMessage(ex);
        }
        finally
        {
            tab.IsStreaming = false;
            _activeCts = null;
        }
    }

    [RelayCommand]
    private async Task ContinueGenerationAsync()
    {
        if (ActiveTab is null) return;

        if (ActiveTab.IsStreaming) return;

        var tab = ActiveTab;
        tab.IsStreaming = true;

        try
        {
            using var cts = new CancellationTokenSource();
            _activeCts = cts;

            await _chatService.ContinueGenerationAsync(tab.Thread.Id, cts.Token);

            // If the user switched tabs during streaming, skip the reload
            if (tab != ActiveTab) return;

            var messages = await _chatService.GetActiveBranchMessagesAsync(tab.Thread.Id);
            tab.Messages = new ObservableCollection<Message>(messages);
            UpdateContextAndCost();

            // Notify cross-tab
            WeakReferenceMessenger.Default.Send(
                new GenerationCompletedMessage(ActiveTab.Thread.Id));
        }
        catch (OperationCanceledException)
        {
            if (tab == ActiveTab)
            {
                var messages = await _chatService.GetActiveBranchMessagesAsync(tab.Thread.Id);
                tab.Messages = new ObservableCollection<Message>(messages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to continue generation");
            HasError = true;
            ErrorMessage = GetUserFacingErrorMessage(ex);
        }
        finally
        {
            tab.IsStreaming = false;
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

    /// <summary>
    /// Syncs the <see cref="ThinkingEnabled"/> toggle to the active tab's thread
    /// so the state persists across tab switches.
    /// </summary>
    partial void OnThinkingEnabledChanged(bool value)
    {
        if (ActiveTab?.Thread is not null)
        {
            ActiveTab.Thread.ThinkingEnabled = value;
            _logger.LogDebug("ThinkingEnabled changed to {Value} for thread '{ThreadId}'",
                value, ActiveTab.Thread.Id);
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    /// <summary>
    /// Syncs the <see cref="IsMuted"/> toggle to the active tab's thread
    /// so the state persists across tab switches.
    /// </summary>
    partial void OnIsMutedChanged(bool value)
    {
        if (ActiveTab?.Thread is not null)
        {
            ActiveTab.Thread.IsMuted = value;
            _logger.LogDebug("IsMuted changed to {Value} for thread '{ThreadId}'",
                value, ActiveTab.Thread.Id);
        }
    }

    [RelayCommand]
    private void ToggleMemory()
    {
        MemoryEnabled = !MemoryEnabled;
    }

    // ================================================================
    // Chat mode properties
    // ================================================================

    /// <summary>
    /// Current chat mode: Standard (conversation history sent) or
    /// TextCompletion (raw prompt sent without context).
    /// </summary>
    [ObservableProperty]
    private ChatMode _chatMode = ChatMode.Standard;

    /// <summary>
    /// Returns a display label for the current chat mode.
    /// </summary>
    public string ChatModeDisplay => ChatMode == ChatMode.Standard ? "Standard" : "Text";

    partial void OnChatModeChanged(ChatMode value)
    {
        OnPropertyChanged(nameof(ChatModeDisplay));
    }

    // ================================================================
    // System message editor state (E5)
    // ================================================================

    /// <summary>True when the system message editor popover is open.</summary>
    [ObservableProperty]
    private bool _isSystemMessageEditorOpen;

    /// <summary>
    /// The editable system message text currently shown in the editor popover.
    /// Pre-populated from the active persona or thread's custom system message.
    /// </summary>
    [ObservableProperty]
    private string _editingSystemMessage = string.Empty;

    // ================================================================
    // Source context properties (for [Apply] button shell)
    // ================================================================

    /// <summary>
    /// True when the active chat has source context (e.g., from a text action
    /// or attached document). Controls visibility of the source banner.
    /// </summary>
    [ObservableProperty]
    private bool _hasSourceContext;

    /// <summary>
    /// Display text for the source context banner, e.g. "[Source: Word — 'document.docx']".
    /// </summary>
    [ObservableProperty]
    private string? _sourceContextText;

    // ================================================================
    // Temporary/incognito chat state
    // ================================================================

    /// <summary>
    /// True when the active chat is temporary (🕶️ indicator).
    /// Read from ActiveTab.Thread.IsTransient on tab switch.
    /// </summary>
    public bool IsTemporary => ActiveTab?.Thread.IsTransient ?? false;

    /// <summary>Indicator icon for the tab based on state.</summary>
    public string TabStateIcon => IsTemporary ? "🕶️" : (IsLocked ? "🔒" : string.Empty);

    /// <summary>True when the active chat is locked.</summary>
    public bool IsLocked => ActiveTab?.Thread.IsLocked ?? false;

    // ================================================================
    // Network status (C19)
    // ================================================================

    [ObservableProperty]
    private NetworkStatus _networkStatus = NetworkStatus.Online;

    partial void OnNetworkStatusChanged(NetworkStatus value)
    {
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(IsOnline));
    }

    /// <summary>True when the network is unavailable.</summary>
    public bool IsOffline => NetworkStatus == NetworkStatus.Offline;

    /// <summary>True when the network is available.</summary>
    public bool IsOnline => NetworkStatus == NetworkStatus.Online;

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            NetworkStatus = e.IsAvailable ? NetworkStatus.Online : NetworkStatus.Offline;
        });
    }

    // ================================================================
    // Message favoriting (C33)
    // ================================================================

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Message? message)
    {
        if (message is null) return;

        try
        {
            message.IsFavorited = !message.IsFavorited;
            // Persist the IsFavorited flag by editing the message with same content
            await _chatService.EditMessageAsync(message.Id, message.Content, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle favorite for message '{MessageId}'", message.Id);
        }
    }

    // ================================================================
    // Message selection mode (C18)
    // ================================================================

    [ObservableProperty]
    private bool _isSelectionMode;

    [ObservableProperty]
    private ObservableCollection<Message> _selectedMessages = [];

    /// <summary>Number of currently selected messages.</summary>
    public int SelectedCount => SelectedMessages.Count;

    partial void OnSelectedMessagesChanged(ObservableCollection<Message> value)
    {
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Creates a file viewer tab from a FileViewerTabViewModel and adds it to the tab list.
    /// Called by MainWindow on Ctrl+O.
    /// </summary>
    public async Task<ChatTabItem?> NewFileViewerTab(FileViewerTabViewModel fileVm)
    {
        try
        {
            var persona = ActivePersona ?? PersonaList.FirstOrDefault();
            if (persona is null)
            {
                // Create a dummy thread for the file viewer tab
                var thread = new ChatThread
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = fileVm.FileName,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastActivityAt = DateTimeOffset.UtcNow,
                    IsTransient = true
                };

                var tab = new ChatTabItem(thread);
                // Store the current persona, model config, and theme on the tab
                tab.ActivePersona = ActivePersona;
                tab.ActiveModelConfig = ActiveModelConfig;
                tab.ChatVisualTheme = CurrentChatVisualTheme;
                ChatTabs.Add(tab);
                ActiveTab = tab;

                // Populate file content as a system message
                if (!string.IsNullOrEmpty(fileVm.FileContent))
                {
                    var contentMsg = new Message
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ThreadId = thread.Id,
                        Role = "system",
                        Content = fileVm.FileContent,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    tab.Messages.Add(contentMsg);
                }

                // Start auto-save timer on first tab creation
                if (_draftTimer is null)
                    StartDraftTimer();

                _logger.LogDebug("Opened file viewer tab for '{FileName}' ({Type})",
                    fileVm.FileName, fileVm.FileType);
                return tab;
            }

            var chatThread = await _chatService.CreateThreadAsync(fileVm.FileName, true, persona);
            var chatTab = new ChatTabItem(chatThread);
            // Store the current persona, model config, and theme on the tab
            chatTab.ActivePersona = ActivePersona;
            chatTab.ActiveModelConfig = ActiveModelConfig;
            chatTab.ChatVisualTheme = CurrentChatVisualTheme;
            ChatTabs.Add(chatTab);
            ActiveTab = chatTab;

            // Populate file content as a system message
            if (!string.IsNullOrEmpty(fileVm.FileContent))
            {
                var contentMsg = new Message
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ThreadId = chatThread.Id,
                    Role = "system",
                    Content = fileVm.FileContent,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                chatTab.Messages.Add(contentMsg);
            }

            if (_draftTimer is null)
                StartDraftTimer();

            _logger.LogDebug("Opened file viewer tab for '{FileName}' ({Type})",
                fileVm.FileName, fileVm.FileType);
            return chatTab;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create file viewer tab");
            return null;
        }
    }

    /// <summary>
    /// Toggle selection of a specific message. Called via checkbox in the message template.
    /// </summary>
    [RelayCommand]
    private void ToggleMessageSelection(Message? message)
    {
        if (message is null) return;

        if (SelectedMessages.Contains(message))
            SelectedMessages.Remove(message);
        else
            SelectedMessages.Add(message);

        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Enter or exit message selection mode.
    /// </summary>
    [RelayCommand]
    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;
        if (!IsSelectionMode)
        {
            SelectedMessages.Clear();
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    /// <summary>
    /// Delete all selected messages.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedMessagesAsync()
    {
        var toDelete = SelectedMessages.ToList();
        foreach (var msg in toDelete)
        {
            try
            {
                await _chatService.DeleteMessageAsync(msg.Id);
                ActiveTab?.Messages.Remove(msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete selected message '{MessageId}'", msg.Id);
            }
        }
        SelectedMessages.Clear();
        OnPropertyChanged(nameof(SelectedCount));
        IsSelectionMode = false;
    }

    /// <summary>
    /// Copy all selected messages as Markdown.
    /// </summary>
    [RelayCommand]
    private void CopySelectedMessages()
    {
        if (SelectedMessages.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var msg in SelectedMessages)
        {
            sb.AppendLine($"## {msg.Role}");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy selected messages to clipboard");
        }
    }

    /// <summary>
    /// Quote selected messages into the textbox.
    /// </summary>
    [RelayCommand]
    private void QuoteSelectedMessages()
    {
        if (SelectedMessages.Count == 0 || ActiveTab is null) return;

        var sb = new StringBuilder();
        foreach (var msg in SelectedMessages)
        {
            sb.AppendLine($"> **{msg.Role}**: {msg.Content}");
            sb.AppendLine();
        }

        ActiveTab.TextboxContent = sb.ToString() + ActiveTab.TextboxContent;
        IsSelectionMode = false;
        SelectedMessages.Clear();
        OnPropertyChanged(nameof(SelectedCount));
    }

    // ================================================================
    // Lock/Unlock chat (C31)
    // ================================================================

    [RelayCommand]
    private async Task LockChatAsync()
    {
        if (ActiveTab is null) return;

        try
        {
            var dialog = new LockedChatPasswordDialog(
                new LockedChatViewModel(ActiveTab.Thread.Id, true));
            dialog.Owner = System.Windows.Application.Current?.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var vm = (LockedChatViewModel)dialog.DataContext;
                await _lockedChatService.LockChatAsync(ActiveTab.Thread.Id, vm.Password);
                ActiveTab.Thread.IsLocked = true;
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(TabStateIcon));
                _logger.LogInformation("Chat '{ThreadId}' locked", ActiveTab.Thread.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock chat");
        }
    }

    [RelayCommand]
    private async Task UnlockChatAsync()
    {
        if (ActiveTab is null) return;

        try
        {
            var dialog = new LockedChatPasswordDialog(
                new LockedChatViewModel(ActiveTab.Thread.Id, false));
            dialog.Owner = System.Windows.Application.Current?.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var vm = (LockedChatViewModel)dialog.DataContext;
                await _lockedChatService.UnlockChatAsync(ActiveTab.Thread.Id, vm.Password);

                // Reload messages
                var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
                ActiveTab.Messages = new ObservableCollection<Message>(messages);
                ActiveTab.Thread.IsLocked = false;
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(TabStateIcon));
                _logger.LogInformation("Chat '{ThreadId}' unlocked", ActiveTab.Thread.Id);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Incorrect password attempt for chat '{ThreadId}'", ActiveTab.Thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock chat");
        }
    }

    // ================================================================
    // Chat mode command — switches between Standard and TextCompletion
    // ================================================================

    [RelayCommand]
    private void SwitchChatMode()
    {
        if (ChatMode == ChatMode.TextCompletion)
        {
            // Switching back to Standard — no warning needed
            ChatMode = ChatMode.Standard;
            _logger.LogDebug("Switched to Standard chat mode");
        }
        else
        {
            // Switching to TextCompletion — warn about history loss
            var confirmed = _confirmationService.Confirm(
                "Switching to Text Completion mode will not send conversation history. " +
                "Only the current prompt will be sent to the model. Continue?",
                "Switch Chat Mode");
            if (!confirmed) return;

            ChatMode = ChatMode.TextCompletion;
            _logger.LogDebug("Switched to Text Completion chat mode");
        }
    }

    // ================================================================
    // Three-dot menu commands (C16a)
    // ================================================================

    [RelayCommand]
    private async Task OpenApiHistory()
    {
        try
        {
            var historyPath = MySecondBrain.Services.LLM.ApiHistoryHelper.GetHistoryPath();

            // Ensure the file exists
            if (!System.IO.File.Exists(historyPath))
            {
                await System.IO.File.WriteAllTextAsync(historyPath, "[]");
            }

            var fileVm = await FileViewerTabViewModel.FromFileAsync(historyPath);
            await NewFileViewerTab(fileVm);
            _logger.LogInformation("Opened API history: {Path}", historyPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open API history");
        }
    }

    [RelayCommand]
    private void ClearConversation()
    {
        if (ActiveTab is null) return;

        var confirmed = _confirmationService.Confirm(
            "Are you sure you want to clear this conversation? All messages will be removed.",
            "Clear Conversation");
        if (!confirmed) return;

        try
        {
            ActiveTab.Messages.Clear();
            CumulativeCost = 0;
            ContextTokens = 0;
            _logger.LogDebug("Cleared conversation for thread '{ThreadId}'", ActiveTab.Thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear conversation");
        }
    }

    [RelayCommand]
    private async Task ExportChatAsync()
    {
        if (ActiveTab is null) return;

        try
        {
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            if (messages.Count == 0)
            {
                _logger.LogWarning("No messages to export");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# {ActiveTab.Thread.Title ?? "Exported Chat"}");
            sb.AppendLine();
            foreach (var msg in messages)
            {
                sb.AppendLine($"## {msg.Role}");
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }

            var exportPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"chat-export-{ActiveTab.Thread.Id[..8]}.md");
            await System.IO.File.WriteAllTextAsync(exportPath, sb.ToString());
            _logger.LogDebug("Exported chat to {Path}", exportPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export chat");
        }
    }

    [RelayCommand]
    private async Task DuplicateChatAsync()
    {
        if (ActiveTab is null) return;

        try
        {
            var persona = ActivePersona ?? PersonaList.FirstOrDefault();
            if (persona is null) return;

            var newThread = await _chatService.CreateThreadAsync(
                null, false, persona);
            if (newThread is null) return;

            // Copy messages without invoking LLM — just clone the in-memory content
            var sourceMessages = ActiveTab.Messages.ToList();
            foreach (var msg in sourceMessages)
            {
                var clonedMessage = new Message
                {
                    ThreadId = newThread.Id,
                    Role = msg.Role,
                    Content = msg.Content,
                    ModelName = msg.ModelName,
                    EstimatedCost = msg.EstimatedCost,
                    PromptTokens = msg.PromptTokens,
                    CompletionTokens = msg.CompletionTokens,
                    TotalTokens = msg.TotalTokens,
                    GenerationTimeMs = msg.GenerationTimeMs,
                };
                await _chatService.SendMessageAsync(newThread.Id, msg.Content, CancellationToken.None);
            }

            // Reload messages from the service for the new tab
            var newMessages = await _chatService.GetActiveBranchMessagesAsync(newThread.Id);
            var tab = new ChatTabItem(newThread);
            tab.Messages = new ObservableCollection<Message>(newMessages);
            // Preserve the source tab's persona and theme on the duplicated tab
            tab.ActivePersona = ActivePersona;
            tab.ActiveModelConfig = ActiveModelConfig;
            tab.ChatVisualTheme = CurrentChatVisualTheme;
            ChatTabs.Add(tab);
            ActiveTab = tab;
            _logger.LogDebug("Duplicated chat to thread '{ThreadId}'", newThread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate chat");
        }
    }

    [RelayCommand]
    private void ShowChatTree()
    {
        // Stub — full implementation in Feature 14 (Message Branching)
        _logger.LogDebug("Chat tree requested (stub)");
    }

    [RelayCommand]
    private void EditSystemMessage()
    {
        // Pre-populate the editor: thread's saved custom message first, then persona default
        EditingSystemMessage = ActiveTab?.Thread?.SystemMessage
            ?? ActivePersona?.SystemPrompt
            ?? string.Empty;
        IsSystemMessageEditorOpen = true;
        _logger.LogDebug("Opened system message editor");
    }

    [RelayCommand]
    private void SaveSystemMessage()
    {
        if (ActiveTab is null) return;

        ActiveTab.Thread.SystemMessage = EditingSystemMessage;
        _logger.LogDebug("Saved system message for thread '{ThreadId}'", ActiveTab.Thread.Id);
        IsSystemMessageEditorOpen = false;
    }

    [RelayCommand]
    private void ResetSystemMessage()
    {
        // Reset to persona default
        EditingSystemMessage = ActivePersona?.SystemPrompt ?? string.Empty;
        _logger.LogDebug("Reset system message to persona default");
    }

    [RelayCommand]
    private async Task SummarizeChatAsync()
    {
        if (ActiveTab is null) return;

        try
        {
            var messages = await _chatService.GetActiveBranchMessagesAsync(ActiveTab.Thread.Id);
            if (messages.Count == 0)
            {
                _logger.LogWarning("No messages to summarize");
                return;
            }

            // Build conversation summary and insert as a system message
            var sb = new StringBuilder();
            sb.AppendLine("📝 **Chat Summary**");
            sb.AppendLine();
            sb.AppendLine($"**Messages**: {messages.Count}");
            sb.AppendLine($"**First message**: {messages[0].CreatedAt:g}");
            sb.AppendLine($"**Last message**: {messages[^1].CreatedAt:g}");
            sb.AppendLine();

            var userMsgs = messages.Count(m => m.Role == "user");
            var assistantMsgs = messages.Count(m => m.Role == "assistant");
            sb.AppendLine($"**User messages**: {userMsgs}");
            sb.AppendLine($"**Assistant messages**: {assistantMsgs}");

            // Show first few user messages as summary preview
            var previewMsgs = messages.Where(m => m.Role == "user").Take(3).ToList();
            if (previewMsgs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Preview**:");
                foreach (var msg in previewMsgs)
                {
                    var preview = msg.Content?.Length > 100
                        ? msg.Content[..100] + "..."
                        : msg.Content;
                    sb.AppendLine($"> {preview}");
                }
            }

            var summaryMsg = new Message
            {
                Id = Guid.NewGuid().ToString("N"),
                ThreadId = ActiveTab.Thread.Id,
                Role = "system",
                Content = sb.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            ActiveTab.Messages.Add(summaryMsg);
            _logger.LogDebug("Chat summary generated for thread '{ThreadId}' ({Count} messages)",
                ActiveTab.Thread.Id, messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize chat");
        }
    }

    [RelayCommand]
    private async Task ToggleTemporaryAsync()
    {
        if (ActiveTab is null) return;

        try
        {
            var isCurrentlyTransient = ActiveTab.Thread.IsTransient;
            if (isCurrentlyTransient)
            {
                await _chatService.ElevateToPermanentAsync(ActiveTab.Thread.Id);
                ActiveTab.Thread.IsTransient = false;
                _logger.LogDebug("Made chat permanent");
            }
            else
            {
                // Soft-delete any existing messages, mark as transient
                await _chatService.SoftDeleteThreadAsync(ActiveTab.Thread.Id);
                ActiveTab.Thread.IsTransient = true;
                ActiveTab.Messages.Clear();
                _logger.LogDebug("Made chat temporary");
            }

            OnPropertyChanged(nameof(IsTemporary));
            OnPropertyChanged(nameof(TabStateIcon));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle temporary state");
        }
    }

    // ================================================================
    // Help menu commands
    // ================================================================

    [RelayCommand]
    private void ShowAppDataLocations()
    {
        _logger.LogDebug("App Data Locations requested — navigation handled by MainWindowViewModel");
    }

    [RelayCommand]
    private void ShowKeyboardShortcuts()
    {
        _logger.LogDebug("Keyboard shortcuts requested");
    }

    [RelayCommand]
    private void ShowAbout()
    {
        _logger.LogDebug("About dialog requested");
    }

    // ================================================================
    // Global commands (delegate to IThemeProvider / ISettingsRepository)
    // ================================================================

    /// <summary>
    /// Monotonically increasing version number incremented on every font size change.
    /// ChatView subscribes to PropertyChanged for this property and refreshes the
    /// message ListBox so the converter re-runs with the new font size.
    /// </summary>
    [ObservableProperty]
    private int _fontSizeVersion;

    [RelayCommand]
    private void IncreaseFont()
    {
        var current = _themeProvider.FontSize;
        if (current >= 24) return;
        _themeProvider.SetFontSettings(_themeProvider.FontFamily, current + 1, _themeProvider.FontWeight);
        FontSizeVersion++;
        NotifyFontSizeChanged();
    }

    [RelayCommand]
    private void DecreaseFont()
    {
        var current = _themeProvider.FontSize;
        if (current <= 10) return;
        _themeProvider.SetFontSettings(_themeProvider.FontFamily, current - 1, _themeProvider.FontWeight);
        FontSizeVersion++;
        NotifyFontSizeChanged();
    }

    /// <summary>
    /// Updates MainWindowViewModel.FontSizeDisplay so the font size label
    /// in ChatHeaderBar (bound to Window DataContext) shows the current value.
    /// Falls back silently if no MainWindow is available (e.g. in unit tests).
    /// </summary>
    private void NotifyFontSizeChanged()
    {
        if (System.Windows.Application.Current?.MainWindow?.DataContext is MainWindowViewModel mainVm)
        {
            mainVm.FontSizeDisplay = _themeProvider.FontSize.ToString("F0");
        }
        else
        {
            _logger.LogDebug("NotifyFontSizeChanged: MainWindow.DataContext is not MainWindowViewModel — FontSizeDisplay not updated");
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var newTheme = _themeProvider.CurrentAppTheme == AppTheme.Light
            ? AppTheme.Dark
            : AppTheme.Light;
        _themeProvider.SetAppTheme(newTheme);

        // Update ThemeToggleIcon on MainWindowViewModel so the ChatHeaderBar button
        // Content binding (Window ancestor) reflects the theme change.
        if (System.Windows.Application.Current?.MainWindow?.DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ThemeToggleIcon = newTheme == AppTheme.Dark ? "🌙" : "☀";
        }
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

        if (_isStreamingSubscribed)
        {
            _chatService.OnStreamChunk -= OnStreamChunkReceived;
            _isStreamingSubscribed = false;
        }

        WeakReferenceMessenger.Default.Unregister<GenerationCompletedMessage>(this);
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
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
