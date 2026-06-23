using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Data;
using MySecondBrain.UI.ViewModels;

namespace MySecondBrain.Tests.Unit;

public class SettingsViewModelGeneralSettingsTests : SettingsViewModelTestBase
{
    // ================================================================
    // Notifications — defaults
    // ================================================================

    [Fact]
    public void Notifications_SoundOnCompletion_DefaultFalse()
    {
        Assert.False(_sut.SoundOnCompletion);
    }

    [Fact]
    public void Notifications_DisableStreaming_DefaultFalse()
    {
        Assert.False(_sut.DisableStreaming);
    }

    [Fact]
    public void Notifications_CrossTabCompletionAlert_DefaultTrue()
    {
        Assert.True(_sut.CrossTabCompletionAlert);
    }

    // ================================================================
    // Notifications — persistence
    // ================================================================

    [Fact]
    public void Notifications_SoundOnCompletionChange_Persists()
    {
        _sut.SoundOnCompletion = true;
        _settingsRepoMock.Verify(s => s.SetAsync("SoundOnCompletion", "true"), Times.Once);
    }

    [Fact]
    public void Notifications_DisableStreamingChange_Persists()
    {
        _sut.DisableStreaming = true;
        _settingsRepoMock.Verify(s => s.SetAsync("DisableStreaming", "true"), Times.Once);
    }

    [Fact]
    public void Notifications_CrossTabCompletionAlertChange_Persists()
    {
        _sut.CrossTabCompletionAlert = false;
        _settingsRepoMock.Verify(s => s.SetAsync("CrossTabCompletionAlert", "false"), Times.Once);
    }

    // ================================================================
    // Startup — LaunchOnWindowsStartup doesn't throw
    // ================================================================

    [Fact]
    public void Startup_LaunchOnWindowsStartupToggle_DoesNotThrow()
    {
        // Verify the registry write/delete logic doesn't throw
        var exception = Record.Exception(() => _sut.LaunchOnWindowsStartup = true);
        Assert.Null(exception);

        exception = Record.Exception(() => _sut.LaunchOnWindowsStartup = false);
        Assert.Null(exception);
    }

    // ================================================================
    // Startup — defaults
    // ================================================================

    [Fact]
    public void Startup_LaunchOnWindowsStartup_DefaultFalse()
    {
        Assert.False(_sut.LaunchOnWindowsStartup);
    }

    [Fact]
    public void Startup_RestoreLastSession_DefaultFalse()
    {
        Assert.False(_sut.RestoreLastSession);
    }

    [Fact]
    public void Startup_MinimizeToTray_DefaultTrue()
    {
        Assert.True(_sut.MinimizeToTray);
    }

    // ================================================================
    // Startup — persistence
    // ================================================================

    [Fact]
    public void Startup_RestoreLastSessionChange_Persists()
    {
        _sut.RestoreLastSession = true;
        _settingsRepoMock.Verify(s => s.SetAsync("RestoreLastSession", "true"), Times.Once);
    }

    [Fact]
    public void Startup_MinimizeToTrayChange_Persists()
    {
        _sut.MinimizeToTray = false;
        _settingsRepoMock.Verify(s => s.SetAsync("MinimizeToTray", "false"), Times.Once);
    }

    // ================================================================
    // Updates — defaults
    // ================================================================

    [Fact]
    public void Updates_UpdateCheckFrequency_DefaultOnStartup()
    {
        Assert.Equal("OnStartup", _sut.UpdateCheckFrequency);
    }

    [Fact]
    public void Updates_FrequencyOptions_ContainsAllFour()
    {
        Assert.Contains("OnStartup", _sut.UpdateCheckFrequencyOptions);
        Assert.Contains("Daily", _sut.UpdateCheckFrequencyOptions);
        Assert.Contains("Weekly", _sut.UpdateCheckFrequencyOptions);
        Assert.Contains("ManualOnly", _sut.UpdateCheckFrequencyOptions);
        Assert.Equal(4, _sut.UpdateCheckFrequencyOptions.Count);
    }

    [Fact]
    public void Updates_CurrentVersion_ReadFromUpdateChecker()
    {
        Assert.Equal("1.0.0.0", _sut.CurrentVersion);
    }

    // ================================================================
    // Updates — persistence
    // ================================================================

    [Fact]
    public void Updates_UpdateCheckFrequencyChange_Persists()
    {
        _sut.UpdateCheckFrequency = "Weekly";
        _settingsRepoMock.Verify(s => s.SetAsync("UpdateCheckFrequency", "Weekly"), Times.Once);
    }

    // ================================================================
    // Updates — CheckForUpdatesCommand
    // ================================================================

    [Fact]
    public async Task Updates_CheckForUpdates_NoUpdateAvailable_ShowsUpToDate()
    {
        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult(false, null, null));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("up to date", _sut.UpdateStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    [Fact]
    public async Task Updates_CheckForUpdates_UpdateAvailable_ShowsVersion()
    {
        var updateInfo = new UpdateInfo(
            new Version(2, 0, 0, 0),
            "Major update",
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            50_000_000,
            "https://example.com/update",
            false);

        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult(true, updateInfo, null));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("2.0.0.0", _sut.UpdateStatusMessage);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    [Fact]
    public async Task Updates_CheckForUpdates_ErrorMessage_ShowsError()
    {
        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult(false, null, "Network timeout"));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("Network timeout", _sut.UpdateStatusMessage);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    [Fact]
    public async Task Updates_CheckForUpdates_Exception_Caught()
    {
        _updateCheckerMock
            .Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("No internet"));

        await _sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Contains("internet", _sut.UpdateStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sut.IsCheckingForUpdates);
    }

    // ================================================================
    // InitializeAsync — loads new settings
    // ================================================================

    [Fact]
    public async Task InitializeAsync_LoadsNewSettings()
    {
        _settingsRepoMock
            .Setup(s => s.GetAsync("AppTheme"))
            .ReturnsAsync("Light");

        _settingsRepoMock
            .Setup(s => s.GetAsync("ChatTheme"))
            .ReturnsAsync("Bubble");

        _settingsRepoMock
            .Setup(s => s.GetAsync("FontFamily"))
            .ReturnsAsync("Consolas");

        _settingsRepoMock
            .Setup(s => s.GetAsync("FontSize"))
            .ReturnsAsync("18.0");

        _settingsRepoMock
            .Setup(s => s.GetAsync("FontWeight"))
            .ReturnsAsync("Bold");

        _settingsRepoMock
            .Setup(s => s.GetAsync("SoundOnCompletion"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("DisableStreaming"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("CrossTabCompletionAlert"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("RestoreLastSession"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("MinimizeToTray"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("UpdateCheckFrequency"))
            .ReturnsAsync("Weekly");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync((string?)null);

        _settingsRepoMock
            .Setup(s => s.GetAsync(It.Is<string>(k => k.StartsWith("LogCategory_"))))
            .ReturnsAsync((string?)null);

        _apiKeyRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ApiKey>());

        _modelConfigRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModelConfiguration>());

        _personaRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Persona>());

        await _sut.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(AppTheme.Light, _sut.AppTheme);
        Assert.Equal(ChatTheme.Bubble, _sut.ChatTheme);
        Assert.Equal("Consolas", _sut.FontFamily);
        Assert.Equal(18.0, _sut.FontSize);
        Assert.Equal("Bold", _sut.FontWeight);
        Assert.True(_sut.SoundOnCompletion);
        Assert.True(_sut.DisableStreaming);
        Assert.False(_sut.CrossTabCompletionAlert);
        Assert.True(_sut.RestoreLastSession);
        Assert.False(_sut.MinimizeToTray);
        Assert.Equal("Weekly", _sut.UpdateCheckFrequency);
    }

    // ================================================================
    // Step 3 — Language
    // ================================================================

    [Fact]
    public void Language_AutoDetectRtl_DefaultTrue()
    {
        Assert.True(_sut.AutoDetectRtl);
    }

    [Fact]
    public void Language_AutoDetectRtlChange_Persists()
    {
        _sut.AutoDetectRtl = false;
        _settingsRepoMock.Verify(s => s.SetAsync("AutoDetectRtl", "false"), Times.Once);
    }

    // ================================================================
    // Step 3 — Maintenance
    // ================================================================

    [Fact]
    public void Maintenance_DatabaseFileSize_InitialEmpty()
    {
        // Default before initialization
        Assert.Equal(string.Empty, _sut.DatabaseFileSize);
    }

    [Fact]
    public void Maintenance_ReclaimableSpace_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.ReclaimableSpace);
    }

    [Fact]
    public void Maintenance_LastCompaction_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.LastCompaction);
    }

    // ================================================================
    // Step 3 — Wiki
    // ================================================================

    [Fact]
    public void Wiki_GitVersionControlEnabled_DefaultFalse()
    {
        Assert.False(_sut.GitVersionControlEnabled);
    }

    [Fact]
    public void Wiki_GitVersionControlEnabledChange_Persists()
    {
        _sut.GitVersionControlEnabled = true;
        _settingsRepoMock.Verify(s => s.SetAsync("GitVersionControlEnabled", "true"), Times.Once);
    }

    [Fact]
    public void Wiki_WikiDirectoryPath_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.WikiDirectoryPath);
    }

    // ================================================================
    // Step 3 — Backup
    // ================================================================

    [Fact]
    public void Backup_BackupSchedule_DefaultDaily()
    {
        Assert.Equal("Daily", _sut.BackupSchedule);
    }

    [Fact]
    public void Backup_BackupScheduleChange_Persists()
    {
        _sut.BackupSchedule = "Weekly";
        _settingsRepoMock.Verify(s => s.SetAsync("BackupSchedule", "Weekly"), Times.Once);
    }

    [Fact]
    public void Backup_LastBackupTime_InitialEmpty()
    {
        Assert.Equal(string.Empty, _sut.LastBackupTime);
    }

    // ================================================================
    // Step 3 — Tools
    // ================================================================

    [Fact]
    public void Tools_WebSearchAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.WebSearchAutoApproval);
    }

    [Fact]
    public void Tools_TerminalAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.TerminalAutoApproval);
    }

    [Fact]
    public void Tools_FileGenerateAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.FileGenerateAutoApproval);
    }

    [Fact]
    public void Tools_FileEditAutoApproval_DefaultAsk()
    {
        Assert.Equal("Ask", _sut.FileEditAutoApproval);
    }

    [Fact]
    public void Tools_WebSearchAutoApprovalChange_Persists()
    {
        _sut.WebSearchAutoApproval = "AutoApprove";
        _settingsRepoMock.Verify(s => s.SetAsync("WebSearchAutoApproval", "AutoApprove"), Times.Once);
    }

    [Fact]
    public void Tools_TerminalChange_Persists()
    {
        _sut.TerminalAutoApproval = "Disabled";
        _settingsRepoMock.Verify(s => s.SetAsync("TerminalAutoApproval", "Disabled"), Times.Once);
    }

    [Fact]
    public void Tools_SttProvider_DefaultOpenAiWhisper()
    {
        Assert.Equal("OpenAI Whisper", _sut.SttProvider);
    }

    [Fact]
    public void Tools_SttProviderChange_Persists()
    {
        _sut.SttProvider = "Windows Speech";
        _settingsRepoMock.Verify(s => s.SetAsync("SttProvider", "Windows Speech"), Times.Once);
    }

    [Fact]
    public void Tools_ToolApprovalOptions_ContainsAllThree()
    {
        Assert.Contains("Ask", _sut.ToolApprovalOptions);
        Assert.Contains("AutoApprove", _sut.ToolApprovalOptions);
        Assert.Contains("Disabled", _sut.ToolApprovalOptions);
        Assert.Equal(3, _sut.ToolApprovalOptions.Count);
    }

    [Fact]
    public void Tools_TerminalApprovalOptions_DoesNotIncludeAutoApprove()
    {
        Assert.Contains("Ask", _sut.TerminalApprovalOptions);
        Assert.Contains("Disabled", _sut.TerminalApprovalOptions);
        Assert.DoesNotContain("AutoApprove", _sut.TerminalApprovalOptions);
        Assert.Equal(2, _sut.TerminalApprovalOptions.Count);
    }

    [Fact]
    public void Tools_SttProviderOptions_ContainsAllThree()
    {
        Assert.Contains("OpenAI Whisper", _sut.SttProviderOptions);
        Assert.Contains("Local Whisper", _sut.SttProviderOptions);
        Assert.Contains("Windows Speech", _sut.SttProviderOptions);
        Assert.Equal(3, _sut.SttProviderOptions.Count);
    }

    // ================================================================
    // Step 3 — Pricing
    // ================================================================

    [Fact]
    public void Pricing_MonthlyBudgetLimit_DefaultNull()
    {
        Assert.Null(_sut.MonthlyBudgetLimit);
    }

    [Fact]
    public void Pricing_MonthlyBudgetLimitChange_Persists()
    {
        _sut.MonthlyBudgetLimit = 50.00m;
        _settingsRepoMock.Verify(s => s.SetAsync("MonthlyBudgetLimit", "50.00"), Times.Once);
    }

    [Fact]
    public void Pricing_WarningThreshold_Default80()
    {
        Assert.Equal(80, _sut.WarningThreshold);
    }

    [Fact]
    public void Pricing_WarningThresholdChange_Persists()
    {
        _sut.WarningThreshold = 90;
        _settingsRepoMock.Verify(s => s.SetAsync("WarningThreshold", "90"), Times.Once);
    }

    [Fact]
    public void Pricing_WarningThresholdClampsBelow50()
    {
        _sut.WarningThreshold = 10;
        Assert.Equal(50, _sut.WarningThreshold);
    }

    [Fact]
    public void Pricing_WarningThresholdClampsAbove100()
    {
        _sut.WarningThreshold = 150;
        Assert.Equal(100, _sut.WarningThreshold);
    }

    [Fact]
    public void Pricing_BlockApiOnLimit_DefaultFalse()
    {
        Assert.False(_sut.BlockApiOnLimit);
    }

    [Fact]
    public void Pricing_BlockApiOnLimitChange_Persists()
    {
        _sut.BlockApiOnLimit = true;
        _settingsRepoMock.Verify(s => s.SetAsync("BlockApiOnLimit", "true"), Times.Once);
    }

    // ================================================================
    // Step 3 — Security
    // ================================================================

    [Fact]
    public void Security_EncryptionStatus_ContainsDpapi()
    {
        Assert.Contains("DPAPI", _sut.EncryptionStatus);
    }

    [Fact]
    public void Security_LockedChatPasswordSet_DefaultFalse()
    {
        Assert.False(_sut.LockedChatPasswordSet);
    }

    [Fact]
    public void Security_HideLockedChats_DefaultFalse()
    {
        Assert.False(_sut.HideLockedChats);
    }

    [Fact]
    public void Security_HideLockedChatsChange_Persists()
    {
        _sut.HideLockedChats = true;
        _settingsRepoMock.Verify(s => s.SetAsync("HideLockedChats", "true"), Times.Once);
    }

    // ================================================================
    // Step 3 — CategoryItems
    // ================================================================

    [Fact]
    public void CategoryItems_Contains16Items()
    {
        Assert.Equal(16, _sut.CategoryItems.Count);
    }

    [Fact]
    public void CategoryItems_ContainsLanguage()
    {
        Assert.Contains(_sut.CategoryItems, c => c.Category == SettingsCategory.Language);
    }

    // ================================================================
    // Step 3 — Placeholder commands
    // ================================================================

    [Fact]
    public void ConfigureBackupCommand_SetsStatusMessage()
    {
        _sut.ConfigureBackupCommand.Execute(null);
        Assert.Contains("Feature 16", _sut.StatusMessage);
    }

    [Fact]
    public void TestMicrophoneCommand_SetsStatusMessage()
    {
        _sut.TestMicrophoneCommand.Execute(null);
        Assert.Contains("not yet implemented", _sut.StatusMessage);
    }

    [Fact]
    public void SetGlobalPasswordCommand_SetsStatusMessage()
    {
        _sut.SetGlobalPasswordCommand.Execute(null);
        Assert.Contains("placeholder", _sut.StatusMessage);
    }

    // ================================================================
    // Step 3 — IsBusy default
    // ================================================================

    [Fact]
    public void IsBusy_DefaultFalse()
    {
        Assert.False(_sut.IsBusy);
    }

    // ================================================================
    // Step 4 — Text Actions
    // ================================================================

    [Fact]
    public void AddTextActionCommand_InitializesFormWithDefaults()
    {
        _sut.AddTextActionCommand.Execute(null);

        Assert.True(_sut.IsEditingTextAction);
        Assert.True(_sut.CaptureSelection);
        Assert.False(_sut.CaptureFocusedElement);
        Assert.False(_sut.CaptureSurroundingContext);
        Assert.False(_sut.CaptureFullDocument);
        Assert.False(_sut.CaptureScreenshot);
        Assert.Equal("replaceSelection", _sut.SelectedApplyMode);
        Assert.Null(_sut.TextActionAssignedHotkey);
        Assert.Null(_sut.EditingTextAction);
    }

    [Fact]
    public async Task SaveTextActionCommand_ValidatesNameIsRequired()
    {
        _sut.AddTextActionCommand.Execute(null);
        _sut.TextActionDisplayNameValue = string.Empty;

        await _sut.SaveTextActionCommand.ExecuteAsync(null);

        Assert.Contains("display name is required", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateTextActionCommand_AppendsCopySuffix()
    {
        var source = new TextAction
        {
            Id = "test-id",
            DisplayName = "Test Action",
            SystemPrompt = "Do something",
            CaptureScope = "selection",
            ApplyMode = "replaceSelection",
        };

        _textActionRepoMock
            .Setup(r => r.GetByIdAsync("test-id"))
            .ReturnsAsync(source);

        _textActionRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<TextAction>()))
            .ReturnsAsync((TextAction a) => a);

        _textActionRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<TextAction>());

        var item = new TextActionDisplayItem { Id = "test-id", DisplayName = "Test Action" };

        await _sut.DuplicateTextActionCommand.ExecuteAsync(item);

        _textActionRepoMock.Verify(r => r.CreateAsync(It.Is<TextAction>(
            ta => ta.DisplayName == "Test Action (Copy)" && ta.Hotkey == null)));
    }

    [Fact]
    public void BuildCaptureScopeString_SelectionOnly_ReturnsSelection()
    {
        _sut.AddTextActionCommand.Execute(null);
        _sut.CaptureSelection = true;
        _sut.CaptureFocusedElement = false;
        _sut.CaptureSurroundingContext = false;
        _sut.CaptureFullDocument = false;
        _sut.CaptureScreenshot = false;

        var method = typeof(SettingsViewModel).GetMethod(
            "BuildCaptureScopeString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(_sut, null) as string;

        Assert.Equal("selection", result);
    }

    [Fact]
    public void BuildCaptureScopeString_MultiSelect_ReturnsCommaSeparated()
    {
        _sut.AddTextActionCommand.Execute(null);
        _sut.CaptureSelection = true;
        _sut.CaptureFocusedElement = true;
        _sut.CaptureScreenshot = true;

        var method = typeof(SettingsViewModel).GetMethod(
            "BuildCaptureScopeString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(_sut, null) as string;

        Assert.Equal("selection,focusedElement,screenshot", result);
    }

    [Fact]
    public void ApplyRecordedHotkey_SetsHotkeyAndStopsRecording()
    {
        _sut.ApplyRecordedHotkey("Ctrl+Shift+K");

        Assert.Equal("Ctrl+Shift+K", _sut.TextActionAssignedHotkey);
        Assert.False(_sut.IsRecordingHotkey);
    }

    // ================================================================
    // Step 4 — Hotkeys
    // ================================================================

    [Fact]
    public void ResetHotkeysToDefaultsCommand_ShowsConfirmation()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), "Reset Hotkeys to Defaults"))
            .Returns(false);

        _sut.ResetHotkeysToDefaultsCommand.Execute(null);

        _textActionRepoMock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    // ================================================================
    // Re-run Onboarding Wizard
    // ================================================================

    [Fact]
    public void ReRunOnboardingCommand_DoesNotThrow()
    {
        // Verify the command executes without exception (the messenger fires but
        // no recipient may be registered in a unit test context)
        var exception = Record.Exception(() => _sut.ReRunOnboardingCommand.Execute(null));
        Assert.Null(exception);
    }
}
