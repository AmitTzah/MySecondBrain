using System.Windows;
using MySecondBrain.UI.Views;
using Xunit;

namespace MySecondBrain.Tests.Unit;

/// <summary>
/// Regression tests for SettingsView XAML rendering.
/// Verifies the XAML parses and renders without throwing (e.g., the
/// DisplayMemberPath + ItemTemplate conflict that crashed navigation).
/// </summary>
public class SettingsViewTests
{
    /// <summary>
    /// Regression test for the app crash when navigating to Settings.
    /// Verifies that the SettingsView XAML loads without throwing a
    /// XamlParseException caused by setting both DisplayMemberPath and
    /// ItemTemplate on the same ItemsControl.
    ///
    /// The sidebar ListBox had both DisplayMemberPath="Label" and
    /// ItemTemplate={StaticResource SidebarItemTemplate}, which WPF
    /// rejects with "Cannot set both DisplayMemberPath and ItemTemplate."
    ///
    /// Note: This test validates XAML parse only. Binding failures are
    /// silent in WPF and do not cause crashes, so a full DataContext
    /// setup is not required for this regression.
    /// </summary>
    [StaFact]
    public void XamlParsesWithoutDisplayMemberPathAndItemTemplateConflict()
    {
        var exception = Record.Exception(() =>
        {
            var view = new SettingsView();
            // Force WPF to measure/arrange, triggering template application
            view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            view.Arrange(new System.Windows.Rect(view.DesiredSize));
        });

        Assert.Null(exception);
    }
}
