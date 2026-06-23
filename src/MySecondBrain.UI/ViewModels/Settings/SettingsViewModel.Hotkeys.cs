using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Hotkey assignment properties and commands.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Hotkeys — assignments, change command, reset to defaults
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<HotkeyAssignmentDisplayItem> _hotkeyAssignments = [];

    [ObservableProperty]
    private string _recordingHotkeyCombo = string.Empty;

    private HotkeyAssignmentDisplayItem? _changingHotkeyItem;

    [RelayCommand]
    private void ChangeHotkey(HotkeyAssignmentDisplayItem? item)
    {
        if (item is null) return;
        _changingHotkeyItem = item;
        RecordingHotkeyCombo = item.Hotkey ?? string.Empty;
        IsRecordingHotkey = true;
    }

    public async void ApplyHotkeyChange(string combo)
    {
        var item = _changingHotkeyItem;
        if (item is null) return;

        if (string.IsNullOrWhiteSpace(combo))
        {
            StatusMessage = "Hotkey combo cannot be empty.";
            IsRecordingHotkey = false;
            _changingHotkeyItem = null;
            return;
        }

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

        await PersistHotkeyChangeAsync(item.ActionId, combo);

        await RefreshTextActionListAsync();
        await RefreshHotkeyAssignmentsAsync();

        IsRecordingHotkey = false;
    }

    private async Task PersistHotkeyChangeAsync(string actionId, string? hotkey)
    {
        try
        {
            if (actionId == "__commandbar__")
            {
                _logger.LogInformation("Command Bar hotkey changes not yet supported — skipping persist");
                StatusMessage = "Command Bar hotkey changes are not yet supported.";
                return;
            }

            var action = await _textActionRepo.GetByIdAsync(actionId);
            if (action is not null)
            {
                action.Hotkey = hotkey;
                action.UpdatedAt = DateTimeOffset.UtcNow;
                await _textActionRepo.UpdateAsync(action);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hotkey change for {ActionId}", actionId);
            StatusMessage = $"Failed to save hotkey change: {ex.Message}";
        }

        _changingHotkeyItem = null;
    }

    [RelayCommand]
    private async Task ResetHotkeysToDefaultsAsync()
    {
        if (!_confirmationService.Confirm(
            "Reset all hotkeys to their default values? This will overwrite any custom hotkey assignments.",
            "Reset Hotkeys to Defaults"))
            return;

        try
        {
            var allActions = await _textActionRepo.GetAllAsync();
            var defaultHotkeys = new Dictionary<string, string>
            {
                { "Rewrite", "Alt+Q" },
                { "Summarize", "Alt+W" },
                { "Explain", "Alt+E" },
                { "Translate", "Alt+R" },
                { "Continue Writing", "Alt+C" },
            };

            foreach (var action in allActions)
            {
                if (defaultHotkeys.TryGetValue(action.DisplayName, out var defaultHotkey))
                {
                    action.Hotkey = defaultHotkey;
                }
                else
                {
                    action.Hotkey = null;
                }
                action.UpdatedAt = DateTimeOffset.UtcNow;
                await _textActionRepo.UpdateAsync(action);
            }

            await RefreshHotkeyAssignmentsAsync();
            await RefreshTextActionListAsync();
            StatusMessage = "All hotkeys reset to defaults.";
            _logger.LogInformation("Hotkeys reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset hotkeys");
            StatusMessage = "Failed to reset hotkeys.";
        }
    }

    private async Task RefreshHotkeyAssignmentsAsync()
    {
        try
        {
            var actions = await _textActionRepo.GetAllAsync();

            var displayItems = actions
                .Where(a => !string.IsNullOrEmpty(a.Hotkey))
                .Select(a => new HotkeyAssignmentDisplayItem
                {
                    ActionId = a.Id,
                    ActionName = a.DisplayName,
                    Source = "TextAction",
                    CaptureScope = a.CaptureScope,
                    ApplyMode = a.ApplyMode,
                    Hotkey = a.Hotkey,
                }).ToList();

            displayItems.AddRange([
                new HotkeyAssignmentDisplayItem
                {
                    ActionId = "__commandbar__",
                    ActionName = "Command Bar",
                    Source = "CommandBar",
                    CaptureScope = "global",
                    ApplyMode = "showOnly",
                    Hotkey = "Alt+Space",
                },
            ]);

            HotkeyAssignments = new ObservableCollection<HotkeyAssignmentDisplayItem>(displayItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh hotkey assignments");
            StatusMessage = "Failed to load hotkey assignments.";
        }
    }
}
