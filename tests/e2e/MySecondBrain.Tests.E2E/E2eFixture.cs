using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// Shared xUnit collection fixture that launches the app once for all tests in the collection.
/// Implements ICollectionFixture pattern — one launch for all ~60 tests across 8 classes.
/// Sets MSB_DB_PATH to a temp database so each run gets a fresh DB.
/// </summary>
public sealed class E2eFixture : IDisposable
{
    public FlaUI.Core.Application App { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }

    private readonly string _testDbPath;

    public E2eFixture()
    {
        // ═══════════════════════════════════════════════════════════════
        // Show a hands-off countdown before launching the E2E app.
        // Mouse/keyboard activity during E2E tests steals focus and
        // breaks WPF UIA interactions.
        // ═══════════════════════════════════════════════════════════════
        HandsOffCountdown.Show();

        var testOutputDir = Path.Combine(Path.GetTempPath(), "MySecondBrain_E2E");
        Directory.CreateDirectory(testOutputDir);

        _testDbPath = Path.Combine(testOutputDir, "e2e-test.db");
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);

        Environment.SetEnvironmentVariable("MSB_DB_PATH", _testDbPath,
            EnvironmentVariableTarget.Process);

        var appPath = GetAppPath();
        Console.WriteLine($"[FIXTURE] Launching app: {appPath}");

        App = FlaUI.Core.Application.Launch(appPath);
        try
        {
            Automation = new UIA3Automation();
            MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));

            // Check if the onboarding wizard window is present (fresh test DB scenario).
            // The wizard is a separate top-level Window that blocks MainWindow's NavChats.
            // If present, auto-dismiss it before waiting for NavChats.
            DismissOnboardingWizardIfPresent();

            // After dismissing the wizard (which was the first/only window GetMainWindow returned),
            // re-acquire the real MainWindow from the app process.
            MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));

            // Wait for NavChats (proves sidebar rendered)
            var readyCondition = Automation.ConditionFactory.ByAutomationId("NavChats");
            var sw = Stopwatch.StartNew();
            AutomationElement? ready = null;
            while (sw.Elapsed < TimeSpan.FromSeconds(8))
            {
                ready = MainWindow.FindFirst(TreeScope.Descendants, readyCondition);
                if (ready != null && ready.IsAvailable) break;
                Wait.UntilInputIsProcessed();
                Thread.Sleep(200);
            }
            if (ready == null || !ready.IsAvailable)
                throw new TimeoutException("App failed to initialize (NavChats not found).");

            // Wait for IncreaseFontBtn (proves ChatView UIA subtree populated)
            var chatReadyCondition = Automation.ConditionFactory.ByAutomationId("IncreaseFontBtn");
            var chatSw = Stopwatch.StartNew();
            AutomationElement? chatReady = null;
            while (chatSw.Elapsed < TimeSpan.FromSeconds(6))
            {
                chatReady = MainWindow.FindFirst(TreeScope.Descendants, chatReadyCondition);
                if (chatReady != null && chatReady.IsAvailable) break;
                Wait.UntilInputIsProcessed();
                Thread.Sleep(200);
            }
            if (chatReady == null || !chatReady.IsAvailable)
                throw new TimeoutException("App failed to fully initialize: ChatView UIA subtree not populated.");

            Console.WriteLine($"[FIXTURE] App launched. PID={App.ProcessId}");
        }
        catch
        {
            // Constructor failure: kill launched process so it doesn't leak.
            // xUnit does NOT call Dispose() when the constructor throws.
            try { App?.Kill(); } catch { }
            try { App?.Dispose(); } catch { }
            try { Automation?.Dispose(); } catch { }
            throw;
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[FIXTURE] Cleaning up...");
        var pid = 0;
        try { pid = App?.ProcessId ?? 0; } catch { }

        try { Automation?.Dispose(); }
        catch (Exception ex) { Console.WriteLine($"[FIXTURE] Automation dispose error: {ex.Message}"); }

        try
        {
            if (App != null && !App.HasExited)
            {
                App.Close();
                var sw = Stopwatch.StartNew();
                while (!App.HasExited && sw.Elapsed < TimeSpan.FromSeconds(5))
                    Thread.Sleep(200);
                if (!App.HasExited)
                {
                    Console.WriteLine("[FIXTURE] App did not close gracefully, killing.");
                    App.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FIXTURE] App cleanup error: {ex.Message}");
            if (pid > 0)
            {
                try
                {
                    using var fallbackProcess = Process.GetProcessById(pid);
                    fallbackProcess.Kill();
                }
                catch { }
            }
        }
        finally
        {
            try { App?.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"[FIXTURE] App dispose error: {ex.Message}"); }
        }

        // Delete test database
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); }
            catch (Exception ex) { Console.WriteLine($"[FIXTURE] DB delete error: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Checks if the OnboardingWizardWindow exists in the UIA desktop tree and,
    /// if so, auto-completes the wizard by clicking through all steps to dismiss it.
    /// This handles the case where a fresh test database triggers the first-launch wizard.
    /// </summary>
    private void DismissOnboardingWizardIfPresent()
    {
        var desktop = Automation.GetDesktop();
        var wizardCondition = Automation.ConditionFactory.ByAutomationId("OnboardingWizardWindow");
        var sw = Stopwatch.StartNew();
        AutomationElement? wizardWindow = null;

        while (sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            wizardWindow = desktop.FindFirstDescendant(wizardCondition);
            if (wizardWindow != null) break;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
        }

        if (wizardWindow == null)
        {
            Console.WriteLine("[FIXTURE] No onboarding wizard detected — proceeding normally.");
            return;
        }

        Console.WriteLine("[FIXTURE] Onboarding wizard detected — auto-dismissing.");

        // Click "Get Started" on the welcome screen
        var getStarted = wizardWindow.FindFirstDescendant(
            Automation.ConditionFactory.ByAutomationId("WizardGetStarted"));
        if (getStarted != null && getStarted.IsAvailable && getStarted.IsEnabled)
        {
            getStarted.Click();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(300);
        }

        // Skip through steps 0-3 (the 4 steps before the Finish screen)
        for (int i = 0; i < 4; i++)
        {
            var skipBtn = wizardWindow.FindFirstDescendant(
                Automation.ConditionFactory.ByAutomationId("WizardSkip"));
            if (skipBtn != null && skipBtn.IsAvailable && skipBtn.IsEnabled)
            {
                skipBtn.Click();
                Wait.UntilInputIsProcessed();
                Thread.Sleep(300);
            }
        }

        // Click "Launch Studio" to finish the wizard (extra delay for window close animation)
        var launchBtn = wizardWindow.FindFirstDescendant(
            Automation.ConditionFactory.ByAutomationId("WizardLaunchStudio"));
        if (launchBtn != null && launchBtn.IsAvailable && launchBtn.IsEnabled)
        {
            launchBtn.Click();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(500);
        }

        Console.WriteLine("[FIXTURE] Onboarding wizard dismissed.");
    }

    private static string GetAppPath()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln").Any())
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("Could not locate solution root.");
        var appPath = Path.Combine(dir.FullName, "src", "MySecondBrain.UI", "bin",
            "Debug", "net8.0-windows10.0.17763.0", "MySecondBrain.UI.exe");
        if (!File.Exists(appPath))
            throw new FileNotFoundException("MySecondBrain.UI.exe not found. Build first.", appPath);
        return appPath;
    }
}
