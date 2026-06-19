using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using Serilog;
using System.Net;

namespace MySecondBrain.UI.Services;

public class KestrelWebSocketServer : ILocalWebSocketServer
{
    private readonly ILogger<KestrelWebSocketServer> _logger;
    private WebApplication? _app;
    private int _port;

    public KestrelWebSocketServer(ILogger<KestrelWebSocketServer> logger)
    {
        _logger = logger;
    }

#pragma warning disable CS0067, CS0414
    public event EventHandler<string>? MessageReceived;
#pragma warning restore CS0067, CS0414

    public int Port => _port;

    public bool IsRunning => _app is not null;

    public string AuthToken => string.Empty;

    public async Task StartAsync(int? preferredPort = null, CancellationToken ct = default)
    {
        if (_app is not null)
        {
            _logger.LogWarning("WebSocket server is already running on port {Port}", _port);
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Production"
        });

        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, preferredPort ?? 0);
        });

        // Clear WebApplication's default logging providers, then add Serilog
        // so Kestrel startup errors (port binding failures, etc.) are visible
        // in the application log file.
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        var app = builder.Build();

        // Health check endpoint
        app.MapGet("/health", () => "OK");

        await app.StartAsync(ct);

        // Extract the actual port from the bound URL (needed for auto-port=0).
        // Kestrel's IServerAddressesFeature yields URLs like http://127.0.0.1:54321.
        // Using Uri.TryCreate for robustness against trailing slashes or unexpected formats.
        _port = ExtractPort(app.Urls.FirstOrDefault());

        _app = app;
        _logger.LogInformation("WebSocket server started on port {Port}", _port);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is null)
        {
            _logger.LogWarning("WebSocket server is not running");
            return;
        }

        await _app.StopAsync(ct);
        _app = null;
        _logger.LogInformation("WebSocket server stopped");
    }

    public Task SendAsync(string message, CancellationToken ct = default) =>
        Task.CompletedTask;

    public string RegenerateAuthToken() => string.Empty;

    /// <summary>
    /// Synchronous dispose required by IDisposable. The WebApplication is disposed
    /// synchronously because this singleton is only disposed once at process exit
    /// (in App.OnExit) where blocking is acceptable. Using GetAwaiter().GetResult()
    /// is safe here because Dispose() is not called from a synchronization context.
    /// </summary>
    public void Dispose()
    {
        if (_app is not null)
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _app = null;
        }
        MessageReceived = null;
    }

    /// <summary>
    /// Extracts the port number from a Kestrel URL like http://127.0.0.1:54321.
    /// Returns 0 if parsing fails.
    /// </summary>
    private static int ExtractPort(string? url)
    {
        if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Port;

        return 0;
    }
}
