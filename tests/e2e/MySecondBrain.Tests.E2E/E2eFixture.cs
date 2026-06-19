using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// Shared xUnit fixture that launches the app once for all tests in the class.
/// Implements IClassFixture pattern — one launch, shared main window.
/// </summary>
public sealed class E2eFixture : IDisposable
{
    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }

    public E2eFixture()
    {
        var appPath = GetAppPath();
        Console.WriteLine($"[FIXTURE] Launching app: {appPath}");

        App = Application.Launch(appPath);
        try
        {
            Automation = new UIA3Automation();
            MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));

            // Wait for a known UIA-exposed element to confirm startup is complete.
            // NOTE: WPF Grid/Panel elements don't expose AutomationId to UIA,
            // but RadioButton/Button/ComboBox elements do. NavChats is a guaranteed
            // RadioButton at the window root level.
            var sw = Stopwatch.StartNew();
            var readyCondition = Automation.ConditionFactory.ByAutomationId("NavChats");
            AutomationElement? ready = null;
            while (sw.Elapsed < TimeSpan.FromSeconds(8))
            {
                ready = MainWindow.FindFirst(TreeScope.Descendants, readyCondition);
                if (ready != null && ready.IsAvailable)
                    break;
                Wait.UntilInputIsProcessed();
                Thread.Sleep(200);
            }

            if (ready == null || !ready.IsAvailable)
                throw new TimeoutException(
                    "App failed to initialize (NavChats not found in UIA tree within timeout).");

            // NavChats proves the sidebar is rendered. But the initial screen view
            // (ChatView) loads asynchronously via ContentTemplateSelector. Wait for
            // a deep ChatView element to confirm the UIA subtree is fully populated.
            var chatViewCondition = Automation.ConditionFactory.ByAutomationId("IncreaseFontBtn");
            var chatSw = Stopwatch.StartNew();
            AutomationElement? chatReady = null;
            while (chatSw.Elapsed < TimeSpan.FromSeconds(6))
            {
                chatReady = MainWindow.FindFirst(TreeScope.Descendants, chatViewCondition);
                if (chatReady != null && chatReady.IsAvailable)
                    break;
                Wait.UntilInputIsProcessed();
                Thread.Sleep(150);
            }

            if (chatReady == null || !chatReady.IsAvailable)
                throw new TimeoutException(
                    "App failed to fully initialize: ChatView UIA subtree not populated within timeout.");

            Console.WriteLine($"[FIXTURE] App launched successfully. PID={App.ProcessId} "
                + $"Window: {MainWindow.BoundingRectangle.Width}x{MainWindow.BoundingRectangle.Height}");
        }
        catch
        {
            // Constructor failure: kill the launched process so it doesn't leak.
            // xUnit does NOT call Dispose() when the constructor throws.
            try { App?.Kill(); } catch { /* best effort */ }
            try { App?.Dispose(); } catch { /* best effort */ }
            try { Automation?.Dispose(); } catch { /* best effort */ }
            throw;
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[FIXTURE] Cleaning up...");

        // Capture PID before disposal — App.ProcessId can throw ObjectDisposedException
        // if Dispose() was already called by a failed close attempt.
        var pid = 0;
        try { pid = App?.ProcessId ?? 0; } catch { /* App may already be disposed */ }

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
                    Console.WriteLine("[FIXTURE] App did not close gracefully, killing process.");
                    App.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FIXTURE] App cleanup error: {ex.Message}");
            if (pid > 0)
            {
                try { Process.GetProcessById(pid)?.Kill(); }
                catch { /* best effort */ }
            }
        }
        finally
        {
            // Always dispose the Application wrapper, even if the process already exited.
            try { App?.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"[FIXTURE] App dispose error: {ex.Message}"); }
        }
    }

    private static string GetAppPath()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln").Any())
            dir = dir.Parent;

        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate solution root from test output directory.");

        var appPath = Path.Combine(
            dir.FullName,
            "src", "MySecondBrain.UI", "bin", "Debug", "net8.0-windows10.0.17763.0",
            "MySecondBrain.UI.exe");

        if (!File.Exists(appPath))
            throw new FileNotFoundException(
                "MySecondBrain.UI.exe not found. Build the solution first.", appPath);

        return appPath;
    }
}
