namespace MySecondBrain.Core.Interfaces;

public interface ISystemTrayService : IDisposable
{
    bool IsVisible { get; }
    void Show();
    void Hide();
    void SetGenerationIndicator(bool isGenerating);
    void UpdateRecentChats(IReadOnlyList<string> recentChatTitles);
    event EventHandler? OpenStudioRequested;
    event EventHandler? NewChatRequested;
    event EventHandler? CommandBarRequested;
    event EventHandler? SettingsRequested;
    event EventHandler? ExitRequested;
}
