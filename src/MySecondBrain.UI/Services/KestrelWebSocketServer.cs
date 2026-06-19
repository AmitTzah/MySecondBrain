using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using Serilog;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;

namespace MySecondBrain.UI.Services;

public class KestrelWebSocketServer : ILocalWebSocketServer
{
    private readonly ILogger<KestrelWebSocketServer> _logger;
    private readonly ISettingsRepository _settings;
    private WebApplication? _app;
    private int _port;
    private string _authToken;

    private const string AuthTokenKey = "WebSocketAuthToken";

    public KestrelWebSocketServer(ILogger<KestrelWebSocketServer> logger, ISettingsRepository settings)
    {
        _logger = logger;
        _settings = settings;
        _authToken = LoadOrGenerateToken();
    }

    public event EventHandler<string>? MessageReceived;

    public int Port => _port;

    public bool IsRunning => _app is not null;

    public string AuthToken => _authToken;

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

        // Enable WebSocket middleware so that context.WebSockets.IsWebSocketRequest
        // works correctly for upgrade requests.
        app.UseWebSockets();

        // Health check endpoint
        app.MapGet("/health", () => "OK");

        // WebSocket endpoint with token-based authentication
        app.Map("/ws", async (HttpContext context) =>
        {
            var token = context.Request.Query["token"].FirstOrDefault()
                ?? ExtractBearerToken(context.Request.Headers["Authorization"].FirstOrDefault());

            if (string.IsNullOrEmpty(token) || token != _authToken)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            if (context.WebSockets.IsWebSocketRequest)
            {
                using var ws = await context.WebSockets.AcceptWebSocketAsync();
                await ReceiveLoop(ws);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });

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

    /// <summary>
    /// Regenerates the authentication token: creates a cryptographically random
    /// 32-byte hex string (64 characters) and persists it via ISettingsRepository.
    /// </summary>
    public string RegenerateAuthToken()
    {
        var token = GenerateRandomHexToken();
        _settings.SetAsync(AuthTokenKey, token).GetAwaiter().GetResult();
        _authToken = token;
        _logger.LogInformation("WebSocket auth token regenerated");
        return token;
    }

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

    /// <summary>
    /// Loads the existing auth token from settings. If none exists, generates
    /// a new cryptographically random 32-byte hex string and persists it.
    /// </summary>
    private string LoadOrGenerateToken()
    {
        var existing = _settings.GetAsync(AuthTokenKey).GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(existing))
        {
            _logger.LogDebug("Loaded existing WebSocket auth token from settings");
            return existing!;
        }

        var token = GenerateRandomHexToken();
        _settings.SetAsync(AuthTokenKey, token).GetAwaiter().GetResult();
        _logger.LogInformation("Generated new WebSocket auth token");
        return token;
    }

    /// <summary>
    /// Creates a cryptographically random 32-byte value and returns it as
    /// a 64-character uppercase hexadecimal string.
    /// </summary>
    private static string GenerateRandomHexToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Extracts the Bearer token from an Authorization header value,
    /// handling optional extra whitespace and case-insensitive scheme matching.
    /// Returns null if the header is missing or not a Bearer token.
    /// </summary>
    private static string? ExtractBearerToken(string? authHeader)
    {
        if (string.IsNullOrEmpty(authHeader))
            return null;

        const string bearerPrefix = "Bearer ";
        var index = authHeader.IndexOf(bearerPrefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        return authHeader[(index + bearerPrefix.Length)..].Trim();
    }

    /// <summary>
    /// Placeholder receive loop for WebSocket messages. This method is invoked
    /// for each accepted WebSocket connection. The full JSON message protocol
    /// will be implemented in a subsequent step.
    /// </summary>
    private async Task ReceiveLoop(WebSocket webSocket)
    {
        var buffer = new byte[4096];
        _logger.LogInformation("WebSocket client connected");
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
                var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("WebSocket received: {Message}", message);
                MessageReceived?.Invoke(this, message);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error");
        }
        finally
        {
            _logger.LogInformation("WebSocket client disconnected");
        }
    }
}
