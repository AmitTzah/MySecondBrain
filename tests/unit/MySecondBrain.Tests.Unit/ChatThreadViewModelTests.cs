using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class ChatThreadViewModelTests
{
    private readonly Mock<IChatThreadService> _chatServiceMock = new();
    private readonly Mock<IPersonaRepository> _personaRepoMock = new();
    private readonly Mock<IModelConfigurationRepository> _modelConfigRepoMock = new();
    private readonly Mock<ISettingsRepository> _settingsRepoMock = new();
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

        // Assert
        Assert.NotNull(vm.ActivePersona);
        Assert.Equal("General Assistant", vm.ActivePersona!.DisplayName);
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

        // Assert — recently-used tracking should have been triggered
        _settingsRepoMock.Verify(r => r.SetAsync("RecentPersonaIds",
            It.Is<List<string>>(ids => ids[0] == "custom-001")), Times.AtLeastOnce);
    }

    // ================================================================
    // Recently-used ordering places last-selected persona at top
    // ================================================================

    [Fact]
    public async Task RecentlyUsedOrdering_PlacesLastSelectedPersonaAtTop()
    {
        // Arrange — track stored recentIds locally to simulate persistence
        List<string>? storedRecentIds = null;

        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _modelConfigRepoMock.Setup(r => r.GetByIdAsync("config-002")).ReturnsAsync(_modelConfigB);
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds"))
            .ReturnsAsync(() => storedRecentIds);
        _settingsRepoMock.Setup(r => r.SetAsync("RecentPersonaIds", It.IsAny<List<string>>()))
            .Callback<string, List<string>>((_, ids) => storedRecentIds = ids)
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Initial state: General Assistant was selected as default → tracked as recent
        // PersonaList = [General Assistant (idx 0), Code Helper (int.MaxValue), Custom Writer (int.MaxValue)]
        // Then alphabetically: Code Helper, Custom Writer
        Assert.Equal("General Assistant", vm.PersonaList[0].DisplayName);
        Assert.Equal("Code Helper", vm.PersonaList[1].DisplayName);

        // Act — select a different persona
        await vm.SelectPersonaCommand.ExecuteAsync(_customPersona);

        // Assert — PersonaList should now have Custom Writer first (most recent), then General Assistant
        Assert.Equal("Custom Writer", vm.PersonaList[0].DisplayName);
        Assert.Equal("General Assistant", vm.PersonaList[1].DisplayName);

        // Assert — recently-used was persisted
        Assert.NotNull(storedRecentIds);
        Assert.Equal("custom-001", storedRecentIds[0]);
    }

    // ================================================================
    // Multiple recently-used IDs: most recent first
    // ================================================================

    [Fact]
    public async Task PersonaList_WithMultipleRecentIds_SortsMostRecentFirst()
    {
        // Arrange — simulate persistence for SetAsync calls
        List<string>? storedRecentIds = ["custom-001", "custom-002"];

        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona, _anotherPersona });
        // custom-001 (Custom Writer) is most recent (index 0), then custom-002 (Another Persona)
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds"))
            .ReturnsAsync(() => storedRecentIds);
        _settingsRepoMock.Setup(r => r.SetAsync("RecentPersonaIds", It.IsAny<List<string>>()))
            .Callback<string, List<string>>((_, ids) => storedRecentIds = ids)
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Init selects General Assistant and tracks it as most recent.
        // storedRecentIds becomes ["...0001", "custom-001", "custom-002"]
        // PersonaList: General Assistant (idx 0), Custom Writer (idx 1), Another Persona (idx 2), Code Helper (int.MaxValue)
        Assert.Equal(4, vm.PersonaList.Count);
        Assert.Equal("General Assistant", vm.PersonaList[0].DisplayName);
        Assert.Equal("Custom Writer", vm.PersonaList[1].DisplayName);
        Assert.Equal("Another Persona", vm.PersonaList[2].DisplayName);
        Assert.Equal("Code Helper", vm.PersonaList[3].DisplayName);
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
    // PersonaList is sorted with recently-used first
    // ================================================================

    [Fact]
    public async Task PersonaList_WithRecentIds_SortsRecentlyUsedFirst()
    {
        // Arrange — use tracking variable to simulate persistence
        List<string>? storedRecentIds = ["00000000000000000000000000000002"];

        _personaRepoMock.Setup(r => r.GetDefaultAsync()).ReturnsAsync(_generalAssistant);
        _personaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Persona> { _generalAssistant, _codeHelper, _customPersona });
        _settingsRepoMock.Setup(r => r.GetAsync<List<string>>("RecentPersonaIds"))
            .ReturnsAsync(() => storedRecentIds);
        _settingsRepoMock.Setup(r => r.SetAsync("RecentPersonaIds", It.IsAny<List<string>>()))
            .Callback<string, List<string>>((_, ids) => storedRecentIds = ids)
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Init selects General Assistant as default → tracks it as most recent.
        // storedRecentIds = ["...0001", "...0002"]
        // PersonaList: General Assistant (idx 0), Code Helper (idx 1), Custom Writer (int.MaxValue)
        Assert.Equal(3, vm.PersonaList.Count);
        Assert.Equal("General Assistant", vm.PersonaList[0].DisplayName);
        Assert.Equal("Code Helper", vm.PersonaList[1].DisplayName);
    }
}
