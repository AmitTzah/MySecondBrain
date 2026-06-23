using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using Xunit;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// Abstract base class for all E2E test classes.
/// Provides shared helpers for UIA element discovery, settings navigation, and MessageBox handling.
/// 
/// NOTE: Each concrete test class must independently implement ICollectionFixture<E2eFixture>
/// and be decorated with [Collection("E2E")]. Do NOT put the collection interface on this base class.
/// </summary>
public abstract class E2eTestBase
{
    protected readonly ITestOutputHelper _output;
    protected readonly E2eFixture _fixture;
    protected readonly ConditionFactory _cf;

    protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    protected const int RetryIntervalMs = 200;

    protected E2eTestBase(E2eFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _cf = _fixture.Automation.ConditionFactory;
    }

    /// <summary>
    /// Brings the main application window to the foreground.
    /// Call at the start of every test to ensure consistent focus state.
    /// </summary>
    protected async Task UseSharedAppAsync()
    {
        _fixture.MainWindow.Focus();
        await Task.Delay(200);
    }

    /// <summary>
    /// Finds a UIA element by its AutomationId within the given root (or MainWindow).
    /// Polls at RetryIntervalMs up to the specified timeout.
    /// Returns null if not found.
    /// </summary>
    protected AutomationElement? FindById(string automationId,
        AutomationElement? root = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var searchRoot = root ?? _fixture.MainWindow;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var element = searchRoot.FindFirst(TreeScope.Descendants,
                _cf.ByAutomationId(automationId));
            if (element != null && element.IsAvailable)
                return element;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }
        return null;
    }

    /// <summary>
    /// Finds a UIA element by its Name property within the given root (or MainWindow).
    /// Polls at RetryIntervalMs up to the specified timeout.
    /// Returns null if not found.
    /// </summary>
    protected AutomationElement? FindByName(string name,
        AutomationElement? root = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var searchRoot = root ?? _fixture.MainWindow;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var element = searchRoot.FindFirst(TreeScope.Descendants,
                _cf.ByName(name));
            if (element != null && element.IsAvailable)
                return element;
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }
        return null;
    }

    /// <summary>
    /// Finds a UIA element whose Name contains the given substring (case-insensitive).
    /// Polls at RetryIntervalMs up to the specified timeout.
    /// Returns null if not found.
    /// </summary>
    protected AutomationElement? FindByNameContains(string partialName,
        AutomationElement? root = null, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var searchRoot = root ?? _fixture.MainWindow;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var allElements = searchRoot.FindAllDescendants();
            foreach (var element in allElements)
            {
                if (element.Name?.Contains(partialName, StringComparison.OrdinalIgnoreCase) == true)
                    return element;
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(RetryIntervalMs);
        }
        return null;
    }

    /// <summary>
    /// Navigates to the Settings screen by clicking the NavSettings RadioButton.
    /// Asserts that SettingsView becomes visible.
    /// </summary>
    protected void NavigateToSettings()
    {
        var navSettings = FindById("NavSettings");
        Assert.NotNull(navSettings);
        navSettings!.Click();
        Thread.Sleep(400); // Screen transition
        var settingsView = FindById("SettingsView");
        Assert.NotNull(settingsView);
    }

    /// <summary>
    /// Selects a settings category by partial name match.
    /// Finds a ListBoxItem whose Name contains the given string and clicks it.
    /// </summary>
    protected void SelectSettingsCategory(string categoryMatch)
    {
        var categoryItem = FindByNameContains(categoryMatch);
        Assert.NotNull(categoryItem);
        categoryItem!.Click();
        Thread.Sleep(300); // Category content transition
    }

    /// <summary>
    /// Waits for a WPF MessageBox with "Confirm" in its title to appear,
    /// then clicks the specified button (e.g., "Yes" or "No").
    /// Throws TimeoutException if the confirmation dialog does not appear within the timeout.
    /// </summary>
    protected void ConfirmMessageBox(string expectedButton, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(3);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var windows = _fixture.Automation.GetDesktop().FindAllDescendants(
                _cf.ByControlType(ControlType.Window));
            foreach (var w in windows)
            {
                if (w.Name?.Contains("Confirm", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var btn = w.FindFirstDescendant(
                        _cf.ByControlType(ControlType.Button).And(_cf.ByName(expectedButton)));
                    btn?.Click();
                    return;
                }
            }
            Wait.UntilInputIsProcessed();
            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Confirmation MessageBox with title containing 'Confirm' did not appear within {limit.TotalSeconds} seconds.");
    }

    /// <summary>
    /// Sets text in a PasswordBox identified by its AutomationId.
    /// Uses the UIA Value pattern (SetValue), not keyboard simulation.
    /// </summary>
    protected void SetPasswordInput(string automationId, string text)
    {
        var passwordBox = FindById(automationId);
        Assert.NotNull(passwordBox);
        passwordBox!.AsTextBox().Text = text;
    }
}
