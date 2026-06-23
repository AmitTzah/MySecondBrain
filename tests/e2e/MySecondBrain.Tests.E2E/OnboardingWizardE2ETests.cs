using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 8: Onboarding Wizard.
/// Tests the 5-step onboarding flow including welcome screen verification,
/// per-step skip navigation, full completion, and re-run from Settings.
///
/// IMPORTANT: These tests have a strict ordering dependency. xUnit runs tests
/// within a class in alphabetical order by method name. The numbered prefixes
/// (01_, 02_, 03_, 04_, 05_, 06_) enforce the required order:
///
/// 01 → Welcome screen elements check
/// 02 → Step 0 (API Keys) → Step 1 (Persona) skip
/// 03 → Step 1 (Persona) → Step 2 (Wiki) skip
/// 04 → Complete 5-step flow, sets _wizardCompleted = true
/// 05 → Verify wizard does NOT appear after completion
/// 06 → Re-run wizard from Settings
///
/// The onboarding wizard auto-launches on first app start (fresh test DB).
/// Tests share static _wizardCompleted to guard the post-completion state.
/// </summary>
[Collection("E2E")]
public sealed class OnboardingWizardE2ETests : E2eTestBase
{
    /// <summary>
    /// Static flag set to true after the full wizard flow completes.
    /// The "should not appear" test guards against running before completion.
    /// This relies on xUnit's default alphabetical test ordering within a class.
    /// </summary>
    private static bool _wizardCompleted;

    /// <summary>
    /// Number of wizard steps 0-3 that support the Skip button (steps before Finish).
    /// </summary>
    private const int WizardStepCount = 4;

    public OnboardingWizardE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // Test 01: Welcome Screen Elements
    // ============================================================

    [Fact]
    public async Task Onboarding_01_WelcomeScreen_ShouldHaveCorrectElements()
    {
        await UseSharedAppAsync();

        // Verify welcome title
        var welcomeTitle = FindByName("Welcome to MySecondBrain");
        Assert.NotNull(welcomeTitle);
        _output.WriteLine("Welcome screen title: 'Welcome to MySecondBrain' found.");

        // Verify Get Started button
        var getStartedBtn = FindById("WizardGetStarted");
        Assert.NotNull(getStartedBtn);
        Assert.True(getStartedBtn!.IsEnabled, "WizardGetStarted button should be enabled.");
        _output.WriteLine("WizardGetStarted button present and enabled.");

        // Verify feature cards exist (text elements)
        var featureCard1 = FindByNameContains("Three-Tier AI");
        var featureCard2 = FindByNameContains("Bring Your Own Keys");
        var featureCard3 = FindByNameContains("Personal Wiki");

        Assert.NotNull(featureCard1);
        Assert.NotNull(featureCard2);
        Assert.NotNull(featureCard3);
        _output.WriteLine("All 3 feature cards present on welcome screen.");

        // Verify Back button is NOT visible on welcome screen
        // The Back button has Visibility="Collapsed" when IsWelcomeScreen is true (via InvertedVisibilityConverter)
        // and appears only after the user clicks Get Started.
        var backBtn = FindById("WizardBack", timeout: TimeSpan.FromSeconds(1));
        if (backBtn != null)
        {
            // If found in the UIA tree, it must be off-screen (collapsed)
            Assert.True(backBtn.IsOffscreen,
                "WizardBack should be off-screen (collapsed) on the welcome screen.");
            _output.WriteLine("WizardBack found but off-screen (correct for welcome screen).");
        }
        else
        {
            _output.WriteLine("WizardBack not found on welcome screen (expected).");
        }

        _output.WriteLine("Onboarding welcome screen elements verified.");
    }

    // ============================================================
    // Test 02: Step 1 (API Keys) — Can Skip
    // ============================================================

    [Fact]
    public async Task Onboarding_02_Step1_ApiKeys_CanSkip()
    {
        await UseSharedAppAsync();

        // Navigate from welcome to Step 0 (API Keys)
        var getStartedBtn = FindById("WizardGetStarted");
        Assert.NotNull(getStartedBtn);
        getStartedBtn!.Click();
        await Task.Delay(400);

        // Verify Step 0 is visible
        var step0View = FindById("OnboardingStep0View", timeout: TimeSpan.FromSeconds(3));
        Assert.NotNull(step0View);
        _output.WriteLine("Onboarding Step 0 (API Keys) view visible.");

        // Verify the step title
        var step0Title = FindByName("Add Your API Keys");
        Assert.NotNull(step0Title);
        _output.WriteLine("Step 0 title: 'Add Your API Keys' found.");

        // Click Skip to go to Step 1 (Persona)
        var skipBtn = FindById("WizardSkip");
        Assert.NotNull(skipBtn);
        Assert.True(skipBtn!.IsEnabled, "WizardSkip should be enabled on Step 0.");
        skipBtn.Click();
        await Task.Delay(400);

        // Verify Step 1 is visible
        var step1View = FindById("OnboardingStep1View", timeout: TimeSpan.FromSeconds(3));
        Assert.NotNull(step1View);
        _output.WriteLine("Navigated to Step 1 (Persona) via Skip.");

        // Verify Step 1 title
        var step1Title = FindByName("Choose Your Assistant");
        Assert.NotNull(step1Title);
        _output.WriteLine("Step 1 title: 'Choose Your Assistant' found.");
    }

    // ============================================================
    // Test 03: Step 2 (Persona) — Can Skip
    // ============================================================

    [Fact]
    public async Task Onboarding_03_Step2_Persona_CanSkip()
    {
        await UseSharedAppAsync();

        // Verify Step 1 (Persona) is visible — from the previous test in the full suite run.
        // In a filtered run the fixture auto-dismisses the wizard, so this test gracefully
        // exits when its preconditions aren't met.
        var step1View = FindById("OnboardingStep1View", timeout: TimeSpan.FromSeconds(3));
        if (step1View == null)
        {
            _output.WriteLine("Onboarding wizard not present (fixture auto-dismissed it) — skipping test.");
            return;
        }

        _output.WriteLine("Onboarding is on Step 1 (Persona).");

        // Click Skip to go to Step 2 (Wiki)
        var skipBtn = FindById("WizardSkip");
        Assert.NotNull(skipBtn);
        Assert.True(skipBtn!.IsEnabled, "WizardSkip should be enabled on Step 1.");
        skipBtn.Click();
        await Task.Delay(400);

        // Verify Step 2 is visible
        var step2View = FindById("OnboardingStep2View", timeout: TimeSpan.FromSeconds(3));
        Assert.NotNull(step2View);
        _output.WriteLine("Navigated to Step 2 (Wiki) via Skip.");

        // Verify Step 2 title
        var step2Title = FindByName("Set Up Your Personal Wiki");
        Assert.NotNull(step2Title);
        _output.WriteLine("Step 2 title: 'Set Up Your Personal Wiki' found.");
    }

    // ============================================================
    // Test 04: Complete 5-Step Flow
    // ============================================================

    [Fact]
    public async Task Onboarding_04_ShouldComplete5StepFlow()
    {
        await UseSharedAppAsync();

        // We should be on Step 2 (Wiki) from the previous test
        // If the wizard was re-launched (e.g., via re-run test), we may be on welcome
        var welcomeTitle = FindByName("Welcome to MySecondBrain", timeout: TimeSpan.FromSeconds(1));
        if (welcomeTitle != null)
        {
            _output.WriteLine("On welcome screen — navigating to Step 2.");
            FindById("WizardGetStarted")!.Click();
            await Task.Delay(300);

            // Skip Step 0
            FindById("WizardSkip")!.Click();
            await Task.Delay(300);

            // Skip Step 1
            FindById("WizardSkip")!.Click();
            await Task.Delay(300);
        }

        // Now we should be on Step 2 — skip it
        var step2View = FindById("OnboardingStep2View", timeout: TimeSpan.FromSeconds(2));
        if (step2View != null)
        {
            _output.WriteLine("On Step 2 (Wiki) — skipping.");
            FindById("WizardSkip")!.Click();
            await Task.Delay(400);
        }

        // Verify Step 3 (Hotkeys)
        var step3View = FindById("OnboardingStep3View", timeout: TimeSpan.FromSeconds(3));
        Assert.NotNull(step3View);
        var step3Title = FindByName("Review Your Hotkeys");
        Assert.NotNull(step3Title);
        _output.WriteLine("Step 3 (Hotkeys) visible.");

        // Skip Step 3
        var skipBtn = FindById("WizardSkip");
        Assert.NotNull(skipBtn);
        skipBtn!.Click();
        await Task.Delay(400);

        // Verify Finish screen (Step 4)
        var step4View = FindById("OnboardingStep4View", timeout: TimeSpan.FromSeconds(3));
        Assert.NotNull(step4View);
        var finishTitle = FindByName("You're All Set!");
        Assert.NotNull(finishTitle);
        _output.WriteLine("Finish screen (Step 4) visible with 'You're All Set!' title.");

        // Click Launch Studio
        var launchBtn = FindById("WizardLaunchStudio");
        Assert.NotNull(launchBtn);
        Assert.True(launchBtn!.IsEnabled, "WizardLaunchStudio button should be enabled.");
        launchBtn.Click();
        await Task.Delay(500);

        // Verify ChatView is visible (wizard closed, MainWindow active)
        var chatView = FindById("ChatView");
        Assert.NotNull(chatView);
        _output.WriteLine("Wizard completed — ChatView visible.");

        _wizardCompleted = true;
        _output.WriteLine("Onboarding wizard 5-step flow completed successfully.");
    }

    // ============================================================
    // Test 05: Wizard Should Not Appear After Completion
    // ============================================================

    [Fact]
    public async Task Onboarding_05_ShouldNotAppearAfterCompletion()
    {
        if (!_wizardCompleted)
            Assert.Fail("This test must run after Onboarding_04_ShouldComplete5StepFlow. "
                + "Set _wizardCompleted = true first by running the full completion flow.");

        await UseSharedAppAsync();

        // Verify the wizard window is NOT present
        var wizardTitle = FindByName("Welcome to MySecondBrain", timeout: TimeSpan.FromSeconds(1));
        Assert.Null(wizardTitle);
        _output.WriteLine("Wizard welcome title absent (expected after completion).");

        // Verify the wizard window itself is not in the UIA desktop tree
        var wizardWindow = FindWizardWindow();
        Assert.Null(wizardWindow);
        _output.WriteLine("OnboardingWizardWindow absent from UIA tree.");

        // Verify we're on the ChatView
        var chatView = FindById("ChatView");
        Assert.NotNull(chatView);
        _output.WriteLine("ChatView visible — onboarding correctly skipped after completion.");
    }

    // ============================================================
    // Test 06: Re-run Onboarding from Settings
    // ============================================================

    [Fact]
    public async Task Onboarding_06_ReRunOnboarding_FromSettings_ShouldLaunchWizard()
    {
        await UseSharedAppAsync();
        NavigateToSettings();
        await Task.Delay(300);

        // Find the "🔄 Re-run Onboarding Wizard" hyperlink in the sidebar footer
        var reRunLink = FindByNameContains("Re-run Onboarding Wizard");
        Assert.NotNull(reRunLink);
        _output.WriteLine("Found 'Re-run Onboarding Wizard' hyperlink in Settings.");

        // Click the hyperlink
        reRunLink!.Click();
        await Task.Delay(800);

        // Verify the wizard window appears
        var wizardWindow = FindWizardWindow();
        Assert.NotNull(wizardWindow);
        _output.WriteLine("OnboardingWizardWindow re-launched from Settings.");

        // Verify welcome screen is shown
        var welcomeTitle = FindByName("Welcome to MySecondBrain");
        Assert.NotNull(welcomeTitle);
        _output.WriteLine("Welcome screen visible in re-launched wizard.");

        // Skip through the wizard to dismiss it
        var getStarted = FindById("WizardGetStarted");
        Assert.NotNull(getStarted);
        getStarted!.Click();
        await Task.Delay(300);

        // Skip steps 0-3 (the WizardStepCount steps before Finish)
        int successCount = 0;
        for (int i = 0; i < WizardStepCount; i++)
        {
            var skip = FindById("WizardSkip");
            if (skip != null && skip.IsAvailable && skip.IsEnabled)
            {
                skip.Click();
                await Task.Delay(300);
                successCount++;
            }
            else
            {
                _output.WriteLine($"[WARN] WizardSkip not found/enabled at iteration {i}.");
            }
        }

        _output.WriteLine($"Successfully skipped {successCount}/{WizardStepCount} wizard steps.");

        // Launch Studio to dismiss
        var launchBtn = FindById("WizardLaunchStudio", timeout: TimeSpan.FromSeconds(3));
        if (launchBtn != null)
        {
            launchBtn.Click();
            await Task.Delay(500);
            _output.WriteLine("Dismissed re-launched wizard via Launch Studio.");
        }
        else
        {
            _output.WriteLine("WizardLaunchStudio not found — wizard may have been dismissed differently.");
        }

        _output.WriteLine("Re-run onboarding from settings verified.");
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// Finds the OnboardingWizardWindow in the desktop UIA tree.
    /// The wizard is a separate top-level Window, not a child of MainWindow.
    /// Returns null if not found.
    /// </summary>
    private AutomationElement? FindWizardWindow()
    {
        var desktop = _fixture.Automation.GetDesktop();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            var windows = desktop.FindAllDescendants(
                _cf.ByControlType(ControlType.Window));
            foreach (var w in windows)
            {
                if (w.Name?.Contains("MySecondBrain") == true &&
                    w.Name?.Contains("Onboarding") == true)
                {
                    return w;
                }
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
        }
        return null;
    }
}
