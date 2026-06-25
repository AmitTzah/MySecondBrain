using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services;
using MySecondBrain.UI.ViewModels;

// ReSharper disable PossibleNullReferenceException

namespace MySecondBrain.Tests.Unit;

public class ChatThreadViewModelTests
{
    private readonly Mock<IChatThreadService> _chatServiceMock = new();
    private readonly Mock<IPersonaRepository> _personaRepoMock = new();
    private readonly Mock<IModelConfigurationRepository> _modelConfigRepoMock = new();
    private readonly Mock<ISettingsRepository> _settingsRepoMock = new();
    private readonly Mock<ISkillService> _skillServiceMock = new();
    private readonly Mock<ILogger<ChatThreadViewModel>> _loggerMock = new();

    private readonly Persona _generalAssistant = new()
    {
        Id = "00000000000000000000000000000001",
        DisplayName = "General Assistant",
        SystemPrompt = "You are a helpful, thoughtful assistant.",
        IsBuiltIn = true,
        DefaultChatMode = "Standard",
    };

    private readonly Persona _codeHelper = new()
    {
        Id = "00000000000000000000000000000002",
        DisplayName = "Code Helper",
        SystemPrompt = "You are an expert software developer.",
        IsBuiltIn = true,
        DefaultChatMode = "Standard",
        DefaultModelConfigId = "config-001",
    };

    private readonly Persona _customPersona = new()
    {
        Id = "custom-001",
        DisplayName = "Custom Writer",
        SystemPrompt = "You are a writer.",
        IsBuiltIn = false,
        DefaultChatMode = "TextCompletion",
        DefaultModelConfigId = "config-002",
    };

    private readonly Persona _anotherPersona = new()
    {
        Id = "custom-002",
        DisplayName = "Another Persona",
        SystemPrompt = "You are another assistant.",
        IsBuiltIn = false,
        DefaultChatMode = "Standard",
    };

    private readonly ModelConfiguration _modelConfigA = new()
    {
        Id = "config-001",
        DisplayName = "GPT-4o",
        ProviderType = ProviderType.OpenAI,
        ModelIdentifier = "gpt-4o",
    };

    private readonly ModelConfiguration _modelConfigB = new()
    {
        Id = "config-002",
        DisplayName = "Claude Sonnet",
        ProviderType = ProviderType.Anthropic,
        ModelIdentifier = "claude-sonnet-4-20250514",
    };

    private ChatThreadViewModel CreateViewModel()
    {
        return new ChatThreadViewModel(
            _chatServiceMock.Object,
            _personaRepoMock.Object,
            _modelConfigRepoMock.Object,
            _settingsRepoMock.Object,
            _skillServiceMock.Object,
            _loggerMock.Object);
    }

    // ================================================================
    // ActivePersona defaults to built-in persona (General Assistant)
    // ================================================================

    [Fact]
    public async Task InitializeAsync_SetsActivePersonaToDefaultBuiltInPersona()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert — ActivePersona must reference an object that exists in PersonaList
        // so the ComboBox SelectedItem matches an ItemsSource entry
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("General Assistant", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);
    }

    // ================================================================
    // Last-selected persona persistence across sessions
    // ================================================================

    [Fact]
    public async Task SelectPersonaAsync_PersistsLastSelectedPersonaId()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Clear invocations from initialization
        _settingsRepoMock.Invocations.Clear();

        // Act
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);

        // Assert — LastSelectedPersonaId should have been persisted exactly once
        _settingsRepoMock.Verify(r => r.SetAsync("LastSelectedPersonaId", "custom-001"), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_RestoresLastSelectedPersona()
    {
        // Arrange — return a saved persona ID that overrides the default
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync("custom-001");

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert — should restore Custom Writer, not the default General Assistant
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("Custom Writer", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToDefaultWhenSavedPersonaNotFound()
    {
        // Arrange — saved persona ID doesn't match any persona in the list
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync("non-existent-id");

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert — should fall back to the default persona
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("General Assistant", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);
    }

    // ================================================================
    // SelectPersonaCommand updates ActivePersona and ActiveModelConfig
    // ================================================================

    [Fact]
    public async Task SelectPersonaAsync_UpdatesActivePersonaAndActiveModelConfig()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("General Assistant", vm.ActivePersona!.DisplayName);

        // Act
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);

        // Assert
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("Custom Writer", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);
        Assert.NotNull(vm.ActiveModelConfig);
        Assert.Equal("Claude Sonnet", vm.ActiveModelConfig!.DisplayName);
    }

    [Fact]
    public async Task SelectPersonaAsync_WithNullPersona_DoesNotChangeActivePersona()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var originalPersona = vm.ActivePersona;

        // Act
        await vm.SelectPersonaCommand.ExecuteAsync(null);

        // Assert
        Assert.Same(originalPersona, vm.ActivePersona);
    }

    // ================================================================
    // Setting ActivePersona directly (ComboBox TwoWay binding) triggers side effects
    // ================================================================

    [Fact]
    public async Task SettingActivePersonaDirectly_TriggersModelConfigResolution()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Reset mock call counts
        _settingsRepoMock.Invocations.Clear();

        // Act — simulate ComboBox TwoWay binding setting ActivePersona directly
        vm.ActivePersona = _customPersona;

        // Wait for the fire-and-forget OnActivePersonaChanged to complete
        await Task.Delay(100);

        // Assert — model config should be resolved
        Assert.NotNull(vm.ActiveModelConfig);
        Assert.Equal("Claude Sonnet", vm.ActiveModelConfig!.DisplayName);

        // Assert — ActivePersona was remapped to an object in PersonaList
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("Custom Writer", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);

        // Assert — recently-used tracking should have been triggered
        _settingsRepoMock.Verify(r => r.SetAsync("RecentPersonaIds",
            It.Is<List<string>>(ids => ids[0] == "custom-001")), Times.AtLeastOnce);

        // Assert — last-selected persona should also persist via this path
        _settingsRepoMock.Verify(r => r.SetAsync("LastSelectedPersonaId", "custom-001"), Times.Once);
    }

    // ================================================================
    // Regression: ActivePersona stays in sync after ComboBox binding
    // ================================================================

    [Fact]
    public async Task SettingActivePersonaDirectly_ActivePersonaInSyncAfterSelection()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // After InitializeAsync, ActivePersona should point to an object in PersonaList
        Assert.NotNull(vm.ActivePersona);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);

        // Act — simulate ComboBox TwoWay binding setting ActivePersona to a different persona.
        // Use _customPersona (NOT from PersonaList) to verify the remap actually fires.
        vm.ActivePersona = _customPersona;

        // Wait for fire-and-forget OnActivePersonaChanged → SetActivePersonaAsync to complete
        await Task.Delay(100);

        // Assert — ActivePersona must be remapped to an object in PersonaList
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("Custom Writer", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);
    }

    // ================================================================
    // PersonaList is always sorted alphabetically (static order)
    // ================================================================

    [Fact]
    public async Task PersonaList_IsSortedAlphabetically()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Assert — alphabetical order regardless of selection
        Assert.Equal(3, vm.PersonaList.Count);
        Assert.Equal("Code Helper", vm.PersonaList[0].DisplayName);
        Assert.Equal("Custom Writer", vm.PersonaList[1].DisplayName);
        Assert.Equal("General Assistant", vm.PersonaList[2].DisplayName);
    }

    [Fact]
    public async Task PersonaList_RemainsAlphabeticallyOrderedAfterSelection()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona, _anotherPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act — select a different persona (should NOT reorder the list)
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);

        // Assert — list stays alphabetically ordered
        Assert.Equal(4, vm.PersonaList.Count);
        Assert.Equal("Another Persona", vm.PersonaList[0].DisplayName);
        Assert.Equal("Code Helper", vm.PersonaList[1].DisplayName);
        Assert.Equal("Custom Writer", vm.PersonaList[2].DisplayName);
        Assert.Equal("General Assistant", vm.PersonaList[3].DisplayName);
    }

    // ================================================================
    // PreparePersonaPickerCommand prepares filtered list
    // ================================================================

    [Fact]
    public async Task PreparePersonaPicker_PreparesFilteredList()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        vm.PreparePersonaPickerCommand.Execute(null);

        // Set search text and filter
        vm.PersonaPickerSearchText = "Code";
        vm.FilterPersonaPickerCommand.Execute(null);

        // Assert
        Assert.Single(vm.FilteredPersonaList);
        Assert.Equal("Code Helper", vm.FilteredPersonaList[0].DisplayName);
    }

    [Fact]
    public async Task FilterPersonaPicker_WithEmptySearch_ShowsAllPersonas()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.PreparePersonaPickerCommand.Execute(null);
        vm.PersonaPickerSearchText = "Code";
        vm.FilterPersonaPickerCommand.Execute(null);
        Assert.Single(vm.FilteredPersonaList);

        // Act — clear search
        vm.PersonaPickerSearchText = string.Empty;
        vm.FilterPersonaPickerCommand.Execute(null);

        // Assert
        Assert.Equal(3, vm.FilteredPersonaList.Count);
    }

    // ================================================================
    // Persona change resolves {{date}}/{{time}} in system prompt
    // ================================================================

    [Fact]
    public void ResolveSystemPrompt_ReplacesDateVariable()
    {
        var template = "Today is {{date}}.";
        var result = ChatThreadViewModel.ResolveSystemPrompt(template);
        var expectedDate = DateTime.Now.ToString("yyyy-MM-dd");
        Assert.Equal($"Today is {expectedDate}.", result);
    }

    [Fact]
    public void ResolveSystemPrompt_ReplacesTimeVariable()
    {
        var template = "The time is {{time}}.";
        var result = ChatThreadViewModel.ResolveSystemPrompt(template);
        var expectedTime = DateTime.Now.ToString("HH:mm:ss");
        Assert.Equal($"The time is {expectedTime}.", result);
    }

    [Fact]
    public void ResolveSystemPrompt_ReplacesUserNameVariable()
    {
        var template = "User: {{user_name}}";
        var result = ChatThreadViewModel.ResolveSystemPrompt(template);
        Assert.Equal($"User: {Environment.UserName}", result);
    }

    [Fact]
    public void ResolveSystemPrompt_ReplacesAllVariables()
    {
        var template = "{{date}} {{time}} {{user_name}}";
        var result = ChatThreadViewModel.ResolveSystemPrompt(template);
        var expectedDate = DateTime.Now.ToString("yyyy-MM-dd");
        var expectedTime = DateTime.Now.ToString("HH:mm:ss");
        Assert.Equal($"{expectedDate} {expectedTime} {Environment.UserName}", result);
    }

    [Fact]
    public void ResolveSystemPrompt_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ChatThreadViewModel.ResolveSystemPrompt(string.Empty));
    }

    // ================================================================
    // Per-chat toolbar toggles
    // ================================================================

    [Fact]
    public void Constructor_InitializesAllTenToolsEnabled()
    {
        var vm = CreateViewModel();

        Assert.Equal(10, vm.ToolToggles.Count);
        Assert.Contains(vm.ToolToggles, t => t.Name == "bash" && t.DisplayName == "Bash" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "text_editor" && t.DisplayName == "Text Editor" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "web_search" && t.DisplayName == "Web Search" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "web_fetch" && t.DisplayName == "Web Fetch" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "wiki_search" && t.DisplayName == "Wiki Search" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "memory" && t.DisplayName == "Memory" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "skill_load" && t.DisplayName == "Skill Load" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "ask_user_input" && t.DisplayName == "Ask User Input" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "present_files" && t.DisplayName == "Present Files" && t.IsEnabled);
        Assert.Contains(vm.ToolToggles, t => t.Name == "image_search" && t.DisplayName == "Image Search" && t.IsEnabled);
    }

    [Fact]
    public void Constructor_InitializesSkillsEmpty()
    {
        var vm = CreateViewModel();

        Assert.Empty(vm.SkillToggles);
    }

    [Fact]
    public void Constructor_MemoryDefaultsToOff()
    {
        var vm = CreateViewModel();

        Assert.False(vm.MemoryEnabled);
    }

    [Fact]
    public void PopulateSkillToggles_CreatesToggleForEachSkill()
    {
        var vm = CreateViewModel();
        var skills = new List<SkillMetadata>
        {
            new("xlsx", "Spreadsheet creation", "embedded", "Skills/anthropic/xlsx"),
            new("docx", "Document creation", "embedded", "Skills/anthropic/docx"),
            new("pdf", "PDF manipulation", "embedded", "Skills/anthropic/pdf"),
        };

        vm.PopulateSkillToggles(skills);

        Assert.Equal(3, vm.SkillToggles.Count);
        Assert.Contains(vm.SkillToggles, s => s.Name == "xlsx" && s.Description == "Spreadsheet creation" && s.IsEnabled);
        Assert.Contains(vm.SkillToggles, s => s.Name == "docx" && s.Description == "Document creation" && s.IsEnabled);
        Assert.Contains(vm.SkillToggles, s => s.Name == "pdf" && s.Description == "PDF manipulation" && s.IsEnabled);
    }

    [Fact]
    public void PopulateSkillToggles_EmptyList_ClearsCollection()
    {
        var vm = CreateViewModel();
        vm.PopulateSkillToggles(new List<SkillMetadata>());

        Assert.Empty(vm.SkillToggles);
    }

    [Fact]
    public void SetAllSkillsEnabled_True_EnablesAllSkills()
    {
        var vm = CreateViewModel();
        vm.PopulateSkillToggles(new List<SkillMetadata>
        {
            new("xlsx", "Test", "embedded", "path"),
            new("docx", "Test", "embedded", "path"),
        });

        // Disable one first
        vm.SkillToggles[0].IsEnabled = false;

        vm.SetAllSkillsEnabled(true);

        Assert.True(vm.SkillToggles[0].IsEnabled);
        Assert.True(vm.SkillToggles[1].IsEnabled);
    }

    [Fact]
    public void SetAllSkillsEnabled_False_DisablesAllSkills()
    {
        var vm = CreateViewModel();
        vm.PopulateSkillToggles(new List<SkillMetadata>
        {
            new("xlsx", "Test", "embedded", "path"),
            new("docx", "Test", "embedded", "path"),
        });

        vm.SetAllSkillsEnabled(false);

        Assert.False(vm.SkillToggles[0].IsEnabled);
        Assert.False(vm.SkillToggles[1].IsEnabled);
    }

    [Fact]
    public void EnabledToolNames_ReturnsOnlyEnabledToolNames()
    {
        var vm = CreateViewModel();

        // Disable one tool
        vm.ToolToggles.First(t => t.Name == "memory").IsEnabled = false;

        var enabled = vm.EnabledToolNames;

        Assert.DoesNotContain("memory", enabled);
        Assert.Contains("bash", enabled);
        Assert.Equal(9, enabled.Count);
    }

    [Fact]
    public void EnabledToolNames_AllDisabled_ReturnsEmpty()
    {
        var vm = CreateViewModel();

        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        Assert.Empty(vm.EnabledToolNames);
    }

    [Fact]
    public void EnabledSkillNames_ReturnsOnlyEnabledSkillNames()
    {
        var vm = CreateViewModel();
        vm.PopulateSkillToggles(new List<SkillMetadata>
        {
            new("xlsx", "Test", "embedded", "path"),
            new("docx", "Test", "embedded", "path"),
            new("pdf", "Test", "embedded", "path"),
        });

        vm.SkillToggles[1].IsEnabled = false;

        var enabled = vm.EnabledSkillNames;

        Assert.Contains("xlsx", enabled);
        Assert.DoesNotContain("docx", enabled);
        Assert.Contains("pdf", enabled);
        Assert.Equal(2, enabled.Count);
    }

    [Fact]
    public void ToggleMemoryEnabled_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.MemoryEnabled = true;

        Assert.Contains(nameof(vm.MemoryEnabled), changedProperties);
        Assert.True(vm.MemoryEnabled);
    }

    // ================================================================
    // Additive system prompt assembly
    // ================================================================

    [Fact]
    public void GetFilteredToolNames_AllEnabled_IncludesAskUserInput()
    {
        var vm = CreateViewModel();

        // All tools default to enabled (10 tools, including legacy text_editor).
        // text_editor is not in AllKnownToolNames so it's filtered out (→9).
        // skill_load is removed because no skills are enabled (→8).
        // ask_user_input is always present.
        var tools = vm.GetFilteredToolNames();

        Assert.Contains("ask_user_input", tools);
        Assert.DoesNotContain("skill_load", tools);
        Assert.Equal(8, tools.Count);
    }

    [Fact]
    public void GetFilteredToolNames_AllToolsDisabled_NoSkills_ReturnsEmpty()
    {
        var vm = CreateViewModel();

        // Disable all tools
        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        var tools = vm.GetFilteredToolNames();

        Assert.Empty(tools);
    }

    [Fact]
    public void GetFilteredToolNames_AllToolsDisabled_WithSkills_IncludesAskUserInputAndSkillLoad()
    {
        var vm = CreateViewModel();

        // Disable all tools
        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        // Add some skills (simulate enabled skills without populating toggles)
        // Since EnabledSkillNames depends on SkillToggles, we need to populate those
        vm.PopulateSkillToggles(new List<SkillMetadata>
        {
            new("xlsx", "Spreadsheet skill", "embedded", "Skills/anthropic/xlsx"),
        });

        var tools = vm.GetFilteredToolNames();

        // ask_user_input is always present
        Assert.Contains("ask_user_input", tools);
        // skill_load present because ≥1 skill enabled
        Assert.Contains("skill_load", tools);
        // No other tools
        Assert.Equal(2, tools.Count);
    }

    [Fact]
    public void GetFilteredToolNames_SkillLoadRemovedWhenNoSkills()
    {
        var vm = CreateViewModel();

        // skill_load defaults to enabled, but no skills are populated
        vm.ToolToggles.First(t => t.Name == "skill_load").IsEnabled = false;

        var tools = vm.GetFilteredToolNames();

        Assert.DoesNotContain("skill_load", tools);
    }

    [Fact]
    public void GetSystemPrompt_AllEnabled_ContainsAllSections()
    {
        var vm = CreateViewModel();
        vm.ActivePersona = _generalAssistant;
        vm.PopulateSkillToggles(new List<SkillMetadata>
        {
            new("xlsx", "Spreadsheet creation skill", "embedded", "path"),
        });

        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>
        {
            new("xlsx", "Spreadsheet creation skill", "embedded", "path"),
        });

        var prompt = vm.GetSystemPrompt(@"C:\Users\test\workspace");

        Assert.NotNull(prompt);
        Assert.Contains("You are a helpful, thoughtful assistant.", prompt);
        Assert.Contains("Tools are called via function calling.", prompt);
        Assert.Contains("Current date:", prompt);
        Assert.Contains("You are running on Windows.", prompt);
        Assert.Contains("workspace is at", prompt);
        Assert.Contains("<available_skills>", prompt);
        Assert.Contains("<name>xlsx</name>", prompt);
    }

    [Fact]
    public void GetSystemPrompt_EmptyPersonaNoSkillsWithTools_ReturnsNotNull()
    {
        var vm = CreateViewModel();
        vm.ActivePersona = new Persona
        {
            Id = "empty",
            DisplayName = "Empty",
            SystemPrompt = string.Empty,
            IsBuiltIn = true,
            DefaultChatMode = "Standard",
        };

        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>());

        var prompt = vm.GetSystemPrompt(@"C:\workspace");

        // Empty persona + no skills, but tools are still enabled.
        // Behavioral instructions, date/time, and platform context are always present.
        Assert.NotNull(prompt);
        Assert.Contains("Tools are called via function calling.", prompt);
        Assert.Contains("Current date:", prompt);
        Assert.Contains("You are running on Windows.", prompt);
        Assert.DoesNotContain("<available_skills>", prompt);
    }

    [Fact]
    public void GetSystemPrompt_EmptyPersonaEverythingDisabled_ReturnsNull()
    {
        var vm = CreateViewModel();
        vm.ActivePersona = new Persona
        {
            Id = "empty",
            DisplayName = "Empty",
            SystemPrompt = string.Empty,
            IsBuiltIn = true,
            DefaultChatMode = "Standard",
        };

        // Disable all tools
        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>());

        var prompt = vm.GetSystemPrompt(@"C:\workspace");

        // Edge case: empty persona + everything disabled = no system prompt
        Assert.Null(prompt);
    }

    [Fact]
    public void GetSkillCatalogXml_WithEnabledSkills_ReturnsXml()
    {
        var vm = CreateViewModel();
        var skills = new List<SkillMetadata>
        {
            new("xlsx", "Create/edit Excel spreadsheets", "embedded", "Skills/anthropic/xlsx"),
            new("docx", "Create/edit Word documents", "embedded", "Skills/anthropic/docx"),
        };

        vm.PopulateSkillToggles(skills);
        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(skills);

        var xml = vm.GetSkillCatalogXml();

        Assert.Contains("<available_skills>", xml);
        Assert.Contains("<name>xlsx</name>", xml);
        Assert.Contains("<description>Create/edit Excel spreadsheets</description>", xml);
        Assert.Contains("<name>docx</name>", xml);
        Assert.Contains("</available_skills>", xml);
    }

    [Fact]
    public void GetSkillCatalogXml_NoSkills_ReturnsEmpty()
    {
        var vm = CreateViewModel();
        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>());

        var xml = vm.GetSkillCatalogXml();

        Assert.Equal(string.Empty, xml);
    }

    [Fact]
    public void GetSkillCatalogXml_SomeSkillsDisabled_ExcludesDisabled()
    {
        var vm = CreateViewModel();
        var skills = new List<SkillMetadata>
        {
            new("xlsx", "Spreadsheet", "embedded", "path"),
            new("docx", "Document", "embedded", "path"),
            new("pdf", "PDF", "embedded", "path"),
        };

        vm.PopulateSkillToggles(skills);
        vm.SkillToggles[1].IsEnabled = false; // Disable docx

        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(skills);

        var xml = vm.GetSkillCatalogXml();

        Assert.Contains("<name>xlsx</name>", xml);
        Assert.DoesNotContain("<name>docx</name>", xml);
        Assert.Contains("<name>pdf</name>", xml);
    }

    [Fact]
    public void GetSkillCatalogXml_EscapesXmlEntitiesInDescriptions()
    {
        var vm = CreateViewModel();
        // Build the description with special XML chars that need entity escaping.
        // Use string concatenation to avoid HTML entity decoding by source tooling.
        var desc = "Search " + '\u0026' + " Rescue: x " + '\u003C' + " y, a " + '\u003E' + " b, \"quoted\", 'single'";
        var skills = new List<SkillMetadata>
        {
            new("special", desc, "embedded", "path"),
        };

        vm.PopulateSkillToggles(skills);
        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(skills);

        var xml = vm.GetSkillCatalogXml();

        var amp = '\u0026'; // & character
        Assert.Contains(amp + "amp;", xml);
        Assert.Contains(amp + "lt;", xml);
        Assert.Contains(amp + "gt;", xml);
        Assert.Contains(amp + "quot;", xml);
        Assert.Contains(amp + "apos;", xml);
    }

    [Fact]
    public void GetSystemPrompt_NullActivePersonaWithTools_ReturnsPrompt()
    {
        var vm = CreateViewModel();
        // ActivePersona is null by default (not initialized)
        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>());

        var prompt = vm.GetSystemPrompt(@"C:\workspace");

        // Null persona → no persona message. Tools enabled → behavioral/date/platform present.
        Assert.NotNull(prompt);
        Assert.Contains("Tools are called via function calling.", prompt);
        Assert.Contains("Current date:", prompt);
        Assert.Contains("You are running on Windows.", prompt);
        Assert.DoesNotContain("assistant", prompt); // No persona message
    }

    [Fact]
    public void ResolveSystemPrompt_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SystemPromptBuilder.ResolveSystemPromptVariables(null));
    }
}
