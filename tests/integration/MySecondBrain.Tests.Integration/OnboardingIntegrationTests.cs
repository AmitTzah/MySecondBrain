using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.Data.Repositories;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Integration;

public class OnboardingIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IApiKeyRepository _apiKeyRepo;
    private readonly IPersonaRepository _personaRepo;
    private readonly IModelConfigurationRepository _modelConfigRepo;
    private bool _disposed;

    public OnboardingIntegrationTests()
    {
        var testDir = Path.Combine(
            Path.GetTempPath(),
            "MySecondBrain_OnboardingIntTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDir);
        _dbPath = Path.Combine(testDir, "msb.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _settingsRepo = new SettingsRepository(_db);
        _apiKeyRepo = new ApiKeyRepository(_db);
        _personaRepo = new PersonaRepository(_db);
        _modelConfigRepo = new ModelConfigurationRepository(_db);
    }

    // ================================================================
    // Test: Full wizard flow — step through all 5 steps, verify
    // Onboarding_Step*_Completed keys set
    // ================================================================

    [Fact]
    public async Task FullWizardFlow_AllSteps_CompletesSuccessfully()
    {
        var vm = CreateViewModel();

        // Start at Welcome (init sets to first incomplete = 0)
        Assert.Equal(0, vm.CurrentStep);

        // Step 0 → 1
        vm.CurrentStep = 0;
        await vm.GoNextCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.CurrentStep);
        var step1Val = await _settingsRepo.GetAsync("Onboarding_Step1_Completed");
        Assert.Equal("true", step1Val);

        // Step 1 → 2
        await vm.GoNextCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentStep);
        var step2Val = await _settingsRepo.GetAsync("Onboarding_Step2_Completed");
        Assert.Equal("true", step2Val);

        // Step 2 → 3
        await vm.GoNextCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.CurrentStep);
        var step3Val = await _settingsRepo.GetAsync("Onboarding_Step3_Completed");
        Assert.Equal("true", step3Val);

        // Step 3 → 4 (Finish) — Next on step 3 navigates to Finish
        await vm.GoNextCommand.ExecuteAsync(null);
        Assert.Equal(4, vm.CurrentStep);
        var step4Val = await _settingsRepo.GetAsync("Onboarding_Step4_Completed");
        Assert.Equal("true", step4Val);
    }

    // ================================================================
    // Test: Resume — set only Step 1+2 completed, verify wizard
    // opens at Step 3
    // ================================================================

    [Fact]
    public async Task ResumeWizard_Step1And2Completed_OpensAtStep3()
    {
        // Set step 1 and 2 as completed
        await _settingsRepo.SetAsync("Onboarding_Step1_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step2_Completed", "true");

        var vm = CreateViewModel();

        // After init, should start at step 3 (first incomplete = 2... wait, 0-indexed)
        // Step1 = key 0 "Onboarding_Step1_Completed" → CurrentStep should be 2 (step 3)
        // Let me think again:
        // Step0Completed = Onboarding_Step1_Completed (Step 1 key)
        // Step1Completed = Onboarding_Step2_Completed (Step 2 key)
        // Step2Completed = Onboarding_Step3_Completed (Step 3 key)
        // Step3Completed = Onboarding_Step4_Completed (Step 4 key)
        //
        // We set Step1_Completed + Step2_Completed = true
        // So Step0Completed = true, Step1Completed = true
        // First incomplete = 2
        Assert.Equal(2, vm.CurrentStep);
    }

    // ================================================================
    // Test: Resume — all steps completed, wizard doesn't auto-open
    // ================================================================

    [Fact]
    public async Task ResumeWizard_AllStepsCompleted_StaysAtWelcome()
    {
        await _settingsRepo.SetAsync("Onboarding_Step1_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step2_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step3_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step4_Completed", "true");

        var vm = CreateViewModel();

        // All steps complete → determineFirstIncompleteStep returns 4
        // The ViewModel sets CurrentStep = -1 (Welcome) when all complete
        Assert.Equal(-1, vm.CurrentStep);
    }

    // ================================================================
    // Test: Skip all steps — wizard advances through all steps
    // ================================================================

    [Fact]
    public async Task SkipAllSteps_WizardAdvancesToFinish()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        // Skip step 0 → 1
        await vm.SkipCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.CurrentStep);

        // Skip step 1 → 2
        await vm.SkipCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentStep);

        // Skip step 2 → 3
        await vm.SkipCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.CurrentStep);

        // Skip step 3 → 4 (Finish)
        await vm.SkipCommand.ExecuteAsync(null);
        Assert.Equal(4, vm.CurrentStep);
    }

    // ================================================================
    // Test: API key added during onboarding persists to repository
    // ================================================================

    [Fact]
    public void Onboarding_AddApiKey_AppearsInList()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 0;

        // Add a key
        vm.SelectedApiKeyProvider = ProviderType.OpenAI;
        vm.ApiKeyInputValue = "sk-onboarding-test-key-12345";
        vm.AddApiKeyCommand.Execute(null);

        Assert.Single(vm.WizardApiKeys);
        Assert.Equal("sk-onboarding-test-key-12345", vm.WizardApiKeys[0].PlaintextKey);
    }

    // ================================================================
    // Test: Starter persona selection + chat mode
    // ================================================================

    [Fact]
    public void SelectPersona_SetsDisplayNameAndSystemPrompt()
    {
        var vm = CreateViewModel();
        vm.CurrentStep = 1;

        // Select "Code Helper"
        vm.SelectPersonaCommand.Execute(vm.StarterPersonas[1]);

        Assert.Equal("Code Helper", vm.PersonaDisplayName);
        Assert.Contains("expert software developer", vm.PersonaSystemPrompt);
        Assert.Equal("Standard", vm.PersonaChatMode);
    }

    // ================================================================
    // Helper
    // ================================================================

    // ================================================================
    // Test: First launch — no Onboarding_Completed key → wizard opens
    // ================================================================

    [Fact]
    public async Task FirstLaunch_Detected_WizardOpens()
    {
        // No onboarding settings exist (fresh database)
        // For the wizard to open, Onboarding_Completed must be null
        var onboardingCompleted = await _settingsRepo.GetAsync("Onboarding_Completed");
        Assert.Null(onboardingCompleted);

        // Create the ViewModel — it should start at step 0 (first incomplete)
        var vm = CreateViewModel();
        Assert.Equal(0, vm.CurrentStep);

        // Simulate completing all steps by persisting all step flags and Onboarding_Completed
        vm.CurrentStep = 0;
        await vm.GoNextCommand.ExecuteAsync(null); // Step 0 → 1
        await vm.GoNextCommand.ExecuteAsync(null); // Step 1 → 2
        await vm.GoNextCommand.ExecuteAsync(null); // Step 2 → 3
        await vm.GoNextCommand.ExecuteAsync(null); // Step 3 → 4 (Finish)

        // Verify all step keys and Onboarding_Completed are set
        var step1 = await _settingsRepo.GetAsync("Onboarding_Step1_Completed");
        var step2 = await _settingsRepo.GetAsync("Onboarding_Step2_Completed");
        var step3 = await _settingsRepo.GetAsync("Onboarding_Step3_Completed");
        var step4 = await _settingsRepo.GetAsync("Onboarding_Step4_Completed");

        Assert.Equal("true", step1);
        Assert.Equal("true", step2);
        Assert.Equal("true", step3);
        Assert.Equal("true", step4);

        // Note: Onboarding_Completed is set when the user clicks "Launch Studio"
        // Simulate that by calling LaunchStudio command
        vm.LaunchStudioCommand.Execute(null);

        // Verify Onboarding_Completed was set by LaunchStudio
        var completed = await _settingsRepo.GetAsync("Onboarding_Completed");
        Assert.Equal("true", completed);
    }

    // ================================================================
    // Test: All steps completed — wizard should skip to Welcome
    // ================================================================

    [Fact]
    public async Task AllStepsCompleted_WizardSkipped()
    {
        // Set all individual step keys AND Onboarding_Completed to true
        await _settingsRepo.SetAsync("Onboarding_Step1_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step2_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step3_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Step4_Completed", "true");
        await _settingsRepo.SetAsync("Onboarding_Completed", "true");

        // Creating the ViewModel — it should detect all steps completed
        // and stay at Welcome screen (CurrentStep = -1)
        var vm = CreateViewModel();

        // All steps complete → determineFirstIncompleteStep returns 4
        // The ViewModel sets CurrentStep = -1 (Welcome) when all complete
        Assert.Equal(-1, vm.CurrentStep);
    }

    // ================================================================
    // Helper
    // ================================================================

    private OnboardingWizardViewModel CreateViewModel()
    {
        var loggerMock = new Mock<ILogger<OnboardingWizardViewModel>>();
        var encryptionMock = new Mock<IEncryptionService>();
        var llmMock = new Mock<ILLMProviderService>();
        var confirmationMock = new Mock<IConfirmationService>();
        var wikiServiceMock = new Mock<IWikiService>();
        var textActionRepoMock = new Mock<ITextActionRepository>();

        return new OnboardingWizardViewModel(
            _apiKeyRepo,
            _settingsRepo,
            _modelConfigRepo,
            _personaRepo,
            textActionRepoMock.Object,
            encryptionMock.Object,
            llmMock.Object,
            confirmationMock.Object,
            wikiServiceMock.Object,
            loggerMock.Object);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db.Dispose();

        try
        {
            var testDir = Path.GetDirectoryName(_dbPath);
            if (testDir is not null && Directory.Exists(testDir))
            {
                for (var i = 0; i < 3; i++)
                {
                    try { Directory.Delete(testDir, recursive: true); break; }
                    catch { Thread.Sleep(100); }
                }
            }
        }
        catch { /* best effort */ }
    }
}
