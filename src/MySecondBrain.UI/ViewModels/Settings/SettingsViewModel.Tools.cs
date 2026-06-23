using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Tool auto-approval properties and STT configuration.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Tools — Auto-approval defaults, STT provider
    // ================================================================

    [ObservableProperty]
    private string _webSearchAutoApproval = "Ask";

    [ObservableProperty]
    private string _terminalAutoApproval = "Ask";

    [ObservableProperty]
    private string _fileGenerateAutoApproval = "Ask";

    [ObservableProperty]
    private string _fileEditAutoApproval = "Ask";

    [ObservableProperty]
    private string _sttProvider = "OpenAI Whisper";

    [ObservableProperty]
    private string _sttModel = string.Empty;

    public IReadOnlyList<string> ToolApprovalOptions { get; } =
    [
        "Ask",
        "AutoApprove",
        "Disabled",
    ];

    public IReadOnlyList<string> TerminalApprovalOptions { get; } =
    [
        "Ask",
        "Disabled",
    ];

    public IReadOnlyList<string> SttProviderOptions { get; } =
    [
        "OpenAI Whisper",
        "Local Whisper",
        "Windows Speech",
    ];

    partial void OnWebSearchAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("WebSearchAutoApproval", value);

    partial void OnTerminalAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("TerminalAutoApproval", value);

    partial void OnFileGenerateAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("FileGenerateAutoApproval", value);

    partial void OnFileEditAutoApprovalChanged(string value)
        => _ = _settingsRepo.SetAsync("FileEditAutoApproval", value);

    partial void OnSttProviderChanged(string value)
        => _ = _settingsRepo.SetAsync("SttProvider", value);

    partial void OnSttModelChanged(string value)
        => _ = _settingsRepo.SetAsync("SttModel", value);

    [RelayCommand]
    private void TestMicrophone()
    {
        StatusMessage = "Microphone test — not yet implemented.";
    }
}
