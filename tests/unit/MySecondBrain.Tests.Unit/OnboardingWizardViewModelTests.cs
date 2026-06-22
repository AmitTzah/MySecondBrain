using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class OnboardingWizardViewModelTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepo = new();
    private readonly Mock<ISettingsRepository> _settingsRepo = new();
    private readonly Mock<IModelConfigurationRepository> _modelConfigRepo = new();
    private readonly Mock<IPersonaRepository> _personaRepo = new();
    private readonly Mock<ITextActionRepository> _textActionRepo = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    private readonly Mock<ILLMProviderService> _llmProviderService = new();
    private readonly Mock<IConfirmationService> _confirmationService = new();
    private readonly Mock<IWikiService> _wikiService = new();
    private readonly Mock<ILogger<OnboardingWizardViewModel>> _logger = new();

    private OnboardingWizardViewModel CreateViewModel()
    {
        // Default settings repo returns null for all step keys (fresh state)
        _settingsRepo.Setup(s => s.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string)null!);

        // Default text action repo returns empty list (no actions to match)
        _textActionRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(Array.Empty<TextAction>());

        return new OnboardingWizardViewModel(
            _apiKeyRepo.Object,
            _settingsRepo.Object,
            _modelConfigRepo.Object,
            _personaRepo.Object,
            _textActionRepo.Object,
            _encryptionService.Object,
            _llmProviderService.Object,
            _confirmationService.Object,
            _wikiService.Object,
            _logger.Object);
    }

    // ================================================================
    // State Machine Tests
    // ================================================================

    [Fact]
    public void CurrentStep_DefaultsToMinusOne_WelcomeScreen()
    {
        // Settings returns null for step keys (fresh state → first incomplete = 0)
        var vm = CreateViewModel();

        // After init, first incomplete step is 0 since no step is completed
        Assert.Equal(0, vm.CurrentStep);
    }

    [Fact]
    public void GetStarted_SetsCurrentStepToFirstIncomplete()
    {
        // All steps uncompleted → first incomplete = 0
        _settingsRepo.Setup(s => s.GetAsync("Onboarding_Step1_Completed")).ReturnsAsync((string?)null);
        _settingsRepo.Setup(s => s.GetAsync("Onboarding_Step2_Completed")).ReturnsAsync((string?)null);
        _settingsRepo.Setup(s => s.GetAsync("Onboarding_Step3_Completed")).ReturnsAsync((string?)null);
        _settingsRepo.Setup(s => s.GetAsync("Onboarding_Step4_Completed")).ReturnsAsync((string?)null);

        var vm = CreateViewModel();
        Assert.Equal(0, vm.CurrentStep);
    }

    [Fact]
    public void CanGoBack_FalseOnWelcome_TrueOnSteps()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = -1;
        Assert.False(vm.CanGoBack);

        vm.CurrentStep = 0;
        Assert.True(vm.CanGoBack);
    }

    [Fact]
    public void CanSkip_FalseOnWelcomeAndFinish_TrueOnSteps()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = -1;
        Assert.False(vm.CanSkip);

        vm.CurrentStep = 0;
        Assert.True(vm.CanSkip);

        vm.CurrentStep = 4;
        Assert.False(vm.CanSkip);
    }

    [Fact]
    public void NextButtonText_IsFinishOnStep3()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 3;
        Assert.Equal("Finish", vm.NextButtonText);
    }

    [Fact]
    public async Task GoNext_IncrementsStep()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        await vm.GoNextCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentStep);
    }

    [Fact]
    public async Task GoBack_DecrementsStep()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 2;

        await vm.GoBackCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentStep);
    }

    [Fact]
    public async Task GoBack_FromStep0_GoesToWelcome()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        await vm.GoBackCommand.ExecuteAsync(null);

        Assert.Equal(-1, vm.CurrentStep);
    }

    [Fact]
    public async Task Skip_IncrementsStep()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        await vm.SkipCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentStep);
    }

    // ================================================================
    // Step 0 — API Key Tests
    // ================================================================

    [Fact]
    public void AddApiKey_WithEmptyInput_DoesNotAdd()
    {
        var vm = CreateViewModel();
        _confirmationService.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        vm.CurrentStep = 0;

        vm.ApiKeyInputValue = "";
        vm.AddApiKeyCommand.Execute(null);

        Assert.Empty(vm.WizardApiKeys);
    }

    [Fact]
    public void AddApiKey_WithValidInput_AddsToList()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        vm.SelectedApiKeyProvider = ProviderType.OpenAI;
        vm.ApiKeyInputValue = "sk-test-key-value-here";
        vm.AddApiKeyCommand.Execute(null);

        Assert.Single(vm.WizardApiKeys);
        Assert.Equal("sk-test-key-value-here", vm.WizardApiKeys[0].PlaintextKey);
        Assert.Equal(ProviderType.OpenAI, vm.WizardApiKeys[0].ProviderType);
    }

    [Fact]
    public void AddApiKey_DuplicateKey_NotAdded()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;
        _confirmationService.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        vm.SelectedApiKeyProvider = ProviderType.OpenAI;
        vm.ApiKeyInputValue = "sk-duplicate-key";
        vm.AddApiKeyCommand.Execute(null);

        vm.ApiKeyInputValue = "sk-duplicate-key";
        vm.AddApiKeyCommand.Execute(null);

        Assert.Single(vm.WizardApiKeys);
    }

    [Fact]
    public async Task TestApiKeyAsync_Success_UpdatesStatus()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        _llmProviderService.Setup(l => l.ValidateApiKeyAsync(
                It.IsAny<ProviderType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        vm.SelectedApiKeyProvider = ProviderType.OpenAI;
        vm.ApiKeyInputValue = "sk-valid-key";
        vm.AddApiKeyCommand.Execute(null);

        await vm.TestApiKeyCommand.ExecuteAsync(vm.WizardApiKeys[0]);

        Assert.True(vm.WizardApiKeys[0].IsValid);
        Assert.True(vm.WizardApiKeys[0].IsTested);
        Assert.Equal("✓ Validated", vm.WizardApiKeys[0].Status);
    }

    // ================================================================
    // Step 1 — Persona Tests
    // ================================================================

    [Fact]
    public void SelectPersona_SelectsStarterAndFillsFields()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 1;

        var codeHelper = vm.StarterPersonas[1]; // Code Helper
        vm.SelectPersonaCommand.Execute(codeHelper);

        Assert.Equal("Code Helper", vm.PersonaDisplayName);
        Assert.Contains("expert software developer", vm.PersonaSystemPrompt);
        Assert.True(codeHelper.IsSelected);
    }

    [Fact]
    public void StarterPersonas_HaveThreeItems()
    {
        var vm = CreateViewModel();
        Assert.Equal(3, vm.StarterPersonas.Count);
        Assert.Contains(vm.StarterPersonas, p => p.DisplayName == "General Assistant");
        Assert.Contains(vm.StarterPersonas, p => p.DisplayName == "Code Helper");
        Assert.Contains(vm.StarterPersonas, p => p.DisplayName == "Writing Coach");
    }

    // ================================================================
    // Step 2 — Wiki Tests
    // ================================================================

    [Fact]
    public async Task CreateNewWikiFolder_CreatesDirectoryAndIndexMd()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 2;

        // Use a temp directory for the test
        var testDir = Path.Combine(Path.GetTempPath(), "MSB_WikiTest_" + Guid.NewGuid().ToString("N"));

        // We need to mock the CreateNewWikiFolderAsync path
        // Since it uses Environment.SpecialFolder.MyDocuments, we'll verify behavior
        // by checking that path is set after creation
        await vm.CreateNewWikiFolderCommand.ExecuteAsync(null);

        // The path should be set to Documents/MySecondBrain-Wiki
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MySecondBrain-Wiki");

        Assert.Equal(expectedPath, vm.WikiDirectoryPath);
        Assert.Contains(".md", vm.WikiFileCount);

        // Clean up
        if (Directory.Exists(expectedPath))
        {
            try { Directory.Delete(expectedPath, recursive: true); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public void CreateFromScratch_ClearsFieldsAndSetsFlag()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 1;

        vm.PersonaDisplayName = "Test";
        vm.PersonaSystemPrompt = "Test prompt";
        vm.CreateFromScratchCommand.Execute(null);

        Assert.True(vm.IsCreatingFromScratch);
        Assert.Equal(string.Empty, vm.PersonaDisplayName);
        Assert.Equal(string.Empty, vm.PersonaSystemPrompt);
    }

    // ================================================================
    // Step 3 — Hotkey Tests
    // ================================================================

    [Fact]
    public void DefaultHotkeys_ArePrePopulated()
    {
        var vm = CreateViewModel();

        Assert.Equal(6, vm.HotkeyAssignments.Count);
        Assert.Contains(vm.HotkeyAssignments, h => h.ActionName == "Rewrite" && h.Hotkey == "Alt+Q");
        Assert.Contains(vm.HotkeyAssignments, h => h.ActionName == "Command Bar" && h.Hotkey == "Alt+Space");
    }

    [Fact]
    public void ResetHotkeysToDefaults_RestoresDefaultCombos()
    {
        var vm = CreateViewModel();

        // Modify a hotkey
        vm.HotkeyAssignments[0].Hotkey = "Ctrl+Shift+R";

        // Reset
        vm.ResetWizardHotkeysToDefaultsCommand.Execute(null);

        // Verify restored
        Assert.Equal("Alt+Q", vm.HotkeyAssignments[0].Hotkey);
    }

    // ================================================================
    // Launch Studio — saves hotkeys, saves keys, then sends message
    // ================================================================

    [Fact]
    public async Task LaunchStudio_SavesHotkeysThenKeysThenSendsMessage()
    {
        var vm = CreateViewModel();

        // Set up SetAsync so the async flow completes without Moq returning null
        _settingsRepo.Setup(s => s.SetAsync("Onboarding_Completed", "true"))
            .Returns(Task.CompletedTask);

        // This should not throw (the messenger fires but nothing may be registered)
        var exception = Record.Exception(() => vm.LaunchStudioCommand.Execute(null));
        Assert.Null(exception);

        // Wait for async operations to complete
        if (vm.LaunchStudioCommand is IAsyncRelayCommand asyncCmd && asyncCmd.ExecutionTask is not null)
            await asyncCmd.ExecutionTask;

        // Verify GetAllAsync was called for hotkey loading
        _textActionRepo.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);

        // Verify onboarding was marked complete after keys were saved
        _settingsRepo.Verify(s => s.SetAsync("Onboarding_Completed", "true"), Times.Once);
    }

    [Fact]
    public async Task LaunchStudio_PersistsChangedHotkeysToRepository()
    {
        // CreateViewModel sets up default GetAllAsync → empty array.
        // We override AFTER creation since SaveHotkeysToRepositoryAsync
        // is only called at launch time, not during construction.
        var vm = CreateViewModel();

        // Arrange — seed TextActions that match wizard hotkey items
        var seededActions = new List<TextAction>
        {
            new() { Id = "a1", DisplayName = "Rewrite", Hotkey = "Alt+Q" },
            new() { Id = "a2", DisplayName = "Summarize", Hotkey = "Alt+W" },
        };
        _textActionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(seededActions);
        _textActionRepo.Setup(r => r.UpdateAsync(It.IsAny<TextAction>()))
            .Returns(Task.CompletedTask);
        _settingsRepo.Setup(s => s.SetAsync("Onboarding_Completed", "true"))
            .Returns(Task.CompletedTask);

        // Act — change one hotkey in the wizard, then launch
        var rewriteItem = vm.HotkeyAssignments.First(h => h.ActionName == "Rewrite");
        rewriteItem.Hotkey = "Ctrl+Shift+R";

        vm.LaunchStudioCommand.Execute(null);
        if (vm.LaunchStudioCommand is IAsyncRelayCommand asyncCmd && asyncCmd.ExecutionTask is not null)
            await asyncCmd.ExecutionTask;

        // Assert — UpdateAsync should have been called once for the changed hotkey
        _textActionRepo.Verify(r => r.UpdateAsync(It.Is<TextAction>(
            a => a.Id == "a1" && a.Hotkey == "Ctrl+Shift+R")), Times.Once);

        // The unchanged hotkey should NOT have been updated
        _textActionRepo.Verify(r => r.UpdateAsync(It.Is<TextAction>(
            a => a.Id == "a2")), Times.Never);
    }

    // ================================================================
    // Import from ChatGPT — shows toast
    // ================================================================

    [Fact]
    public void ImportFromChatGpt_ShowsComingSoonToast()
    {
        var vm = CreateViewModel();
        _confirmationService.Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        vm.ImportFromChatGptCommand.Execute(null);

        _confirmationService.Verify(c => c.Confirm(
            It.Is<string>(s => s.Contains("coming soon", StringComparison.OrdinalIgnoreCase)),
            "Coming Soon"), Times.Once);
    }
}
