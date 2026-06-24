using Microsoft.Extensions.Logging;
using Moq;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.Tools;

namespace MySecondBrain.Tests.Unit;

public class ToolExecutorTests
{
    // ════════════════════════════════════════════════════════════════
    // BashToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void BashToolExecutor_ToolName_ReturnsBash()
    {
        var executor = CreateBashExecutor();
        Assert.Equal("bash", executor.ToolName);
    }

    [Fact]
    public void BashToolExecutor_RiskLevel_IsMedium()
    {
        var executor = CreateBashExecutor();
        Assert.Equal(ToolRiskLevel.Medium, executor.RiskLevel);
    }

    [Fact]
    public void BashToolExecutor_CanAutoApprove_IsFalse()
    {
        var executor = CreateBashExecutor();
        Assert.False(executor.CanAutoApprove);
    }

    [Fact]
    public void BashToolExecutor_RequiresUserConfirmation_IsTrue()
    {
        var executor = CreateBashExecutor();
        Assert.True(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void BashToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateBashExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_WithValidCommand_ReturnsValid()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test-id", "bash", """{"command":"echo hello"}""");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_MissingCommandField_ReturnsInvalid()
    {
        var executor = CreateBashExecutor();
        var toolCall = CreateToolCall("bash");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void BashToolExecutor_GetConfirmationDescription_ReturnsNonEmpty()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test-id", "bash", """{"command":"echo hello"}""");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal("Execute: echo hello", description);
    }

    // ════════════════════════════════════════════════════════════════
    // BashToolExecutor — Workspace isolation tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void BashToolExecutor_WorkspacePath_IsUnderLocalAppData()
    {
        var workspacePath = BashToolExecutor.WorkspacePath;
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            workspacePath);
    }

    [Fact]
    public void BashToolExecutor_WorkspacePath_EndsWithMySecondBrainWorkspace()
    {
        var workspacePath = BashToolExecutor.WorkspacePath;
        Assert.Contains("MySecondBrain", workspacePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace", workspacePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BashToolExecutor_Description_IncludesWorkspacePath()
    {
        var executor = CreateBashExecutor();
        var desc = executor.Description;
        Assert.Contains(BashToolExecutor.WorkspacePath, desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BashToolExecutor_Description_IncludesCmdExe()
    {
        var executor = CreateBashExecutor();
        var desc = executor.Description;
        Assert.Contains("cmd.exe", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_DetectsDriveLetter()
    {
        Assert.True(BashToolExecutor.ContainsBlockedPath(@"type D:\config.json"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_DetectsDriveLetterWithForwardSlash()
    {
        Assert.True(BashToolExecutor.ContainsBlockedPath(@"type D:/config.json"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_DetectsPercentEnvVar()
    {
        Assert.True(BashToolExecutor.ContainsBlockedPath(@"echo %TEMP%\file.txt"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_DetectsTilde()
    {
        Assert.True(BashToolExecutor.ContainsBlockedPath(@"ls ~/Documents"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_SimpleCommand_ReturnsFalse()
    {
        Assert.False(BashToolExecutor.ContainsBlockedPath("echo hello"));
        Assert.False(BashToolExecutor.ContainsBlockedPath("python script.py"));
        Assert.False(BashToolExecutor.ContainsBlockedPath("npm install"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_WorkspaceDrivePath_NowBlocked()
    {
        // All absolute drive letter paths are blocked, even on the workspace drive
        // The model must use relative paths for workspace files
        Assert.True(BashToolExecutor.ContainsBlockedPath(@"type C:\Windows\system.ini"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_AllowsLocalAppDataVar()
    {
        // LOCALAPPDATA is explicitly allowed (resolves to workspace path chain)
        Assert.False(BashToolExecutor.ContainsBlockedPath(@"echo %LOCALAPPDATA%\MySecondBrain\workspace\test.txt"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_AllowsUserProfileVar()
    {
        // USERPROFILE is explicitly allowed
        Assert.False(BashToolExecutor.ContainsBlockedPath(@"echo %USERPROFILE%\MySecondBrain\workspace\test.txt"));
    }

    [Fact]
    public void BashToolExecutor_ContainsBlockedPath_EmptyStringReturnsFalse()
    {
        Assert.False(BashToolExecutor.ContainsBlockedPath(string.Empty));
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_BlocksDriveLetterPaths()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test", "bash", """{"command":"type D:\\config.json"}""");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("absolute path", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_BlocksHeredocSyntax()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test", "bash", """{"command":"cat > file.txt << 'EOF'\nline1\nEOF"}""");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("heredoc", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_PassesSimpleCommand()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test", "bash", """{"command":"echo hello"}""");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_BlocksWikiDirectoryWrites()
    {
        // Wiki path nested under workspace so relative paths resolve correctly
        var wikiPath = Path.Combine(BashToolExecutor.WorkspacePath, "wiki");
        var executor = CreateBashExecutor(wikiPath);
        var toolCall = new ToolCall("test", "bash",
            """{"command":"echo test > wiki/test.md"}""");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("wiki", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_BlocksWikiDirectoryWrites_WithAppendRedirect()
    {
        var wikiPath = Path.Combine(BashToolExecutor.WorkspacePath, "wiki");
        var executor = CreateBashExecutor(wikiPath);
        var toolCall = new ToolCall("test", "bash",
            """{"command":"echo test >> wiki/test.md"}""");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("wiki", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BashToolExecutor_ParametersJsonSchema_IsValidJson()
    {
        var executor = CreateBashExecutor();
        var schema = executor.ParametersJsonSchema;
        Assert.NotNull(schema);
        Assert.Contains("command", schema);
        // Verify it parses
        using var doc = System.Text.Json.JsonDocument.Parse(schema);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task BashToolExecutor_ValidateAsync_NullArguments_ReturnsInvalid()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test", "bash", "{}");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void BashToolExecutor_GetConfirmationDescription_IncludesCommand()
    {
        var executor = CreateBashExecutor();
        var toolCall = new ToolCall("test", "bash", """{"command":"pip install requests"}""");
        var desc = executor.GetConfirmationDescription(toolCall);
        Assert.Contains("pip install", desc);
    }

    [Fact]
    public void BashToolExecutor_GetConfirmationDescription_TruncatesLongCommands()
    {
        var executor = CreateBashExecutor();
        var longCmd = new string('x', 500);
        var toolCall = new ToolCall("test", "bash", $"{{\"command\":\"{longCmd}\"}}");
        var desc = executor.GetConfirmationDescription(toolCall);
        Assert.Contains("...", desc);
    }

    [Fact]
    public void BashToolExecutor_StartupCleanup_CreatesWorkspaceDirectory()
    {
        // Reset static cleanup flag so PerformStartupCleanup runs again
        var field = typeof(BashToolExecutor).GetField("_cleanupPerformed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field?.SetValue(null, false);

        var workspacePath = BashToolExecutor.WorkspacePath;
        try
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);

            BashToolExecutor.PerformStartupCleanup();
            Assert.True(Directory.Exists(workspacePath));
        }
        finally
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);

            // Reset cleanup flag back for subsequent tests
            field?.SetValue(null, false);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // TextEditorToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void TextEditorToolExecutor_ToolName_ReturnsTextEditor()
    {
        var executor = CreateTextEditorExecutor();
        Assert.Equal("text_editor", executor.ToolName);
    }

    [Fact]
    public void TextEditorToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreateTextEditorExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void TextEditorToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreateTextEditorExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void TextEditorToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreateTextEditorExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void TextEditorToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateTextEditorExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task TextEditorToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateTextEditorExecutor();
        var toolCall = CreateToolCall("text_editor");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TextEditorToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateTextEditorExecutor();
        var toolCall = CreateToolCall("text_editor");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void TextEditorToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateTextEditorExecutor();
        var toolCall = CreateToolCall("text_editor");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // WebFetchToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void WebFetchToolExecutor_ToolName_ReturnsWebFetch()
    {
        var executor = CreateWebFetchExecutor();
        Assert.Equal("web_fetch", executor.ToolName);
    }

    [Fact]
    public void WebFetchToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreateWebFetchExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void WebFetchToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreateWebFetchExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void WebFetchToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreateWebFetchExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void WebFetchToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateWebFetchExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task WebFetchToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateWebFetchExecutor();
        var toolCall = CreateToolCall("web_fetch");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task WebFetchToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateWebFetchExecutor();
        var toolCall = CreateToolCall("web_fetch");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void WebFetchToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateWebFetchExecutor();
        var toolCall = CreateToolCall("web_fetch");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // MemoryToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void MemoryToolExecutor_ToolName_ReturnsMemory()
    {
        var executor = CreateMemoryExecutor();
        Assert.Equal("memory", executor.ToolName);
    }

    [Fact]
    public void MemoryToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreateMemoryExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void MemoryToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreateMemoryExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void MemoryToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreateMemoryExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void MemoryToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateMemoryExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task MemoryToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateMemoryExecutor();
        var toolCall = CreateToolCall("memory");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task MemoryToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateMemoryExecutor();
        var toolCall = CreateToolCall("memory");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void MemoryToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateMemoryExecutor();
        var toolCall = CreateToolCall("memory");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // SkillLoadToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void SkillLoadToolExecutor_ToolName_ReturnsSkillLoad()
    {
        var executor = CreateSkillLoadExecutor();
        Assert.Equal("skill_load", executor.ToolName);
    }

    [Fact]
    public void SkillLoadToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreateSkillLoadExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void SkillLoadToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreateSkillLoadExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void SkillLoadToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreateSkillLoadExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void SkillLoadToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateSkillLoadExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task SkillLoadToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateSkillLoadExecutor();
        var toolCall = CreateToolCall("skill_load");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SkillLoadToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateSkillLoadExecutor();
        var toolCall = CreateToolCall("skill_load");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void SkillLoadToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateSkillLoadExecutor();
        var toolCall = CreateToolCall("skill_load");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // AskUserInputToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void AskUserInputToolExecutor_ToolName_ReturnsAskUserInput()
    {
        var executor = CreateAskUserInputExecutor();
        Assert.Equal("ask_user_input", executor.ToolName);
    }

    [Fact]
    public void AskUserInputToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreateAskUserInputExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void AskUserInputToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreateAskUserInputExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void AskUserInputToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreateAskUserInputExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void AskUserInputToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateAskUserInputExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task AskUserInputToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateAskUserInputExecutor();
        var toolCall = CreateToolCall("ask_user_input");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AskUserInputToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateAskUserInputExecutor();
        var toolCall = CreateToolCall("ask_user_input");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void AskUserInputToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateAskUserInputExecutor();
        var toolCall = CreateToolCall("ask_user_input");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // PresentFilesToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void PresentFilesToolExecutor_ToolName_ReturnsPresentFiles()
    {
        var executor = CreatePresentFilesExecutor();
        Assert.Equal("present_files", executor.ToolName);
    }

    [Fact]
    public void PresentFilesToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreatePresentFilesExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void PresentFilesToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreatePresentFilesExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void PresentFilesToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreatePresentFilesExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void PresentFilesToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreatePresentFilesExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task PresentFilesToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreatePresentFilesExecutor();
        var toolCall = CreateToolCall("present_files");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PresentFilesToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreatePresentFilesExecutor();
        var toolCall = CreateToolCall("present_files");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void PresentFilesToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreatePresentFilesExecutor();
        var toolCall = CreateToolCall("present_files");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // ImageSearchToolExecutor tests
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ImageSearchToolExecutor_ToolName_ReturnsImageSearch()
    {
        var executor = CreateImageSearchExecutor();
        Assert.Equal("image_search", executor.ToolName);
    }

    [Fact]
    public void ImageSearchToolExecutor_RiskLevel_IsLow()
    {
        var executor = CreateImageSearchExecutor();
        Assert.Equal(ToolRiskLevel.Low, executor.RiskLevel);
    }

    [Fact]
    public void ImageSearchToolExecutor_CanAutoApprove_IsTrue()
    {
        var executor = CreateImageSearchExecutor();
        Assert.True(executor.CanAutoApprove);
    }

    [Fact]
    public void ImageSearchToolExecutor_RequiresUserConfirmation_IsFalse()
    {
        var executor = CreateImageSearchExecutor();
        Assert.False(executor.RequiresUserConfirmation);
    }

    [Fact]
    public void ImageSearchToolExecutor_ImplementsIToolExecutor()
    {
        var executor = CreateImageSearchExecutor();
        Assert.IsAssignableFrom<IToolExecutor>(executor);
    }

    [Fact]
    public async Task ImageSearchToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateImageSearchExecutor();
        var toolCall = CreateToolCall("image_search");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ImageSearchToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateImageSearchExecutor();
        var toolCall = CreateToolCall("image_search");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void ImageSearchToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateImageSearchExecutor();
        var toolCall = CreateToolCall("image_search");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
    }

    // ════════════════════════════════════════════════════════════════
    // Private helpers
    // ════════════════════════════════════════════════════════════════

    private static ToolCall CreateToolCall(string toolName) =>
        new("test-id", toolName, "{}");

    private static BashToolExecutor CreateBashExecutor(string? wikiDirectoryPath = null)
    {
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        return new BashToolExecutor(loggerMock.Object, wikiDirectoryPath);
    }

    private static TextEditorToolExecutor CreateTextEditorExecutor()
    {
        var loggerMock = new Mock<ILogger<TextEditorToolExecutor>>();
        return new TextEditorToolExecutor(loggerMock.Object);
    }

    private static WebFetchToolExecutor CreateWebFetchExecutor()
    {
        var loggerMock = new Mock<ILogger<WebFetchToolExecutor>>();
        return new WebFetchToolExecutor(loggerMock.Object);
    }

    private static MemoryToolExecutor CreateMemoryExecutor()
    {
        var loggerMock = new Mock<ILogger<MemoryToolExecutor>>();
        return new MemoryToolExecutor(loggerMock.Object);
    }

    private static SkillLoadToolExecutor CreateSkillLoadExecutor()
    {
        var skillLoaderMock = new Mock<ISkillLoader>();
        var loggerMock = new Mock<ILogger<SkillLoadToolExecutor>>();
        return new SkillLoadToolExecutor(skillLoaderMock.Object, loggerMock.Object);
    }

    private static AskUserInputToolExecutor CreateAskUserInputExecutor()
    {
        var confirmationServiceMock = new Mock<IConfirmationService>();
        var loggerMock = new Mock<ILogger<AskUserInputToolExecutor>>();
        return new AskUserInputToolExecutor(confirmationServiceMock.Object, loggerMock.Object);
    }

    private static PresentFilesToolExecutor CreatePresentFilesExecutor()
    {
        var loggerMock = new Mock<ILogger<PresentFilesToolExecutor>>();
        return new PresentFilesToolExecutor(loggerMock.Object);
    }

    private static ImageSearchToolExecutor CreateImageSearchExecutor()
    {
        var searchProvidersMock = new Mock<IEnumerable<ISearchProvider>>();
        var loggerMock = new Mock<ILogger<ImageSearchToolExecutor>>();
        return new ImageSearchToolExecutor(searchProvidersMock.Object, loggerMock.Object);
    }
}
