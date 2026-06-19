using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Core.Models;
using MySecondBrain.Services.Update;
using MySecondBrain.UI;

namespace MySecondBrain.Tests.Unit;

public class AutoUpdaterTests
{
    private readonly Mock<ILogger<AutoUpdaterDotNet>> _loggerMock = new();

    // ================================================================
    // Test 1: CurrentVersion reads from assembly (not null, not 0.0.0.0)
    // ================================================================

    [Fact]
    public void AutoUpdater_CurrentVersion_ReadsFromAssembly()
    {
        var updater = CreateUpdater();

        var version = updater.CurrentVersion;

        Assert.NotNull(version);
        // The test assembly has a default version (usually 1.0.0.0)
        Assert.NotEqual(new Version(0, 0, 0), version);
    }

    // ================================================================
    // Test 2: UpdateFeedUrl is not empty
    // ================================================================

    [Fact]
    public void AutoUpdaterDotNet_UpdateFeedUrl_IsNotEmpty()
    {
        var updater = CreateUpdater();

        var url = updater.UpdateFeedUrl;

        Assert.NotNull(url);
        Assert.NotEmpty(url);
        Assert.StartsWith("https://", url);
    }

    // ================================================================
    // Test 3: CheckForUpdates with no feed returns error
    // ================================================================

    [Fact]
    public async Task AutoUpdaterDotNet_CheckForUpdates_NoFeed_ReturnsNoUpdate()
    {
        // Arrange — HttpClient that throws (simulates unreachable feed)
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No route to host"));

        using var httpClient = new HttpClient(handlerMock.Object);
        var updater = CreateUpdater(httpClient);

        // Act
        var result = await updater.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        Assert.False(result.UpdateAvailable);
        Assert.Null(result.Update);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("HTTP error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // Test 4: Version comparison detects newer version from feed
    // ================================================================

    [Fact]
    public async Task AutoUpdater_VersionComparison_NewerVersionDetected()
    {
        // Arrange — mock HttpClient to return a feed with version 99.0.0.0
        var feedXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<item>
    <version>99.0.0.0</version>
    <url>https://example.com/update-99.msix</url>
    <changelog>Major update with new features</changelog>
    <mandatory>true</mandatory>
</item>";

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(feedXml)
            });

        using var httpClient = new HttpClient(handlerMock.Object);
        var updater = CreateUpdater(httpClient, feedUrl: "https://example.com/feed.xml");

        // Act
        var result = await updater.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        Assert.True(result.UpdateAvailable, "Update should be available (99.0.0.0 > local version)");
        Assert.NotNull(result.Update);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(new Version(99, 0, 0, 0), result.Update.Version);
        Assert.Equal("Major update with new features", result.Update.ReleaseNotes);
        Assert.Equal("https://example.com/update-99.msix", result.Update.DownloadUrl);
        Assert.True(result.Update.IsMandatory, "Update should be marked as mandatory");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private AutoUpdaterDotNet CreateUpdater(HttpClient? httpClient = null, string? feedUrl = null)
    {
        httpClient ??= new HttpClient();
        return new AutoUpdaterDotNet(_loggerMock.Object, httpClient, feedUrl);
    }
}
