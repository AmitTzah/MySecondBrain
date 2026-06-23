using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Services.Update;
using MySecondBrain.UI;
using MySecondBrain.UI.Services;
using Xunit.Abstractions;

namespace MySecondBrain.Tests.E2E;

/// <summary>
/// E2E tests for Feature 6: Windows Platform Services.
/// Verifies DI service resolution types, DPI awareness configuration,
/// WebSocket health endpoint, update checker versions, and singleton registration.
/// </summary>
[Collection("E2E")]
public sealed class PlatformServicesE2ETests : E2eTestBase
{
    private static readonly Lazy<IServiceProvider> _serviceProvider = new(() =>
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        return services.BuildServiceProvider();
    });

    public PlatformServicesE2ETests(E2eFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    // ============================================================
    // DI Service Resolution — Type Verification
    // ============================================================

    [Fact]
    public async Task SystemTrayService_ShouldResolveAsWinFormsSystemTrayService()
    {
        await UseSharedAppAsync();
        var service = ResolveService<ISystemTrayService>();
        Assert.NotNull(service);
        Assert.IsType<WinFormsSystemTrayService>(service);
        _output.WriteLine($"ISystemTrayService resolves as {service!.GetType().Name}");
    }

    [Fact]
    public async Task GlobalHotkeyService_ShouldResolveWithExpectedImplementation()
    {
        await UseSharedAppAsync();
        var service = ResolveService<IGlobalHotkeyService>();
        Assert.NotNull(service);
        Assert.IsType<GlobalHotkeyService>(service);
        _output.WriteLine($"IGlobalHotkeyService resolves as {service!.GetType().Name}");
    }

    [Fact]
    public async Task LocalWebSocketServer_ShouldResolveAsKestrelWebSocketServer()
    {
        await UseSharedAppAsync();
        var service = ResolveService<ILocalWebSocketServer>();
        Assert.NotNull(service);
        Assert.IsType<KestrelWebSocketServer>(service);
        _output.WriteLine($"ILocalWebSocketServer resolves as {service!.GetType().Name}");
    }

    [Fact]
    public async Task LocalWebSocketServer_ShouldHaveValidAuthToken()
    {
        await UseSharedAppAsync();
        var wsServer = ResolveService<ILocalWebSocketServer>();
        Assert.NotNull(wsServer);

        var token = wsServer!.AuthToken;
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(token.Length >= 16,
            $"Auth token length ({token.Length}) should be at least 16 characters");
        _output.WriteLine($"WebSocket auth token is valid (length={token.Length}).");
    }

    [Fact]
    public async Task UpdateChecker_ShouldResolveBothImplementations()
    {
        await UseSharedAppAsync();
        var checkers = ResolveServices<IUpdateChecker>();
        Assert.NotNull(checkers);

        var checkerList = checkers!.ToList();
        Assert.Equal(2, checkerList.Count);

        var typeNames = checkerList.Select(c => c.GetType().Name).OrderBy(n => n).ToArray();
        _output.WriteLine($"IUpdateChecker implementations: [{string.Join(", ", typeNames)}]");

        Assert.Contains(typeNames, n => n.Contains("AutoUpdaterDotNet"));
        Assert.Contains(typeNames, n => n.Contains("MsixAppInstallerUpdater"));
    }

    // ============================================================
    // Window Bounds (DPI awareness proxy check)
    // ============================================================

    [Fact]
    public async Task MainWindow_ShouldHaveReasonableBounds()
    {
        await UseSharedAppAsync();
        // Verify the main window has reasonable bounds consistent with DPI-aware rendering
        var bounds = _fixture.MainWindow.BoundingRectangle;
        _output.WriteLine($"Main window bounds: Width={bounds.Width}, Height={bounds.Height}");

        Assert.True(bounds.Width > 200,
            $"Window width ({bounds.Width}) should be > 200px");
        Assert.True(bounds.Height > 200,
            $"Window height ({bounds.Height}) should be > 200px");

        Assert.Equal("MySecondBrain", _fixture.MainWindow.Title);
        _output.WriteLine("Main window has reasonable bounds and title.");
    }

    // ============================================================
    // WebSocket Health Endpoint
    // ============================================================

    [Fact]
    public async Task WebSocketHealthEndpoint_ShouldReturn200()
    {
        await UseSharedAppAsync();

        // Create a separate KestrelWebSocketServer instance for testing.
        // Binds to port 0 (random) so there is no collision with the running app's server.
        var testServer = CreateTestWebSocketServer();
        try
        {
            // StartAsync with no preferredPort defaults to port 0 (random OS assignment)
            await testServer.StartAsync();

            var port = testServer.Port;
            _output.WriteLine($"Test WebSocket server started on port {port}");

            // Poll the health endpoint instead of using a fixed delay
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            HttpResponseMessage? response = null;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                try
                {
                    response = await httpClient.GetAsync($"http://127.0.0.1:{port}/health");
                    if (response.IsSuccessStatusCode)
                        break;
                }
                catch
                {
                    // Server may not be ready yet — retry
                }
                await Task.Delay(200);
            }

            Assert.NotNull(response);
            var body = await response!.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", body);
            _output.WriteLine($"WebSocket health endpoint returned 200 OK after ~{sw.ElapsedMilliseconds}ms.");
        }
        finally
        {
            await testServer.StopAsync();
            (testServer as IDisposable)?.Dispose();
        }
    }

    // ============================================================
    // Auto-Update Version
    // ============================================================

    [Fact]
    public async Task AutoUpdate_CurrentVersion_ShouldBeNonZero()
    {
        await UseSharedAppAsync();
        var checkers = ResolveServices<IUpdateChecker>();
        Assert.NotNull(checkers);

        foreach (var checker in checkers!)
        {
            var version = checker.CurrentVersion;
            _output.WriteLine($"IUpdateChecker '{checker.GetType().Name}' CurrentVersion: {version}");

            // AutoUpdaterDotNet reads from assembly version; MsixAppInstallerUpdater returns 0.0.0
            if (checker is AutoUpdaterDotNet autoUpdater)
            {
                Assert.True(version.Major > 0 || version.Minor > 0 || version.Build > 0,
                    $"AutoUpdaterDotNet.CurrentVersion ({version}) should be non-zero");
            }
        }
    }

    // ============================================================
    // Singleton Registration Verification
    // ============================================================

    [Fact]
    public async Task PlatformServices_ShouldBeRegisteredAsSingletons()
    {
        await UseSharedAppAsync();
        var provider = _serviceProvider.Value;

        // Resolve each service twice and verify same instance (singleton behavior)
        VerifySingleton<ISystemTrayService>(provider);
        VerifySingleton<IGlobalHotkeyService>(provider);
        VerifySingleton<ILocalWebSocketServer>(provider);

        // IUpdateChecker is multi-implementation — verify each is singleton
        var checkers1 = provider.GetServices<IUpdateChecker>().ToList();
        var checkers2 = provider.GetServices<IUpdateChecker>().ToList();
        Assert.Equal(checkers1.Count, checkers2.Count);
        for (int i = 0; i < checkers1.Count; i++)
        {
            Assert.Same(checkers1[i], checkers2[i]);
            _output.WriteLine($"IUpdateChecker[{i}] ({checkers1[i].GetType().Name}) is singleton.");
        }

        _output.WriteLine("All 4 platform services are registered as singletons.");
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static T? ResolveService<T>() where T : class =>
        _serviceProvider.Value.GetService<T>();

    private static IEnumerable<T>? ResolveServices<T>() where T : class =>
        _serviceProvider.Value.GetService<IEnumerable<T>>();

    private static void VerifySingleton<T>(IServiceProvider provider) where T : class
    {
        var instance1 = provider.GetService<T>();
        var instance2 = provider.GetService<T>();
        Assert.NotNull(instance1);
        Assert.Same(instance1, instance2);
    }

    private static ILocalWebSocketServer CreateTestWebSocketServer()
    {
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ILocalWebSocketServer>();
    }
}
