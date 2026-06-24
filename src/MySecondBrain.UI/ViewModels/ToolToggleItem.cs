using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Represents a single tool toggle in the per-chat toolbar.
/// Bound to a CheckBox in the Tools dropdown.
/// </summary>
public partial class ToolToggleItem : ObservableObject
{
    /// <summary>Internal tool name (e.g., "bash", "text_editor").</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Display-friendly name (e.g., "Bash", "Text Editor").</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>Whether this tool is enabled for the current chat.</summary>
    [ObservableProperty]
    private bool _isEnabled = true;
}
