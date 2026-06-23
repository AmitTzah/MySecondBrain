using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Models;

namespace MySecondBrain.UI.ViewModels;

/// <summary>
/// Text Action CRUD properties, commands, capture scope, and apply mode.
/// </summary>
public partial class SettingsViewModel
{
    // ================================================================
    // Text Actions — list, form state, capture scope, apply mode
    // ================================================================

    [ObservableProperty]
    private ObservableCollection<TextActionDisplayItem> _textActions = [];

    [ObservableProperty]
    private bool _isEditingTextAction;

    [ObservableProperty]
    private TextAction? _editingTextAction;

    [ObservableProperty]
    private string _textActionDisplayNameValue = string.Empty;

    [ObservableProperty]
    private string _textActionSystemPromptValue = string.Empty;

    [ObservableProperty]
    private string? _textActionModelConfigId;

    private bool _isNewTextAction;

    // Capture scope flags (multi-select)
    [ObservableProperty]
    private bool _captureSelection = true;

    [ObservableProperty]
    private bool _captureFocusedElement;

    [ObservableProperty]
    private bool _captureSurroundingContext;

    [ObservableProperty]
    private bool _captureFullDocument;

    [ObservableProperty]
    private bool _captureScreenshot;

    // Apply mode (radio — single select)
    [ObservableProperty]
    private string _selectedApplyMode = "replaceSelection";

    [ObservableProperty]
    private string? _textActionAssignedHotkey;

    public IReadOnlyList<string> ApplyModeOptions { get; } =
    [
        "replaceSelection",
        "insertAtCursor",
        "replaceFocusedElement",
        "appendToFocusedElement",
        "prependToFocusedElement",
        "clipboardOnly",
        "showOnly",
    ];

    [RelayCommand]
    private void AddTextAction()
    {
        EditingTextAction = null;
        TextActionDisplayNameValue = string.Empty;
        TextActionSystemPromptValue = string.Empty;
        TextActionModelConfigId = null;
        TextActionAssignedHotkey = null;
        _isNewTextAction = true;

        CaptureSelection = true;
        CaptureFocusedElement = false;
        CaptureSurroundingContext = false;
        CaptureFullDocument = false;
        CaptureScreenshot = false;
        SelectedApplyMode = "replaceSelection";

        IsEditingTextAction = true;
    }

    [RelayCommand]
    private async Task SaveTextActionAsync()
    {
        if (string.IsNullOrWhiteSpace(TextActionDisplayNameValue))
        {
            StatusMessage = "Cannot save: display name is required.";
            return;
        }

        try
        {
            var captureScope = BuildCaptureScopeString();
            var now = DateTimeOffset.UtcNow;

            if (_isNewTextAction)
            {
                var action = new TextAction
                {
                    Id = Guid.NewGuid().ToString("N"),
                    DisplayName = TextActionDisplayNameValue,
                    SystemPrompt = TextActionSystemPromptValue ?? string.Empty,
                    ModelConfigId = TextActionModelConfigId,
                    Hotkey = TextActionAssignedHotkey,
                    CaptureScope = captureScope,
                    ApplyMode = SelectedApplyMode,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                if (!string.IsNullOrEmpty(action.Hotkey))
                {
                    var conflicting = await _textActionRepo.GetByHotkeyAsync(action.Hotkey);
                    if (conflicting.Count > 0 && conflicting[0].Id != action.Id)
                    {
                        if (!_confirmationService.Confirm(
                            $"Hotkey '{action.Hotkey}' is already assigned to '{conflicting[0].DisplayName}'. Assign anyway?",
                            "Hotkey Conflict"))
                            return;
                    }
                }

                await _textActionRepo.CreateAsync(action);
                _logger.LogInformation("Created new text action '{Name}'", action.DisplayName);
            }
            else if (EditingTextAction is not null)
            {
                EditingTextAction.DisplayName = TextActionDisplayNameValue;
                EditingTextAction.SystemPrompt = TextActionSystemPromptValue ?? string.Empty;
                EditingTextAction.ModelConfigId = TextActionModelConfigId;
                EditingTextAction.Hotkey = TextActionAssignedHotkey;
                EditingTextAction.CaptureScope = captureScope;
                EditingTextAction.ApplyMode = SelectedApplyMode;
                EditingTextAction.UpdatedAt = now;

                if (!string.IsNullOrEmpty(EditingTextAction.Hotkey))
                {
                    var conflicting = await _textActionRepo.GetByHotkeyAsync(EditingTextAction.Hotkey);
                    if (conflicting.Count > 0 && conflicting[0].Id != EditingTextAction.Id)
                    {
                        if (!_confirmationService.Confirm(
                            $"Hotkey '{EditingTextAction.Hotkey}' is already assigned to '{conflicting[0].DisplayName}'. Assign anyway?",
                            "Hotkey Conflict"))
                            return;
                    }
                }

                await _textActionRepo.UpdateAsync(EditingTextAction);
                _logger.LogInformation("Updated text action '{Name}'", EditingTextAction.DisplayName);
            }
            else
            {
                StatusMessage = "Cannot save: no text action being edited.";
                return;
            }

            await RefreshTextActionListAsync();
            await RefreshHotkeyAssignmentsAsync();
            ClearTextActionForm();
            StatusMessage = "Text action saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save text action");
            StatusMessage = $"Failed to save text action: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelTextActionEdit()
    {
        ClearTextActionForm();
    }

    [RelayCommand]
    private async Task EditTextActionAsync(TextActionDisplayItem? item)
    {
        if (item is null) return;

        try
        {
            var action = await _textActionRepo.GetByIdAsync(item.Id);
            if (action is null)
            {
                StatusMessage = "Text action not found.";
                return;
            }

            EditingTextAction = action;
            _isNewTextAction = false;
            TextActionDisplayNameValue = action.DisplayName;
            TextActionSystemPromptValue = action.SystemPrompt;
            TextActionModelConfigId = action.ModelConfigId;
            TextActionAssignedHotkey = action.Hotkey;
            SelectedApplyMode = action.ApplyMode;

            var scopes = action.CaptureScope
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();
            CaptureSelection = scopes.Contains("selection");
            CaptureFocusedElement = scopes.Contains("focusedElement");
            CaptureSurroundingContext = scopes.Contains("surroundingContext");
            CaptureFullDocument = scopes.Contains("fullDocument");
            CaptureScreenshot = scopes.Contains("screenshot");

            IsEditingTextAction = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load text action for editing");
            StatusMessage = "Failed to load text action.";
        }
    }

    [RelayCommand]
    private async Task DeleteTextActionAsync(TextActionDisplayItem? item)
    {
        if (item is null) return;

        if (!_confirmationService.Confirm(
            $"Delete text action '{item.DisplayName}'?",
            "Confirm Delete"))
            return;

        try
        {
            await _textActionRepo.DeleteAsync(item.Id);
            await RefreshTextActionListAsync();
            await RefreshHotkeyAssignmentsAsync();
            StatusMessage = "Text action deleted.";
            _logger.LogInformation("Deleted text action {ActionId}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete text action {ActionId}", item.Id);
            StatusMessage = "Failed to delete text action.";
        }
    }

    [RelayCommand]
    private async Task DuplicateTextActionAsync(TextActionDisplayItem? item)
    {
        if (item is null) return;

        try
        {
            var original = await _textActionRepo.GetByIdAsync(item.Id);
            if (original is null)
            {
                StatusMessage = "Text action not found.";
                return;
            }

            var copy = new TextAction
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = original.DisplayName + " (Copy)",
                SystemPrompt = original.SystemPrompt,
                ModelConfigId = original.ModelConfigId,
                CaptureScope = original.CaptureScope,
                ApplyMode = original.ApplyMode,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Hotkey = null,
                IsBuiltIn = false,
            };

            await _textActionRepo.CreateAsync(copy);
            await RefreshTextActionListAsync();
            StatusMessage = $"Duplicated '{original.DisplayName}' as '{copy.DisplayName}'.";
            _logger.LogInformation("Duplicated text action '{Original}' as '{Copy}'",
                original.DisplayName, copy.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate text action");
            StatusMessage = $"Failed to duplicate text action: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AssignTextActionHotkey()
    {
        IsRecordingHotkey = true;
    }

    public void ApplyRecordedHotkey(string combo)
    {
        TextActionAssignedHotkey = combo;
        IsRecordingHotkey = false;
    }

    public void CancelHotkeyRecording()
    {
        IsRecordingHotkey = false;
    }

    [ObservableProperty]
    private bool _isRecordingHotkey;

    private string BuildCaptureScopeString()
    {
        var scopes = new List<string>();
        if (CaptureSelection) scopes.Add("selection");
        if (CaptureFocusedElement) scopes.Add("focusedElement");
        if (CaptureSurroundingContext) scopes.Add("surroundingContext");
        if (CaptureFullDocument) scopes.Add("fullDocument");
        if (CaptureScreenshot) scopes.Add("screenshot");
        return scopes.Count > 0 ? string.Join(",", scopes) : "selection";
    }

    private void ClearTextActionForm()
    {
        IsEditingTextAction = false;
        EditingTextAction = null;
        _isNewTextAction = false;
        TextActionDisplayNameValue = string.Empty;
        TextActionSystemPromptValue = string.Empty;
        TextActionModelConfigId = null;
        TextActionAssignedHotkey = null;
        CaptureSelection = true;
        CaptureFocusedElement = false;
        CaptureSurroundingContext = false;
        CaptureFullDocument = false;
        CaptureScreenshot = false;
        SelectedApplyMode = "replaceSelection";
    }

    private async Task RefreshTextActionListAsync()
    {
        try
        {
            var actions = await _textActionRepo.GetAllAsync();
            var allConfigs = await _modelConfigRepo.GetAllAsync();
            var configLookup = allConfigs.ToDictionary(c => c.Id, c => c.DisplayName);

            var displayItems = actions.Select(a => new TextActionDisplayItem
            {
                Id = a.Id,
                DisplayName = a.DisplayName,
                SystemPrompt = a.SystemPrompt,
                ModelConfigId = a.ModelConfigId,
                ModelConfigName = a.ModelConfigId is not null
                    && configLookup.TryGetValue(a.ModelConfigId, out var name)
                    ? name
                    : string.Empty,
                Hotkey = a.Hotkey,
                CaptureScope = a.CaptureScope,
                ApplyMode = a.ApplyMode,
                IsBuiltIn = a.IsBuiltIn,
            }).ToList();

            TextActions = new ObservableCollection<TextActionDisplayItem>(displayItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh text action list");
            StatusMessage = "Failed to load text actions.";
        }
    }
}
