using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

public partial class ChatThreadViewModel : ObservableObject
{
    private readonly IChatThreadService _chatService;
    private readonly IPersonaRepository _personaRepo;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly ILogger<ChatThreadViewModel> _logger;

    private const string RecentPersonaIdsKey = "RecentPersonaIds";
    private const string LastSelectedPersonaIdKey = "LastSelectedPersonaId";
    private const int MaxRecentPersonas = 5;

    /// <summary>
    /// Prevents re-entrant calls to SetActivePersonaAsync when ActivePersona
    /// is set from both the command path and the OnActivePersonaChanged path.
    /// </summary>
    private bool _isSettingActivePersona;

    public ChatThreadViewModel(
        IChatThreadService chatService,
        IPersonaRepository personaRepo,
        IModelConfigurationRepository modelConfigRepo,
        ISettingsRepository settingsRepo,
        ILogger<ChatThreadViewModel> logger)
    {
        _chatService = chatService;
        _personaRepo = personaRepo;
        _modelConfigRepo = modelConfigRepo;
        _settingsRepo = settingsRepo;
        _logger = logger;

        // Initialize per-chat tool/skill/memory toggles with global defaults
        InitializeToolToggles();
        InitializeSkillToggles();
        InitializeMemoryToggle();
    }

    // ================================================================
    // Per-chat toolbar toggle state
    // ================================================================

    /// <summary>Tools dropdown checkbox items.</summary>
    [ObservableProperty]
    private ObservableCollection<ToolToggleItem> _toolToggles = [];

    /// <summary>Skills dropdown checkbox items.</summary>
    [ObservableProperty]
    private ObservableCollection<SkillToggleItem> _skillToggles = [];

    /// <summary>Memory tool toggle (default OFF).</summary>
    [ObservableProperty]
    private bool _memoryEnabled;

    /// <summary>
    /// Returns the set of tool names that are currently enabled for this chat.
    /// Used by system prompt construction to filter the tools array.
    /// </summary>
    public IReadOnlySet<string> EnabledToolNames =>
        ToolToggles
            .Where(t => t.IsEnabled)
            .Select(t => t.Name)
            .ToHashSet();

    /// <summary>
    /// Returns the set of skill names that are currently enabled for this chat.
    /// Used by system prompt construction to filter the skill catalog.
    /// </summary>
    public IReadOnlySet<string> EnabledSkillNames =>
        SkillToggles
            .Where(s => s.IsEnabled)
            .Select(s => s.Name)
            .ToHashSet();

    /// <summary>
    /// Toggle all skills on or off.
    /// </summary>
    public void SetAllSkillsEnabled(bool enabled)
    {
        foreach (var skill in SkillToggles)
        {
            skill.IsEnabled = enabled;
        }
    }

    private void InitializeToolToggles()
    {
        // Global defaults are stored in Settings by key; load them if set.
        // All tools default to enabled when no global default exists.
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
                IsEnabled = true, // default: enabled; global override can be loaded later
            });
        }

        ToolToggles = new ObservableCollection<ToolToggleItem>(items);
    }

    private void InitializeSkillToggles()
    {
        // Skills are loaded from the skill service at chat initialization.
        // For now, initialize empty; skills will be populated when ISkillService
        // is available and discovery completes.
        SkillToggles = [];
    }

    /// <summary>
    /// Populate the skill toggle list from the discovered skill catalog.
    /// Called after skill discovery completes (e.g., during chat initialization).
    /// </summary>
    public void PopulateSkillToggles(IReadOnlyList<SkillMetadata> discoveredSkills)
    {
        var items = discoveredSkills
            .Select(s => new SkillToggleItem
            {
                Name = s.Name,
                Description = s.Description,
                IsEnabled = true, // default: enabled
            })
            .ToList();

        SkillToggles = new ObservableCollection<SkillToggleItem>(items);
    }

    private void InitializeMemoryToggle()
    {
        // Memory defaults to OFF per spec.
        // Global default can be loaded from settings later.
        MemoryEnabled = false;
    }

    // ================================================================
    // Observable properties
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

    /// <summary>
    /// Called by the generated setter whenever ActivePersona changes (e.g., ComboBox TwoWay binding).
    /// Ensures side effects (model config resolution, recently-used tracking, list refresh)
    /// happen regardless of whether the change originated from the command or the binding.
    ///
    /// The task is intentionally discarded (fire-and-forget) because this is a partial method
    /// invoked synchronously by the generated property setter — it cannot be async. The
    /// _isSettingActivePersona re-entrancy guard in SetActivePersonaAsync prevents concurrent
    /// invocations, and exceptions are caught internally by SetActivePersonaAsync.
    /// </summary>
    partial void OnActivePersonaChanged(Persona? value)
    {
        if (value is null || _isSettingActivePersona)
            return;

        _ = SetActivePersonaAsync(value);
    }

    // ================================================================
    // Initialization
    // ================================================================

    /// <summary>
    /// Called after construction to populate the persona list and restore the
    /// last-selected persona (or fall back to the default persona).
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Populate the list first, then set the active persona.
            // SetActivePersonaAsync no longer refreshes the list internally.
            await RefreshPersonaListAsync();

            // Attempt to restore the last-selected persona across sessions
            var savedPersonaId = await _settingsRepo.GetAsync(LastSelectedPersonaIdKey);
            Persona? personaToActivate = null;

            if (savedPersonaId is not null)
            {
                personaToActivate = PersonaList.FirstOrDefault(p => p.Id == savedPersonaId);
            }

            // Fall back to the default persona if the saved one is missing or not found
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
    // Persona list (alphabetically ordered, static)
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

            // Remove existing entry if present, then insert at front
            recentIds.Remove(personaId);
            recentIds.Insert(0, personaId);

            // Trim to max count
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
    // Commands
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

            // Re-map ActivePersona to the matching instance in PersonaList by ID.
            // PersonaList may contain different object references (e.g., populated
            // via RefreshPersonaListAsync before this method). Without this remap,
            // the ComboBox SelectedItem reference won't match any item in ItemsSource,
            // causing it to display empty/grey.
            if (PersonaList.Count > 0)
            {
                var match = PersonaList.FirstOrDefault(p => p.Id == persona.Id);
                if (match is not null)
                {
                    ActivePersona = match;
                    persona = match; // Use the remapped instance for all downstream operations
                }
            }

            // Update ActiveModelConfig from persona's default model config
            if (!string.IsNullOrEmpty(persona.DefaultModelConfigId))
            {
                var config = await _modelConfigRepo.GetByIdAsync(persona.DefaultModelConfigId);
                ActiveModelConfig = config;
            }
            else
            {
                ActiveModelConfig = null;
            }

            // Track recently-used (used by persona picker dialog, not ComboBox ordering)
            await TrackRecentPersonaAsync(persona.Id);

            // Persist the last-selected persona ID across sessions
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

    /// <summary>
    /// Opens the persona picker by preparing the filtered list and setting the flag.
    /// The actual dialog window is created by ChatView.
    /// </summary>
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

    /// <summary>
    /// Resolves {{variables}} in a system prompt template.
    /// Placeholder resolution for this step; {{date}}, {{time}}, {{user_name}} are supported.
    /// </summary>
    public static string ResolveSystemPrompt(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var now = DateTime.Now;
        return template
            .Replace("{{date}}", now.ToString("yyyy-MM-dd"))
            .Replace("{{time}}", now.ToString("HH:mm:ss"))
            .Replace("{{user_name}}", Environment.UserName);
    }
}
