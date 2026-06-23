using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Step 3 — Hotkeys: view, change, and reset hotkey assignments.
/// </summary>
public partial class OnboardingWizardViewModel
{
    [ObservableProperty]
    private ObservableCollection<WizardHotkeyItem> _hotkeyAssignments = [];

    private WizardHotkeyItem? _changingHotkeyItem;

    [ObservableProperty]
    private string _recordingHotkeyCombo = string.Empty;

    [ObservableProperty]
    private bool _isRecordingHotkey;

    [RelayCommand]
    private void ChangeWizardHotkey(WizardHotkeyItem? item)
    {
        if (item is null) return;
        _changingHotkeyItem = item;
        RecordingHotkeyCombo = item.Hotkey;
        IsRecordingHotkey = true;
    }

    /// <summary>
    /// Called from code-behind when a hotkey combination is recorded.
    /// </summary>
    public void ApplyRecordedHotkey(string combo)
    {
        var item = _changingHotkeyItem;
        if (item is null) return;

        if (string.IsNullOrWhiteSpace(combo))
        {
            IsRecordingHotkey = false;
            _changingHotkeyItem = null;
            return;
        }

        // Check for conflict
        var conflict = HotkeyAssignments.FirstOrDefault(
            h => h.Hotkey == combo && h.ActionId != item.ActionId);
        if (conflict is not null)
        {
            if (!_confirmationService.Confirm(
                $"Hotkey '{combo}' is already assigned to '{conflict.ActionName}'. Assign anyway?",
                "Hotkey Conflict"))
            {
                IsRecordingHotkey = false;
                _changingHotkeyItem = null;
                return;
            }
        }

        item.Hotkey = combo;
        item.IsRecording = false;
        IsRecordingHotkey = false;
        _changingHotkeyItem = null;
    }

    public void CancelHotkeyRecording()
    {
        if (_changingHotkeyItem is not null)
        {
            _changingHotkeyItem.IsRecording = false;
            _changingHotkeyItem = null;
        }
        IsRecordingHotkey = false;
    }

    [RelayCommand]
    private void ResetWizardHotkeysToDefaults()
    {
        foreach (var item in HotkeyAssignments)
        {
            if (s_defaultHotkeys.TryGetValue(item.ActionId, out var hotkey))
                item.Hotkey = hotkey;
        }
    }
}
