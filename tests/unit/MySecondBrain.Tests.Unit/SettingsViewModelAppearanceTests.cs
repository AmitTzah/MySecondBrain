using Moq;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class SettingsViewModelAppearanceTests : SettingsViewModelTestBase
{
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
    // Appearance — ChatTheme persists
    // ================================================================

    [Fact]
    public void Appearance_ChatThemeChange_PersistsToSettingsRepository()
    {
        _sut.ChatTheme = ChatTheme.Bubble;

        _settingsRepoMock.Verify(s => s.SetAsync("ChatTheme", "Bubble"), Times.Once);
    }
}
