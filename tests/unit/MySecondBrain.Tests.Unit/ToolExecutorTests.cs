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
    public async Task BashToolExecutor_ValidateAsync_ReturnsNull()
    {
        var executor = CreateBashExecutor();
        var toolCall = CreateToolCall("bash");
        var result = await executor.ValidateAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BashToolExecutor_ExecuteAsync_ReturnsNull()
    {
        var executor = CreateBashExecutor();
        var toolCall = CreateToolCall("bash");
        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void BashToolExecutor_GetConfirmationDescription_ReturnsEmpty()
    {
        var executor = CreateBashExecutor();
        var toolCall = CreateToolCall("bash");
        var description = executor.GetConfirmationDescription(toolCall);

        Assert.Equal(string.Empty, description);
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
    // Private helpers
    // ════════════════════════════════════════════════════════════════

    private static ToolCall CreateToolCall(string toolName) =>
        new("test-id", toolName, "{}");

    private static BashToolExecutor CreateBashExecutor()
    {
        var loggerMock = new Mock<ILogger<BashToolExecutor>>();
        return new BashToolExecutor(loggerMock.Object);
    }

    private static TextEditorToolExecutor CreateTextEditorExecutor()
    {
        var loggerMock = new Mock<ILogger<TextEditorToolExecutor>>();
        return new TextEditorToolExecutor(loggerMock.Object);
    }
}
