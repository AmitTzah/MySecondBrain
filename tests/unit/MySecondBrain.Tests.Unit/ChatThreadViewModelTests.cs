using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services;
using MySecondBrain.Services.Chat;
using MySecondBrain.Services.Encryption;
using MySecondBrain.UI.ViewModels;
// Resolve ambiguity with System.Windows.Forms.Message (UseWindowsForms=true)
using Message = MySecondBrain.Core.Models.Message;

// ReSharper disable PossibleNullReferenceException

namespace MySecondBrain.Tests.Unit;

public class ChatThreadViewModelTests
{
    private readonly Mock<IChatThreadService> _chatServiceMock = new();
    private readonly Mock<IPersonaRepository> _personaRepoMock = new();
    private readonly Mock<IModelConfigurationRepository> _modelConfigRepoMock = new();
    private readonly Mock<ISettingsRepository> _settingsRepoMock = new();
    private readonly Mock<ISkillService> _skillServiceMock = new();
    private readonly Mock<IConfirmationService> _confirmationServiceMock = new();
    private readonly Mock<IThemeProvider> _themeProviderMock = new();
    private readonly Mock<ILogger<ChatThreadViewModel>> _loggerMock = new();
    private readonly Mock<MarkdownStreamRenderer> _streamRendererMock = new(Mock.Of<IContentRendererRegistry>(), Mock.Of<ILogger<MarkdownStreamRenderer>>());
    private readonly Mock<LockedChatService> _lockedChatServiceMock = new(
        Mock.Of<IChatEncryptionService>(),
        Mock.Of<IChatThreadRepository>(),
        Mock.Of<IMessageRepository>(),
        Mock.Of<ILogger<LockedChatService>>());

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
            _confirmationServiceMock.Object,
            _themeProviderMock.Object,
            _loggerMock.Object,
            _streamRendererMock.Object,
            _lockedChatServiceMock.Object);
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

        // Assert
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

        // Assert
        _settingsRepoMock.Verify(r => r.SetAsync("LastSelectedPersonaId", "custom-001"), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_RestoresLastSelectedPersona()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync("custom-001");

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("Custom Writer", vm.ActivePersona!.DisplayName);
        Assert.Contains(vm.ActivePersona, vm.PersonaList);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToDefaultWhenSavedPersonaNotFound()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync("non-existent-id");

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
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
        vm.ActivePersona = _customPersona;

        // Wait for fire-and-forget OnActivePersonaChanged → SetActivePersonaAsync to complete
        await Task.Delay(100);

        // Assert
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

        // Assert
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

        // Act
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);

        // Assert
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

        var tools = vm.GetFilteredToolNames();

        Assert.Contains("ask_user_input", tools);
        Assert.DoesNotContain("skill_load", tools);
        Assert.Equal(8, tools.Count);
    }

    [Fact]
    public void GetFilteredToolNames_AllToolsDisabled_NoSkills_ReturnsEmpty()
    {
        var vm = CreateViewModel();

        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        var tools = vm.GetFilteredToolNames();

        Assert.Empty(tools);
    }

    [Fact]
    public void GetFilteredToolNames_AllToolsDisabled_WithSkills_IncludesAskUserInputAndSkillLoad()
    {
        var vm = CreateViewModel();

        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        vm.PopulateSkillToggles(new List<SkillMetadata>
        {
            new("xlsx", "Spreadsheet skill", "embedded", "Skills/anthropic/xlsx"),
        });

        var tools = vm.GetFilteredToolNames();

        Assert.Contains("ask_user_input", tools);
        Assert.Contains("skill_load", tools);
        Assert.Equal(2, tools.Count);
    }

    [Fact]
    public void GetFilteredToolNames_SkillLoadRemovedWhenNoSkills()
    {
        var vm = CreateViewModel();

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

        foreach (var tool in vm.ToolToggles)
            tool.IsEnabled = false;

        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>());

        var prompt = vm.GetSystemPrompt(@"C:\workspace");

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
        _skillServiceMock.Setup(s => s.GetCatalog()).Returns(new List<SkillMetadata>());

        var prompt = vm.GetSystemPrompt(@"C:\workspace");

        Assert.NotNull(prompt);
        Assert.Contains("Tools are called via function calling.", prompt);
        Assert.Contains("Current date:", prompt);
        Assert.Contains("You are running on Windows.", prompt);
        Assert.DoesNotContain("assistant", prompt);
    }

    [Fact]
    public void ResolveSystemPrompt_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SystemPromptBuilder.ResolveSystemPromptVariables(null));
    }

    // ================================================================
    // Tab management tests
    // ================================================================

    [Fact]
    public async Task NewChatAsync_CreatesTab()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, _generalAssistant))
            .ReturnsAsync(new ChatThread { Id = "thread-001", PersonaId = _generalAssistant.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        await vm.NewChatCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(vm.ChatTabs);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal("thread-001", vm.ActiveTab!.Thread.Id);
        Assert.Empty(vm.ActiveTab.TextboxContent);
        Assert.False(vm.ActiveTab.IsStreaming);
    }

    [Fact]
    public async Task NewChatAsync_WithoutPersona_DoesNothing()
    {
        // Arrange — no personas in the list
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(() => null);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona>());
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        await vm.NewChatCommand.ExecuteAsync(null);

        // Assert — no tab created since there's no persona
        Assert.Empty(vm.ChatTabs);
    }

    [Fact]
    public async Task NewChatAsync_CreatesMultipleTabs()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, _generalAssistant))
            .ReturnsAsync(() => new ChatThread { Id = Guid.NewGuid().ToString("N"), PersonaId = _generalAssistant.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act — create three tabs
        await vm.NewChatCommand.ExecuteAsync(null);
        await vm.NewChatCommand.ExecuteAsync(null);
        await vm.NewChatCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(3, vm.ChatTabs.Count);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal(vm.ChatTabs[2], vm.ActiveTab); // Last created is active
    }

    [Fact]
    public void CloseTab_RemovesTab()
    {
        // Arrange
        var thread1 = new ChatThread { Id = "thread-1" };
        var thread2 = new ChatThread { Id = "thread-2" };
        var tab1 = new ChatTabItem(thread1);
        var tab2 = new ChatTabItem(thread2);
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab1);
        vm.ChatTabs.Add(tab2);
        vm.ActiveTab = tab1;

        // Act
        vm.CloseTabCommand.Execute(tab1);

        // Assert
        Assert.Single(vm.ChatTabs);
        Assert.Equal("thread-2", vm.ChatTabs[0].Thread.Id);
        // ActiveTab should be null since tab1 was active and tab2 was not selected as replacement
    }

    [Fact]
    public void CloseTab_WithNullTab_DoesNothing()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.CloseTabCommand.Execute(null);

        // Assert — no exception
        Assert.Empty(vm.ChatTabs);
    }

    [Fact]
    public void CloseTab_ShowsConfirmationWhenStreaming()
    {
        // Arrange
        _confirmationServiceMock.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread) { IsStreaming = true };
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        // Act
        vm.CloseTabCommand.Execute(tab);

        // Assert — tab is NOT removed because confirmation was declined
        Assert.Single(vm.ChatTabs);
        _confirmationServiceMock.Verify(c => c.Confirm(
            It.Is<string>(s => s.Contains("generated")),
            "Generation in Progress"), Times.Once);
    }

    [Fact]
    public async Task CloseTab_PreservesForReopen()
    {
        // Arrange
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread);
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        // Act — close the tab
        vm.CloseTabCommand.Execute(tab);
        Assert.Empty(vm.ChatTabs);

        // Reopen via command
        _chatServiceMock.Setup(s => s.GetThreadAsync("thread-1"))
            .ReturnsAsync(thread);
        await vm.ReopenLastClosedTabCommand.ExecuteAsync(null);

        // Assert — tab is restored
        Assert.Single(vm.ChatTabs);
        Assert.Equal("thread-1", vm.ChatTabs[0].Thread.Id);
        Assert.Equal(vm.ChatTabs[0], vm.ActiveTab);
    }

    [Fact]
    public void ActiveTab_ResetsCompletionAlert()
    {
        // Arrange
        var thread1 = new ChatThread { Id = "thread-1" };
        var thread2 = new ChatThread { Id = "thread-2" };
        var tab1 = new ChatTabItem(thread1) { HasCompletionAlert = true };
        var tab2 = new ChatTabItem(thread2);
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab1);
        vm.ChatTabs.Add(tab2);
        vm.ActiveTab = tab1;

        // Assert — switching to tab1 resets its alert
        Assert.False(vm.ActiveTab!.HasCompletionAlert);
    }

    // ================================================================
    // Message sending tests
    // ================================================================

    [Fact]
    public async Task SendMessage_CallsService()
    {
        // Arrange
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread) { TextboxContent = "Hello" };
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        var userMessage = new Message { Id = "msg-1", Role = "User", Content = "Hello", ThreadId = "thread-1" };
        _chatServiceMock.Setup(s => s.SendMessageAsync("thread-1", "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMessage);
        _chatServiceMock.Setup(s => s.GetActiveBranchMessagesAsync("thread-1"))
            .ReturnsAsync(new List<Message> { userMessage });

        // Act
        await vm.SendMessageCommand.ExecuteAsync(null);

        // Assert
        _chatServiceMock.Verify(s => s.SendMessageAsync("thread-1", "Hello", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_ClearsTextboxContent()
    {
        // Arrange
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread) { TextboxContent = "Hello world" };
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        _chatServiceMock.Setup(s => s.SendMessageAsync("thread-1", "Hello world", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { Id = "msg-1", Role = "User", ThreadId = "thread-1" });
        _chatServiceMock.Setup(s => s.GetActiveBranchMessagesAsync("thread-1"))
            .ReturnsAsync(new List<Message>());

        // Act
        await vm.SendMessageCommand.ExecuteAsync(null);

        // Assert — textbox is cleared after sending
        Assert.Equal(string.Empty, tab.TextboxContent);
    }

    [Fact]
    public async Task SendMessage_WithEmptyText_DoesNotCallService()
    {
        // Arrange — textbox is empty
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread) { TextboxContent = "" };
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        // Act
        await vm.SendMessageCommand.ExecuteAsync(null);

        // Assert — service was never called
        _chatServiceMock.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithWhitespace_DoesNotCallService()
    {
        // Arrange
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread) { TextboxContent = "   " };
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        // Act
        await vm.SendMessageCommand.ExecuteAsync(null);

        // Assert
        _chatServiceMock.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithoutActiveTab_DoesNothing()
    {
        // Arrange — no active tab
        var vm = CreateViewModel();

        // Act — execute the command even though there's no active tab
        await vm.SendMessageCommand.ExecuteAsync(null);

        // Assert — no exception, service not called
        _chatServiceMock.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ================================================================
    // Stop generation tests
    // ================================================================

    [Fact]
    public async Task StopGeneration_CancelsActiveCts()
    {
        // Arrange
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread) { TextboxContent = "hello" };
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        CancellationToken capturedToken = default;
        _chatServiceMock.Setup(s => s.SendMessageAsync("thread-1", "hello", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) => capturedToken = ct)
            .Returns(async (string id, string content, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct);
                ct.ThrowIfCancellationRequested();
                return new Message();
            });

        _chatServiceMock.Setup(s => s.GetActiveBranchMessagesAsync("thread-1"))
            .ReturnsAsync(new List<Message>());

        // Act — trigger send then stop
        var sendTask = vm.SendMessageCommand.ExecuteAsync(null);
        // Give the send method time to start and create the CTS
        await Task.Delay(50);
        vm.StopGenerationCommand.Execute(null);

        // Assert — the captured token was cancelled
        Assert.True(capturedToken != default);
        Assert.True(capturedToken.IsCancellationRequested);

        // The send task should complete (with cancellation) after stop
        await sendTask;

        // Verify streaming flag was reset
        Assert.False(vm.ActiveTab!.IsStreaming);
    }

    // ================================================================
    // Toggle commands
    // ================================================================

    [Fact]
    public void ToggleThinking_UpdatesFlag()
    {
        var vm = CreateViewModel();

        Assert.False(vm.ThinkingEnabled);

        vm.ToggleThinkingCommand.Execute(null);
        Assert.True(vm.ThinkingEnabled);

        vm.ToggleThinkingCommand.Execute(null);
        Assert.False(vm.ThinkingEnabled);
    }

    [Fact]
    public void ToggleMute_UpdatesFlag()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsMuted);

        vm.ToggleMuteCommand.Execute(null);
        Assert.True(vm.IsMuted);

        vm.ToggleMuteCommand.Execute(null);
        Assert.False(vm.IsMuted);
    }

    [Fact]
    public void ToggleMemoryEnabled_UpdatesFlag()
    {
        var vm = CreateViewModel();

        Assert.False(vm.MemoryEnabled);

        vm.MemoryEnabled = true;
        Assert.True(vm.MemoryEnabled);
    }

    // ================================================================
    // IsStreaming property
    // ================================================================

    [Fact]
    public void IsStreaming_ReflectsActiveTabStreamingState()
    {
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "thread-1" };
        var tab = new ChatTabItem(thread);
        vm.ChatTabs.Add(tab);

        // No active tab → not streaming
        Assert.False(vm.IsStreaming);

        // Set active tab
        vm.ActiveTab = tab;
        Assert.False(vm.IsStreaming);

        // Tab starts streaming
        tab.IsStreaming = true;
        Assert.True(vm.IsStreaming);

        // Tab stops streaming
        tab.IsStreaming = false;
        Assert.False(vm.IsStreaming);
    }

    // ================================================================
    // ChatTabItem tests
    // ================================================================

    [Fact]
    public void ChatTabItem_Title_FromThreadTitle()
    {
        var thread = new ChatThread { Id = "t1", Title = "My Chat" };
        var tab = new ChatTabItem(thread);

        Assert.Equal("My Chat", tab.Title);
    }

    [Fact]
    public void ChatTabItem_Title_FallsBackToNewChat()
    {
        var thread = new ChatThread { Id = "t1", Title = null };
        var tab = new ChatTabItem(thread);

        Assert.Equal("New Chat", tab.Title);
    }

    [Fact]
    public void ChatTabItem_InitializesWithDefaults()
    {
        var thread = new ChatThread { Id = "t1" };
        var tab = new ChatTabItem(thread);

        Assert.Empty(tab.Messages);
        Assert.Empty(tab.TextboxContent);
        Assert.False(tab.IsStreaming);
        Assert.Equal(0, tab.CursorPosition);
        Assert.Equal(0.0, tab.ScrollOffset);
        Assert.False(tab.HasCompletionAlert);
    }

    // ================================================================
    // Tab switching tests
    // ================================================================

    [Fact]
    public void SwitchingActiveTab_ResetsCompletionAlert()
    {
        // Arrange
        var thread1 = new ChatThread { Id = "t1" };
        var thread2 = new ChatThread { Id = "t2" };
        var tab1 = new ChatTabItem(thread1) { HasCompletionAlert = true };
        var tab2 = new ChatTabItem(thread2);
        var vm = CreateViewModel();
        vm.ChatTabs.Add(tab1);
        vm.ChatTabs.Add(tab2);

        // Act — switch to tab1
        vm.ActiveTab = tab1;

        // Assert — alert is cleared when tab becomes active
        Assert.False(tab1.HasCompletionAlert);
    }

    // ================================================================
    // Existing regression: ActivePersona stays in sync
    // ================================================================

    [Fact]
    public void IsStreaming_PropertyChanged_RaisesNotification()
    {
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "t1" };
        var tab = new ChatTabItem(thread);
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        // Act
        tab.IsStreaming = true;

        // Assert
        Assert.Contains(nameof(vm.IsStreaming), changedProperties);
    }

    // ================================================================
    // Cleanup tests
    // ================================================================

    [Fact]
    public void Cleanup_DoesNotThrow()
    {
        var vm = CreateViewModel();

        // Act
        var exception = Record.Exception(() => vm.Cleanup());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Cleanup_AfterStreaming_DoesNotThrow()
    {
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "t1" };
        var tab = new ChatTabItem(thread);
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        // Act
        var exception = Record.Exception(() => vm.Cleanup());

        // Assert
        Assert.Null(exception);
    }

    // ================================================================
    // Step 7: Chat Header, Chat Modes, System Message Editor
    // ================================================================

    [Fact]
    public void SwitchChatMode_FromStandardToTextCompletion_WarnsAboutHistoryLoss()
    {
        // Arrange
        _confirmationServiceMock.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var vm = CreateViewModel();

        // Act
        vm.SwitchChatModeCommand.Execute(null);

        // Assert
        Assert.Equal(ChatMode.TextCompletion, vm.ChatMode);
        _confirmationServiceMock.Verify(c => c.Confirm(
            It.Is<string>(s => s.Contains("Text Completion")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void SwitchChatMode_FromTextCompletionToStandard_NoWarning()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ChatMode = ChatMode.TextCompletion;

        // Act
        vm.SwitchChatModeCommand.Execute(null);

        // Assert
        Assert.Equal(ChatMode.Standard, vm.ChatMode);
        // Switching from TextCompletion → Standard does not require confirmation
        _confirmationServiceMock.Verify(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void EditSystemMessage_PrePopulatesFromActivePersona()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync())
            .ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper });
        _settingsRepoMock.Setup(r => r.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync((string?)null);
        var vm = CreateViewModel();

        // Set active persona
        vm.SelectPersonaCommand.Execute(_generalAssistant);

        // Act
        vm.EditSystemMessageCommand.Execute(null);

        // Assert
        Assert.Equal(_generalAssistant.SystemPrompt, vm.EditingSystemMessage);
        Assert.True(vm.IsSystemMessageEditorOpen);
    }

    [Fact]
    public void SaveSystemMessage_StoresOnThread()
    {
        // Arrange
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "t1" };
        var tab = new ChatTabItem(thread);
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;
        vm.EditingSystemMessage = "Custom system prompt";

        // Act
        vm.SaveSystemMessageCommand.Execute(null);

        // Assert
        Assert.Equal("Custom system prompt", tab.Thread.SystemMessage);
        Assert.False(vm.IsSystemMessageEditorOpen);
    }

    [Fact]
    public void ResetSystemMessage_RestoresPersonaDefault()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectPersonaCommand.Execute(_generalAssistant);
        vm.EditingSystemMessage = "Modified prompt";

        // Act
        vm.ResetSystemMessageCommand.Execute(null);

        // Assert
        Assert.Equal(_generalAssistant.SystemPrompt, vm.EditingSystemMessage);
    }

    [Fact]
    public void ClearConversation_RemovesMessagesAndResetsCost()
    {
        // Arrange
        _confirmationServiceMock.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "t1" };
        var tab = new ChatTabItem(thread);
        tab.Messages.Add(new Message { Content = "Hello", Role = "user" });
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;
        vm.CumulativeCost = 0.42m;
        vm.ContextTokens = 150;

        // Act
        vm.ClearConversationCommand.Execute(null);

        // Assert
        Assert.Empty(tab.Messages);
        Assert.Equal(0, vm.CumulativeCost);
        Assert.Equal(0, vm.ContextTokens);
    }

    [Fact]
    public void ClearConversation_WithoutConfirmation_DoesNothing()
    {
        // Arrange
        _confirmationServiceMock.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "t1" };
        var tab = new ChatTabItem(thread);
        tab.Messages.Add(new Message { Content = "Hello", Role = "user" });
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        // Act
        vm.ClearConversationCommand.Execute(null);

        // Assert
        Assert.Single(tab.Messages);
    }

    [Fact]
    public void ToggleTemporary_MakesChatTemporary()
    {
        // Arrange
        var vm = CreateViewModel();
        var thread = new ChatThread { Id = "t1", IsTransient = false };
        var tab = new ChatTabItem(thread);
        vm.ChatTabs.Add(tab);
        vm.ActiveTab = tab;

        _chatServiceMock.Setup(s => s.SoftDeleteThreadAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        vm.ToggleTemporaryCommand.Execute(null);

        // Assert
        Assert.True(tab.Thread.IsTransient);
    }

    [Fact]
    public void PinWindow_TogglesTopmost()
    {
        // Arrange
        var vm = CreateViewModel();

        // We can't easily test MainWindow.Topmost in unit tests,
        // but the command should not throw
        var exception = Record.Exception(() => vm.TogglePinWindowCommand.Execute(null));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void ShowHelpCommands_DoNotThrow()
    {
        var vm = CreateViewModel();

        // These commands should never throw — they're informational
        Assert.Null(Record.Exception(() => vm.ShowAppDataLocationsCommand.Execute(null)));
        Assert.Null(Record.Exception(() => vm.ShowKeyboardShortcutsCommand.Execute(null)));
        Assert.Null(Record.Exception(() => vm.ShowAboutCommand.Execute(null)));
    }

    // ================================================================
    // Regression: Startup initialization creates first tab (Step 4 tab bar fix)
    // ================================================================

    [Fact]
    public async Task StartupFlow_InitializeThenNewChat_CreatesTab()
    {
        // Arrange — simulate full startup: InitializeAsync then NewChatCommand
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, _generalAssistant))
            .ReturnsAsync(new ChatThread { Id = "thread-startup-001", PersonaId = _generalAssistant.Id });

        var vm = CreateViewModel();

        // Act — this is the exact sequence MainWindow performs on Loaded
        await vm.InitializeAsync();
        if (vm.ChatTabs.Count == 0 && vm.ActivePersona is not null)
            await vm.NewChatCommand.ExecuteAsync(null);

        // Assert — tab bar must not be empty after startup
        Assert.NotEmpty(vm.ChatTabs);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal("thread-startup-001", vm.ActiveTab!.Thread.Id);
    }

    [Fact]
    public async Task StartupFlow_InitializeWithoutPersonas_LeavesEmptyTabs()
    {
        // Arrange — no personas exist (e.g., fresh database before onboarding)
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(() => null);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona>());
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);

        var vm = CreateViewModel();

        // Act — startup initialization
        await vm.InitializeAsync();

        // Assert — no tabs created (no persona available), but no crash either
        Assert.Empty(vm.ChatTabs);
        Assert.Null(vm.ActiveTab);
        Assert.Null(vm.ActivePersona);
    }

    // ================================================================
    // Tab isolation: each tab maintains its own persona
    // ================================================================

    [Fact]
    public async Task NewChatAsync_StoresPersonaOnTab()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, _generalAssistant))
            .ReturnsAsync(new ChatThread { Id = "thread-001", PersonaId = _generalAssistant.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        await vm.NewChatCommand.ExecuteAsync(null);

        // Assert — tab stores the persona separately from ViewModel
        Assert.NotNull(vm.ActiveTab);
        Assert.NotNull(vm.ActiveTab!.ActivePersona);
        Assert.Equal("General Assistant", vm.ActiveTab.ActivePersona!.DisplayName);
        // VM-level property should also match for the active tab
        Assert.Same(vm.ActivePersona, vm.ActiveTab.ActivePersona);
    }

    [Fact]
    public async Task Tabs_MaintainIndependentPersonas()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-001")).ReturnsAsync(_modelConfigA);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, It.IsAny<Persona>()))
            .ReturnsAsync((string? title, bool isTransient, Persona persona) =>
                new ChatThread { Id = Guid.NewGuid().ToString("N"), PersonaId = persona.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Create two tabs with default persona (General Assistant)
        await vm.NewChatCommand.ExecuteAsync(null);
        var tab1 = vm.ActiveTab!;
        Assert.Equal("General Assistant", tab1.ActivePersona!.DisplayName);

        // Create another tab — also defaults to General Assistant
        await vm.NewChatCommand.ExecuteAsync(null);
        var tab2 = vm.ActiveTab!;
        Assert.Equal("General Assistant", tab2.ActivePersona!.DisplayName);

        // Act — switch to tab1 and change its persona to Code Helper
        vm.ActiveTab = tab1;
        _settingsRepoMock.Invocations.Clear();
        await vm.SelectPersonaCommand.ExecuteAsync(_codeHelper);

        // Assert — tab1's persona changed
        Assert.Equal("Code Helper", tab1.ActivePersona!.DisplayName);

        // Switch to tab2 — its persona should still be General Assistant
        // The guard in OnActiveTabChanged must NOT save LastSelectedPersonaIdKey on tab switch
        _settingsRepoMock.Invocations.Clear();
        vm.ActiveTab = tab2;
        _settingsRepoMock.Verify(r => r.SetAsync("LastSelectedPersonaId", It.IsAny<string>()), Times.Never);
        Assert.Equal("General Assistant", tab2.ActivePersona!.DisplayName);
        // VM display property should reflect tab2's persona
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("General Assistant", vm.ActivePersona!.DisplayName);

        // Switch back to tab1 — VM display should restore Code Helper
        vm.ActiveTab = tab1;
        Assert.Equal("Code Helper", vm.ActivePersona!.DisplayName);
    }

    [Fact]
    public void ChatTabItem_PersonaDefaultsToNull()
    {
        // Arrange
        var thread = new ChatThread { Id = "t1" };

        // Act
        var tab = new ChatTabItem(thread);

        // Assert
        Assert.Null(tab.ActivePersona);
        Assert.Null(tab.ActiveModelConfig);
    }

    [Fact]
    public async Task SwitchingTabs_RestoresPerTabPersonaOnViewModel()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-001")).ReturnsAsync(_modelConfigA);
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, It.IsAny<Persona>()))
            .ReturnsAsync((string? title, bool isTransient, Persona persona) =>
                new ChatThread { Id = Guid.NewGuid().ToString("N"), PersonaId = persona.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Create tab1, set to Code Helper
        await vm.NewChatCommand.ExecuteAsync(null);
        var tab1 = vm.ActiveTab!;
        await vm.SelectPersonaCommand.ExecuteAsync(_codeHelper);
        Assert.Equal("Code Helper", tab1.ActivePersona!.DisplayName);

        // Create tab2 — should use current active persona (Code Helper) for creation,
        // then we'll change it to Custom Writer
        await vm.NewChatCommand.ExecuteAsync(null);
        var tab2 = vm.ActiveTab!;
        // tab2 inherits Code Helper from current active persona
        Assert.Equal("Code Helper", tab2.ActivePersona!.DisplayName);
        // Change tab2 to Custom Writer
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);

        // Act — switch back to tab1
        _settingsRepoMock.Invocations.Clear();
        vm.ActiveTab = tab1;

        // Assert — VM display property shows tab1's persona (Code Helper)
        Assert.Equal("Code Helper", vm.ActivePersona!.DisplayName);
        Assert.Equal("Code Helper", tab1.ActivePersona!.DisplayName);
        // tab2 unchanged
        Assert.Equal("Custom Writer", tab2.ActivePersona!.DisplayName);
        // Switching tabs must NOT save LastSelectedPersonaIdKey (guard in OnActiveTabChanged)
        _settingsRepoMock.Verify(r => r.SetAsync("LastSelectedPersonaId", It.IsAny<string>()), Times.Never);

        // Switch to tab2 — VM shows tab2's persona
        _settingsRepoMock.Invocations.Clear();
        vm.ActiveTab = tab2;
        Assert.Equal("Custom Writer", vm.ActivePersona!.DisplayName);
        _settingsRepoMock.Verify(r => r.SetAsync("LastSelectedPersonaId", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NewFileViewerTab_StoresPersonaOnTab()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync("test.txt", true, It.IsAny<Persona>()))
            .ReturnsAsync((string? title, bool isTransient, Persona persona) =>
                new ChatThread { Id = Guid.NewGuid().ToString("N"), PersonaId = persona.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        var fileVm = new FileViewerTabViewModel
        {
            FileName = "test.txt",
            FileContent = "hello world",
            FileType = FileViewerType.Text,
        };
        var tab = await vm.NewFileViewerTab(fileVm);

        // Assert
        Assert.NotNull(tab);
        Assert.NotNull(tab!.ActivePersona);
        Assert.Equal("General Assistant", tab.ActivePersona!.DisplayName);
        // VM-level property should also match
        Assert.Same(vm.ActivePersona, tab.ActivePersona);
    }

    [Fact]
    public async Task NewFileViewerTab_WithoutPersona_StoresNullOnTab()
    {
        // Arrange — no personas available (e.g., fresh database before onboarding)
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(() => null);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona>());
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act — no persona set, but NewFileViewerTab still creates a tab
        var fileVm = new FileViewerTabViewModel
        {
            FileName = "test.txt",
            FileContent = "hello world",
            FileType = FileViewerType.Text,
        };
        var tab = await vm.NewFileViewerTab(fileVm);

        // Assert — tab exists but has null persona
        Assert.NotNull(tab);
        Assert.Null(tab!.ActivePersona);
        Assert.Null(tab.ActiveModelConfig);
        // VM should also have null persona
        Assert.Null(vm.ActivePersona);
    }

    [Fact]
    public async Task DuplicateChatAsync_PreservesPersonaOnDuplicatedTab()
    {
        // Arrange
        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds")).ReturnsAsync(() => null);
        _settingsRepoMock.Setup(r => r.GetAsync("LastSelectedPersonaId")).ReturnsAsync((string?)null);
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, It.IsAny<Persona>()))
            .ReturnsAsync((string? title, bool isTransient, Persona persona) =>
                new ChatThread { Id = Guid.NewGuid().ToString("N"), PersonaId = persona.Id });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Create a tab and set its persona to Custom Writer
        await vm.NewChatCommand.ExecuteAsync(null);
        var sourceTab = vm.ActiveTab!;
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);
        Assert.Equal("Custom Writer", sourceTab.ActivePersona!.DisplayName);

        // Add a message to the source tab so DuplicateChatAsync has content to duplicate
        sourceTab.Messages.Add(new Message { Id = "msg-1", Role = "user", Content = "Hello", ThreadId = sourceTab.Thread.Id });
        _chatServiceMock.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string threadId, string content, CancellationToken ct) =>
                new Message { Id = Guid.NewGuid().ToString("N"), Role = "user", Content = content, ThreadId = threadId });

        // Mock the duplicate thread's message reload
        var duplicateThreadId = Guid.NewGuid().ToString("N");
        _chatServiceMock.Setup(s => s.CreateThreadAsync(null, false, _customPersona))
            .ReturnsAsync(new ChatThread { Id = duplicateThreadId, PersonaId = _customPersona.Id });
        _chatServiceMock.Setup(s => s.GetActiveBranchMessagesAsync(duplicateThreadId))
            .ReturnsAsync(new List<Message>());

        // Act
        await vm.DuplicateChatCommand.ExecuteAsync(null);

        // Assert — duplicated tab inherits the source tab's persona
        Assert.NotNull(vm.ActiveTab);
        Assert.NotSame(sourceTab, vm.ActiveTab);
        Assert.NotNull(vm.ActiveTab!.ActivePersona);
        Assert.Equal("Custom Writer", vm.ActiveTab.ActivePersona!.DisplayName);
    }
}
