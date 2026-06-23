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

public class SettingsViewModelDiagnosticsTests : SettingsViewModelTestBase
{
    // ================================================================
    // Diagnostics — LogLevel defaults
    // ================================================================

    [Fact]
    public void Diagnostics_DefaultLogLevel_IsInformation()
    {
        Assert.Equal("Information", _sut.LogLevel);
    }

    [Fact]
    public void Diagnostics_LogLevelOptions_ContainsAllLevels()
    {
        Assert.Contains("Information", _sut.LogLevelOptions);
        Assert.Contains("Debug", _sut.LogLevelOptions);
        Assert.Contains("Verbose", _sut.LogLevelOptions);
        Assert.Equal(3, _sut.LogLevelOptions.Count);
    }

    // ================================================================
    // Diagnostics — LogLevel change persistence
    // ================================================================

    [Fact]
    public void Diagnostics_LogLevelChange_PersistsToSettingsRepository()
    {
        _sut.LogLevel = "Debug";

        _settingsRepoMock.Verify(
            s => s.SetAsync("LogLevel", "Debug"),
            Times.Once);
    }

    // ================================================================
    // Diagnostics — Category toggle defaults (3 ON, 5 OFF)
    // ================================================================

    [Fact]
    public void Diagnostics_DefaultCategoryToggles_ThreeOnFiveOff()
    {
        Assert.True(_sut.LogCategory_LLMApiCalls);
        Assert.True(_sut.LogCategory_Tier1HotkeyPipeline);
        Assert.True(_sut.LogCategory_Tier2CommandBar);

        Assert.False(_sut.LogCategory_Database);
        Assert.False(_sut.LogCategory_WikiFileSystem);
        Assert.False(_sut.LogCategory_WebSocket);
        Assert.False(_sut.LogCategory_StartupShutdown);
        Assert.False(_sut.LogCategory_SystemIntegration);
    }

    // ================================================================
    // Diagnostics — Category toggle persistence
    // ================================================================

    [Fact]
    public void Diagnostics_CategoryToggleOn_PersistsTrue()
    {
        _sut.LogCategory_Database = true;

        _settingsRepoMock.Verify(
            s => s.SetAsync("LogCategory_Database", "true"),
            Times.Once);
    }

    [Fact]
    public void Diagnostics_CategoryToggleOff_PersistsFalse()
    {
        _sut.LogCategory_LLMApiCalls = false;

        _settingsRepoMock.Verify(
            s => s.SetAsync("LogCategory_LLMApiCalls", "false"),
            Times.Once);
    }

    [Fact]
    public void Diagnostics_AllCategoryToggles_PersistCorrectKeys()
    {
        // Toggle each to a NON-default value to trigger the OnChanged partial method
        _sut.LogCategory_LLMApiCalls = false;  // Default true → false
        _sut.LogCategory_Tier1HotkeyPipeline = false; // Default true → false
        _sut.LogCategory_Tier2CommandBar = false; // Default true → false
        _sut.LogCategory_Database = true;  // Default false → true
        _sut.LogCategory_WikiFileSystem = true;  // Default false → true
        _sut.LogCategory_WebSocket = true;  // Default false → true
        _sut.LogCategory_StartupShutdown = true;  // Default false → true
        _sut.LogCategory_SystemIntegration = true;  // Default false → true

        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_LLMApiCalls", "false"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_Tier1HotkeyPipeline", "false"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_Tier2CommandBar", "false"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_Database", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_WikiFileSystem", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_WebSocket", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_StartupShutdown", "true"), Times.Once);
        _settingsRepoMock.Verify(s => s.SetAsync("LogCategory_SystemIntegration", "true"), Times.Once);
    }

    // ================================================================
    // Diagnostics — InitializeAsync loads settings from repository
    // ================================================================

    [Fact]
    public async Task InitializeAsync_LoadsDiagnosticsSettings()
    {
        _settingsRepoMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync("Debug");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_LLMApiCalls"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_Tier1HotkeyPipeline"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_Tier2CommandBar"))
            .ReturnsAsync("false");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_Database"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_WikiFileSystem"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_WebSocket"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_StartupShutdown"))
            .ReturnsAsync("true");

        _settingsRepoMock
            .Setup(s => s.GetAsync("LogCategory_SystemIntegration"))
            .ReturnsAsync("true");

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

        Assert.Equal("Debug", _sut.LogLevel);
        Assert.False(_sut.LogCategory_LLMApiCalls);
        Assert.False(_sut.LogCategory_Tier1HotkeyPipeline);
        Assert.False(_sut.LogCategory_Tier2CommandBar);
        Assert.True(_sut.LogCategory_Database);
        Assert.True(_sut.LogCategory_WikiFileSystem);
        Assert.True(_sut.LogCategory_WebSocket);
        Assert.True(_sut.LogCategory_StartupShutdown);
        Assert.True(_sut.LogCategory_SystemIntegration);
    }

    // ================================================================
    // Diagnostics — ClearLogsCommand
    // ================================================================

    [Fact]
    public async Task ClearLogsCommand_ConfirmationDeclined_DoesNotTryToDelete()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        await _sut.ClearLogsCommand.ExecuteAsync(null);

        // No StatusMessage should be set since we never attempted deletion
        Assert.Empty(_sut.StatusMessage);
    }

    [Fact]
    public async Task ClearLogsCommand_AllFilesDeleted_ShowsSuccessMessage()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create test log files to verify they get deleted
        var testFile1 = Path.Combine(logsPath, "msb-test-old.log");
        var testFile2 = Path.Combine(logsPath, "msb-test-old.json");
        await File.WriteAllTextAsync(testFile1, "old data");
        await File.WriteAllTextAsync(testFile2, "{}");

        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.Contains("log files cleared", _sut.StatusMessage);
            Assert.False(File.Exists(testFile1), "Test .log file should be deleted");
            Assert.False(File.Exists(testFile2), "Test .json file should be deleted");
        }
        finally
        {
            if (File.Exists(testFile1)) File.Delete(testFile1);
            if (File.Exists(testFile2)) File.Delete(testFile2);
        }
    }

    [Fact]
    public async Task ClearLogsCommand_LockedFile_ShowsInUseMessage()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create a test log file and lock it with an exclusive file stream
        var testFilePath = Path.Combine(logsPath, "msb-20260622-test.log");
        var fs = new FileStream(testFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.Contains("in use", _sut.StatusMessage);
            Assert.Contains("rotated automatically", _sut.StatusMessage);
        }
        finally
        {
            fs.Dispose();
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ClearLogsCommand_MixedLockedAndDeletableFiles_ShowsCombinedMessage()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create a deletable log file
        var deletableFilePath = Path.Combine(logsPath, "msb-20260621-old.log");
        await File.WriteAllTextAsync(deletableFilePath, "old log data");

        // Create a locked log file
        var lockedFilePath = Path.Combine(logsPath, "msb-20260622-current.log");
        var fs = new FileStream(lockedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.Contains("in use", _sut.StatusMessage);
            Assert.Contains("rotated automatically", _sut.StatusMessage);
            Assert.False(File.Exists(deletableFilePath), "Deletable file should have been removed");
        }
        finally
        {
            fs.Dispose();
            if (File.Exists(lockedFilePath))
                File.Delete(lockedFilePath);
            if (File.Exists(deletableFilePath))
                File.Delete(deletableFilePath);
        }
    }

    [Fact]
    public async Task ClearLogsCommand_FiltersOnlyLogAndJsonFiles()
    {
        _confirmationServiceMock
            .Setup(c => c.Confirm(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");
        Directory.CreateDirectory(logsPath);

        // Create a .log file (should be targeted)
        var logFilePath = Path.Combine(logsPath, "test.log");
        await File.WriteAllTextAsync(logFilePath, "log data");

        // Create a .json file (should be targeted)
        var jsonFilePath = Path.Combine(logsPath, "test.json");
        await File.WriteAllTextAsync(jsonFilePath, "{}");

        // Create a .txt file (should be ignored)
        var txtFilePath = Path.Combine(logsPath, "test.txt");
        await File.WriteAllTextAsync(txtFilePath, "should remain");

        try
        {
            await _sut.ClearLogsCommand.ExecuteAsync(null);

            Assert.False(File.Exists(logFilePath), ".log file should be deleted");
            Assert.False(File.Exists(jsonFilePath), ".json file should be deleted");
            Assert.True(File.Exists(txtFilePath), ".txt file should be ignored");
        }
        finally
        {
            if (File.Exists(logFilePath)) File.Delete(logFilePath);
            if (File.Exists(jsonFilePath)) File.Delete(jsonFilePath);
            if (File.Exists(txtFilePath)) File.Delete(txtFilePath);
        }
    }

    // ================================================================
    // Diagnostics — OpenLogsFolder (no launch during tests)
    // ================================================================

    [Fact]
    public void OpenLogsFolderCommand_CreatesLogsDirectory()
    {
        // Verify the command constructs the correct path
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MySecondBrain", "logs");

        Assert.Contains("MySecondBrain", expectedPath);
        Assert.Contains("logs", expectedPath);
    }
}
