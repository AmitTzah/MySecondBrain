using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class SettingsViewModelAppearanceTests : SettingsViewModelTestBase
{
    // ================================================================
    // Appearance — AppTheme defaults
    // ================================================================

    [Fact]
    public void Appearance_DefaultTheme_IsDark()
    {
        Assert.Equal(AppTheme.Dark, _sut.AppTheme);
    }

    // ================================================================
    // Appearance — AppTheme change calls IThemeProvider and persists
    // ================================================================

    [Fact]
    public void Appearance_AppThemeChange_CallsThemeProviderAndPersists()
    {
        _sut.AppTheme = AppTheme.Light;

        _themeProviderMock.Verify(t => t.SetAppTheme(AppTheme.Light), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("AppTheme", "Light"), Times.Once);
    }

    // ================================================================
    // Appearance — ChatTheme defaults
    // ================================================================

    [Fact]
    public void Appearance_DefaultChatTheme_IsClassic()
    {
        Assert.Equal(ChatTheme.Classic, _sut.ChatTheme);
    }

    [Fact]
    public void Appearance_ChatThemeOptions_ContainsAllThemes()
    {
        Assert.Contains(ChatTheme.Classic, _sut.ChatThemeOptions);
        Assert.Contains(ChatTheme.Compact, _sut.ChatThemeOptions);
        Assert.Contains(ChatTheme.Bubble, _sut.ChatThemeOptions);
        Assert.Equal(3, _sut.ChatThemeOptions.Count);
    }

    // ================================================================
    // Appearance — ChatTheme change calls IThemeProvider and persists
    // ================================================================

    [Fact]
    public void Appearance_ChatThemeChange_CallsThemeProviderAndPersists()
    {
        _sut.ChatTheme = ChatTheme.Bubble;

        _themeProviderMock.Verify(t => t.SetChatTheme(ChatTheme.Bubble), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("ChatTheme", "Bubble"), Times.Once);
    }

    // ================================================================
    // Appearance — Font defaults
    // ================================================================

    [Fact]
    public void Appearance_DefaultFontFamily_IsSegoeUI()
    {
        Assert.Equal("Segoe UI", _sut.FontFamily);
    }

    [Fact]
    public void Appearance_DefaultFontSize_Is13()
    {
        Assert.Equal(13.0, _sut.FontSize);
    }

    [Fact]
    public void Appearance_DefaultFontWeight_IsNormal()
    {
        Assert.Equal("Normal", _sut.FontWeight);
    }

    [Fact]
    public void Appearance_FontFamilyOptions_ContainsCommonFonts()
    {
        Assert.Contains("Segoe UI", _sut.FontFamilyOptions);
        Assert.Contains("Consolas", _sut.FontFamilyOptions);
    }

    [Fact]
    public void Appearance_FontWeightOptions_ContainsNormalAndBold()
    {
        Assert.Contains("Normal", _sut.FontWeightOptions);
        Assert.Contains("Bold", _sut.FontWeightOptions);
        Assert.Equal(2, _sut.FontWeightOptions.Count);
    }

    // ================================================================
    // Appearance — FontSize clamping at boundary values
    // ================================================================

    [Fact]
    public void Appearance_FontSizeClampsBelow10()
    {
        _sut.FontSize = 5.0;
        Assert.Equal(10.0, _sut.FontSize);
        _settingsRepoMock.Verify(s => s.SetAsync("FontSize", "10.0"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), 10.0, It.IsAny<System.Windows.FontWeight>()), Times.Once);
    }

    [Fact]
    public void Appearance_FontSizeClampsAbove24()
    {
        _sut.FontSize = 30.0;
        Assert.Equal(24.0, _sut.FontSize);
        _settingsRepoMock.Verify(s => s.SetAsync("FontSize", "24.0"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), 24.0, It.IsAny<System.Windows.FontWeight>()), Times.Once);
    }

    [Fact]
    public void Appearance_FontSizeWithinRange_NotClamped()
    {
        _sut.FontSize = 16.0;
        Assert.Equal(16.0, _sut.FontSize);
    }

    // ================================================================
    // Appearance — FontFamily change persists
    // ================================================================

    [Fact]
    public void Appearance_FontFamilyChange_PersistsToSettingsRepository()
    {
        _sut.FontFamily = "Consolas";

        _settingsRepoMock.Verify(s => s.SetAsync("FontFamily", "Consolas"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings("Consolas", 13.0, It.IsAny<System.Windows.FontWeight>()), Times.Once);
    }

    // ================================================================
    // Appearance — FontWeight change persists
    // ================================================================

    [Fact]
    public void Appearance_FontWeightBold_PersistsAndCallsThemeProvider()
    {
        _sut.FontWeight = "Bold";

        _settingsRepoMock.Verify(s => s.SetAsync("FontWeight", "Bold"), Times.Once);
        _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), It.IsAny<double>(), System.Windows.FontWeights.Bold), Times.Once);
    }

    // ================================================================
    // Appearance — FontSize persisted with InvariantCulture (Bug 5)
    // ================================================================

    [Fact]
    public void Appearance_FontSizeChange_PersistsWithInvariantCulture()
    {
        // Simulate a culture where comma is the decimal separator
        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            _sut.FontSize = 13.5;

            // Verify the persisted value uses dot (InvariantCulture), not comma
            _settingsRepoMock.Verify(s => s.SetAsync("FontSize", "13.5"), Times.Once);
            _themeProviderMock.Verify(t => t.SetFontSettings(It.IsAny<string>(), 13.5, It.IsAny<System.Windows.FontWeight>()), Times.Once);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
