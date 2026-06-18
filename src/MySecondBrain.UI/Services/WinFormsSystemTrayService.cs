using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.UI.Services;

public class WinFormsSystemTrayService : ISystemTrayService
{
#pragma warning disable CS0414, CS0067
    public event EventHandler? OpenStudioRequested;
    public event EventHandler? NewChatRequested;
    public event EventHandler? CommandBarRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
#pragma warning restore CS0414, CS0067

    public bool IsVisible => false;

    public void Show() { }

    public void Hide() { }

    public void SetGenerationIndicator(bool isGenerating) { }

    public void UpdateRecentChats(IReadOnlyList<string> recentChatTitles) { }

    public void Dispose()
    {
        OpenStudioRequested = null;
        NewChatRequested = null;
        CommandBarRequested = null;
        SettingsRequested = null;
        ExitRequested = null;
    }
}
