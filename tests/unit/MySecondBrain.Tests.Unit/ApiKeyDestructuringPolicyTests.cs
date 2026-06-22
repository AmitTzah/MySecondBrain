using MySecondBrain.Services.Logging;
using Serilog.Core;
using Serilog.Events;

namespace MySecondBrain.Tests.Unit;

public class ApiKeyDestructuringPolicyTests
{
    private readonly ApiKeyDestructuringPolicy _sut = new();
    private readonly Mock<ILogEventPropertyValueFactory> _factoryMock = new();

    // ================================================================
    // OpenAI key patterns
    // ================================================================

    [Fact]
    public void TryDestructure_OpenAiKey_ReturnsRedacted()
    {
        var result = TryDestructure("sk-" + new string('a', 48));

        Assert.True(result.Redacted);
        Assert.Equal("[REDACTED]", result.Value);
    }

    [Fact]
    public void TryDestructure_OpenAiSkProjKey_ReturnsRedacted()
    {
        var result = TryDestructure("sk-proj-" + new string('b', 48));

        Assert.True(result.Redacted);
        Assert.Equal("[REDACTED]", result.Value);
    }

    // ================================================================
    // Anthropic key patterns
    // ================================================================

    [Fact]
    public void TryDestructure_AnthropicKey_ReturnsRedacted()
    {
        var result = TryDestructure("sk-ant-" + new string('c', 48));

        Assert.True(result.Redacted);
        Assert.Equal("[REDACTED]", result.Value);
    }

    // ================================================================
    // Google key patterns
    // ================================================================

    [Fact]
    public void TryDestructure_GoogleKey_ReturnsRedacted()
    {
        var result = TryDestructure("AIzaSy" + new string('d', 33));

        Assert.True(result.Redacted);
        Assert.Equal("[REDACTED]", result.Value);
    }

    // ================================================================
    // Non-key strings — should NOT be redacted
    // ================================================================

    [Fact]
    public void TryDestructure_ShortString_ReturnsFalse()
    {
        var result = TryDestructure("short");

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryDestructure_NormalText_ReturnsFalse()
    {
        var result = TryDestructure("Hello, this is a normal log message.");

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryDestructure_EmailAddress_ReturnsFalse()
    {
        var result = TryDestructure("user@example.com");

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryDestructure_ConnectionString_ReturnsFalse()
    {
        // Connection strings look like keys but don't match API key prefixes
        var result = TryDestructure("Server=localhost;Database=msb;User=admin;Password=secret!;");

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    // ================================================================
    // Edge cases
    // ================================================================

    [Fact]
    public void TryDestructure_NullValue_ReturnsFalse()
    {
        _sut.TryDestructure(null!, _factoryMock.Object, out var result);

        Assert.Null(result);
        Assert.False(_sut.TryDestructure(null!, _factoryMock.Object, out _));
    }

    [Fact]
    public void TryDestructure_EmptyString_ReturnsFalse()
    {
        var result = TryDestructure(string.Empty);

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryDestructure_NonStringObject_ReturnsFalse()
    {
        _sut.TryDestructure(12345, _factoryMock.Object, out var result);

        Assert.Null(result);
    }

    [Fact]
    public void TryDestructure_ApiKeyJustBelow20Chars_ReturnsFalse()
    {
        // 19 chars — below the 20-char threshold
        var result = TryDestructure("sk-" + new string('x', 16));

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryDestructure_ApiKeyExactly20Chars_ReturnsRedacted()
    {
        var result = TryDestructure("sk-" + new string('x', 17)); // "sk-" + 17 = 20

        Assert.True(result.Redacted);
        Assert.Equal("[REDACTED]", result.Value);
    }

    // ================================================================
    // Case sensitivity
    // ================================================================

    [Fact]
    public void TryDestructure_OpenAiKeyUppercase_ReturnsRedacted()
    {
        var result = TryDestructure("SK-" + new string('A', 48));

        Assert.True(result.Redacted);
        Assert.Equal("[REDACTED]", result.Value);
    }

    [Fact]
    public void TryDestructure_GoogleKeyLowercasePrefix_NotRedacted()
    {
        // Google API keys are case-sensitive — lowercase "aiza" should not match
        var result = TryDestructure("aizaSy" + new string('d', 33));

        Assert.False(result.Redacted);
        Assert.Null(result.Value);
    }

    // ================================================================
    // Helpers
    // ================================================================

    private (bool Redacted, string? Value) TryDestructure(string input)
    {
        var redacted = _sut.TryDestructure(input, _factoryMock.Object, out var result);
        return (redacted, (result as ScalarValue)?.Value?.ToString());
    }
}
