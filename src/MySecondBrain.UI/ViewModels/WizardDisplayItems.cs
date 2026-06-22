using CommunityToolkit.Mvvm.ComponentModel;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Represents a starter persona card in the wizard persona step.
/// </summary>
public partial class StarterPersonaCard : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string PromptPreview { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Display wrapper for API keys added during onboarding.
/// </summary>
public partial class WizardApiKeyItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string ProviderLabel => ProviderType.ToString();
    public string PlaintextKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    /// <summary>
    /// Masked value for display: sk-...abc123
    /// </summary>
    public string MaskedValue
    {
        get
        {
            if (string.IsNullOrEmpty(PlaintextKey) || PlaintextKey.Length <= 10)
                return "***";
            return PlaintextKey[..Math.Min(5, PlaintextKey.Length)] + "..." + PlaintextKey[^4..];
        }
    }

    [ObservableProperty]
    private string _status = "Not tested";

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private bool _isTested;
}

/// <summary>
/// Display wrapper for hotkey assignments in the wizard.
/// </summary>
public partial class WizardHotkeyItem : ObservableObject
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string CaptureScope { get; set; } = "selection";
    public string ApplyMode { get; set; } = "replaceSelection";

    [ObservableProperty]
    private string _hotkey = string.Empty;

    [ObservableProperty]
    private bool _isRecording;
}
