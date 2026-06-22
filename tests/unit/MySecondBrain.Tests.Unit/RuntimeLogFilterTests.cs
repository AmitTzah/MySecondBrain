using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Services.Logging;

namespace MySecondBrain.Tests.Unit;

public class RuntimeLogFilterTests
{
    private readonly Mock<ILogger<TestService>> _innerMock;
    private readonly Mock<ISettingsRepository> _settingsMock;
    private readonly RuntimeLogFilter<TestService> _sut;

    public RuntimeLogFilterTests()
    {
        _innerMock = new Mock<ILogger<TestService>>();
        _settingsMock = new Mock<ISettingsRepository>();

        // Default: Information level, all categories enabled
        _settingsMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync("Information");

        _sut = new RuntimeLogFilter<TestService>(_innerMock.Object, _settingsMock.Object);
    }

    // ================================================================
    // LogLevel filtering — Information
    // ================================================================

    [Fact]
    public void IsEnabled_InformationLevelWhenConfiguredInformation_ReturnsTrue()
    {
        var result = _sut.IsEnabled(LogLevel.Information);

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_DebugLevelWhenConfiguredInformation_ReturnsFalse()
    {
        var result = _sut.IsEnabled(LogLevel.Debug);

        Assert.False(result);
    }

    [Fact]
    public void IsEnabled_TraceLevelWhenConfiguredInformation_ReturnsFalse()
    {
        var result = _sut.IsEnabled(LogLevel.Trace);

        Assert.False(result);
    }

    // ================================================================
    // LogLevel filtering — Debug
    // ================================================================

    [Fact]
    public void IsEnabled_DebugLevelWhenConfiguredDebug_ReturnsTrue()
    {
        _settingsMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync("Debug");

        Assert.True(_sut.IsEnabled(LogLevel.Debug));
        Assert.True(_sut.IsEnabled(LogLevel.Information));
        Assert.False(_sut.IsEnabled(LogLevel.Trace));
    }

    // ================================================================
    // LogLevel filtering — Verbose (Trace)
    // ================================================================

    [Fact]
    public void IsEnabled_AllLevelsWhenConfiguredVerbose_ReturnsTrue()
    {
        _settingsMock
            .Setup(s => s.GetAsync("LogLevel"))
            .ReturnsAsync("Verbose");

        Assert.True(_sut.IsEnabled(LogLevel.Trace));
        Assert.True(_sut.IsEnabled(LogLevel.Debug));
        Assert.True(_sut.IsEnabled(LogLevel.Information));
    }

    // ================================================================
    // Category filtering
    // ================================================================

    [Fact]
    public void IsEnabled_DisabledCategory_ReturnsFalse()
    {
        _settingsMock
            .Setup(s => s.GetAsync("LogCategory_LLMApiCalls"))
            .ReturnsAsync("false");

        var inner = new Mock<ILogger<LLMService>>();
        var filter = new RuntimeLogFilter<LLMService>(inner.Object, _settingsMock.Object);
        var result = filter.IsEnabled(LogLevel.Information);

        Assert.False(result);
    }

    [Fact]
    public void IsEnabled_EnabledCategory_ReturnsTrue()
    {
        _settingsMock
            .Setup(s => s.GetAsync("LogCategory_LLMApiCalls"))
            .ReturnsAsync("true");

        var inner = new Mock<ILogger<LLMService>>();
        var filter = new RuntimeLogFilter<LLMService>(inner.Object, _settingsMock.Object);
        var result = filter.IsEnabled(LogLevel.Information);

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_UncategorizedService_ReturnsTrue()
    {
        // TestService is uncategorized — always log if level passes
        var result = _sut.IsEnabled(LogLevel.Information);

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_CategorySettingNull_DefaultsToEnabled()
    {
        _settingsMock
            .Setup(s => s.GetAsync("LogCategory_SystemIntegration"))
            .ReturnsAsync((string?)null);

        var inner = new Mock<ILogger<SystemIntegrationService>>();
        var filter = new RuntimeLogFilter<SystemIntegrationService>(inner.Object, _settingsMock.Object);
        var result = filter.IsEnabled(LogLevel.Information);

        Assert.True(result);
    }

    // ================================================================
    // Log method — delegates to inner when enabled, skips when disabled
    // ================================================================

    [Fact]
    public void Log_WhenEnabled_CallsInnerLog()
    {
        var state = "test message";
        Exception? exception = null;

        _sut.Log(LogLevel.Information, 0, state, exception, (s, _) => s!);

        _innerMock.Verify(
            i => i.Log(LogLevel.Information, 0, state, exception, It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Log_WhenDisabled_DoesNotCallInnerLog()
    {
        // Default level is Information — Debug should be filtered out
        var state = "debug message";
        _sut.Log(LogLevel.Debug, 0, state, null, (s, _) => s!);

        _innerMock.Verify(
            i => i.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<string>(),
                It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()),
            Times.Never);
    }

    // ================================================================
    // BeginScope — delegates to inner
    // ================================================================

    [Fact]
    public void BeginScope_DelegatesToInner()
    {
        var scope = new Mock<IDisposable>();
        _innerMock
            .Setup(i => i.BeginScope("scope-state"))
            .Returns(scope.Object);

        var result = _sut.BeginScope("scope-state");

        Assert.Same(scope.Object, result);
    }

    // ================================================================
    // Category mapping — verified via distinct service type names
    // ================================================================

    [Fact]
    public void CategoryMapping_LLMService_ChecksLLMApiCallsKey()
    {
        var inner = new Mock<ILogger<LLMService>>();
        var filter = new RuntimeLogFilter<LLMService>(inner.Object, _settingsMock.Object);

        filter.IsEnabled(LogLevel.Information);

        _settingsMock.Verify(s => s.GetAsync("LogCategory_LLMApiCalls"), Times.AtLeastOnce);
    }

    [Fact]
    public void CategoryMapping_SystemService_ChecksSystemIntegrationKey()
    {
        var inner = new Mock<ILogger<SystemIntegrationService>>();
        var filter = new RuntimeLogFilter<SystemIntegrationService>(inner.Object, _settingsMock.Object);

        filter.IsEnabled(LogLevel.Information);

        _settingsMock.Verify(s => s.GetAsync("LogCategory_SystemIntegration"), Times.AtLeastOnce);
    }

    [Fact]
    public void CategoryMapping_UncategorizedService_DoesNotCheckAnyCategoryKey()
    {
        // TestService is uncategorized — should not check any LogCategory_* key
        _sut.IsEnabled(LogLevel.Information);

        _settingsMock.Verify(s => s.GetAsync(It.Is<string>(k => k.StartsWith("LogCategory_"))), Times.Never);
    }
}

// ================================================================
// Test service types for category mapping
// ================================================================

public class TestService { }
public class LLMService { }
public class SystemIntegrationService { }
