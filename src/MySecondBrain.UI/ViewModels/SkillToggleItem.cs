using CommunityToolkit.Mvvm.ComponentModel;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Represents a single skill toggle in the per-chat toolbar.
/// Bound to a CheckBox in the Skills dropdown.
/// </summary>
public partial class SkillToggleItem : ObservableObject
{
    /// <summary>Skill identifier (e.g., "xlsx", "docx").</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Human-readable description.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Whether this skill is enabled for the current chat.</summary>
    [ObservableProperty]
    private bool _isEnabled = true;
}
