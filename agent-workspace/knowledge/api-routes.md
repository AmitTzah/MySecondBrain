# API Routes Knowledge — MySecondBrain

> **Global API endpoints, route definitions, middleware patterns, and protocol-level details.**  
> Source: Features W1.1–W1.3 — Solution Scaffold, DI Container, Logging.

---

## 1. Architectural Decision: No HTTP API Routes

MySecondBrain is a **local-first desktop application**. There are no HTTP REST API routes, no controllers, no authentication endpoints, and no cloud backend.

All data access is direct: SQLite via EF Core, file system for `.md` wiki files, and in-process service calls via DI.

---

## 2. Embedded WebSocket Server (Kestrel)

For **external integration** (specifically the Word Add-in), the WPF application hosts an embedded Kestrel WebSocket server:

| Property | Value |
|----------|-------|
| **Protocol** | WebSocket (WS) |
| **Bind address** | `127.0.0.1` (loopback only — no network exposure) |
| **Port** | Dynamically assigned at startup |
| **Purpose** | Word Add-in ↔ Desktop App communication |
| **Security** | Loopback-only binding; no external network access |

### 2.1 Startup Pattern

The Kestrel server is started in `App.xaml.cs` `OnStartup` via a fire-and-forget pattern after `MainWindow.Show()`:

```csharp
// In App.xaml.cs OnStartup, after mainWindow.Show():
_ = StartWebSocketServerAsync();  // Fire-and-forget — server starts in background

private async Task StartWebSocketServerAsync()
{
    var server = _serviceProvider.GetRequiredService<ILocalWebSocketServer>();
    await server.StartAsync(preferredPort: null, CancellationToken.None);
}
```

Shutdown in `OnExit`:

```csharp
protected override void OnExit(ExitEventArgs e)
{
    var server = _serviceProvider?.GetService<ILocalWebSocketServer>();
    if (server is not null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await server.StopAsync(cts.Token);
    }
    Log.CloseAndFlush();
    (_serviceProvider as IDisposable)?.Dispose();
    base.OnExit(e);
}
```

### 2.2 Endpoint Catalog

Two HTTP endpoints are mapped on the embedded Kestrel server:

| Path | Method | Protocol | Purpose | Auth Required |
|------|--------|----------|---------|---------------|
| `/health` | GET | HTTP/1.1 | Health check — returns `200 OK` with body `"OK"` | No |
| `/ws` | GET (Upgrade) | WebSocket (RFC 6455) | Bidirectional JSON message channel for external integrations (Word Add-in protocol in Feature 13) | Yes (token) |

### 2.3 Kestrel Pipeline — ASP.NET Core Minimal API

The Kestrel server uses the ASP.NET Core minimal hosting API with inline endpoint mapping:

```csharp
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseKestrel(options =>
{
    options.Listen(IPAddress.Loopback, preferredPort ?? 0);  // 0 = OS auto-assign
});
builder.Logging.AddSerilog();  // Bridge Kestrel logs into Serilog pipeline

var app = builder.Build();

// Health check — no auth, always available
app.MapGet("/health", () => "OK");

// WebSocket endpoint — token auth, single client constraint
app.Map("/ws", async (HttpContext context) =>
{
    // --- Token extraction (two methods, query takes priority) ---
    var token = context.Request.Query["token"].FirstOrDefault()
        ?? context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

    // --- Missing/invalid token → HTTP 401 ---
    if (string.IsNullOrEmpty(token) || token != _authToken)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    // --- Not a WebSocket request → HTTP 400 ---
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connections only");
        return;
    }

    // --- Single-client constraint → HTTP 409 ---
    if (_currentClient is not null && _currentClient.State == WebSocketState.Open)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsync("Another client is already connected");
        return;
    }

    // --- Accept the WebSocket ---
    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    _currentClient = ws;
    await ReceiveLoop(ws, context.RequestAborted);
    _currentClient = null;
});
```

### 2.4 Authentication — Token Details

Token-based auth on the `/ws` endpoint accepts tokens via two mechanisms (checked in order):

| Method | Format | Example |
|--------|--------|---------|
| **Query string** | `?token={token}` | `ws://127.0.0.1:51234/ws?token=1A2B3C4D5E6F...` |
| **Authorization header** | `Authorization: Bearer {token}` | `Authorization: Bearer 1A2B3C4D5E6F...` |

The query string method takes priority — if both are present, the query parameter value is used.

**Token properties:**
- **Format:** 64-character uppercase hexadecimal string
- **Generation:** `RandomNumberGenerator.GetBytes(32)` → `Convert.ToHexString(bytes)` — 32 bytes (256 bits) of cryptographic entropy
- **Character set:** `[0-9A-F]` only (no lowercase, no hyphens, no special characters)
- **Storage:** `ISettingsRepository` key `"WebSocketAuthToken"` — auto-generated on first run if not present
- **Lifetime:** Generated once, persisted permanently. Regeneration is user-initiated via Settings UI (Feature 8)

### 2.5 HTTP Status Codes — Complete Reference

| Status | Condition | Body |
|--------|-----------|------|
| **200** | `GET /health` — server is healthy | `"OK"` |
| **101** | `GET /ws` with valid token + WebSocket upgrade headers | *(protocol switch to WebSocket)* |
| **400** | `GET /ws` with valid token but no WebSocket upgrade headers | `"WebSocket connections only"` |
| **401** | `GET /ws` with missing or invalid token | `"Unauthorized"` |
| **409** | `GET /ws` with valid token but another client is already connected | `"Another client is already connected"` |

### 2.6 Single-Client Constraint

The Kestrel server enforces a strict single-client policy:

- Only one active WebSocket connection is allowed at any time
- If a client attempts to connect while another is active, the new connection receives **HTTP 409 Conflict**
- When the active client disconnects (graceful close, network drop, or exception), `_currentClient` is set to `null`, allowing a new connection
- This constraint is checked at the Kestrel middleware level — before the WebSocket handshake is accepted — to avoid unnecessary resource allocation
- The active client reference (`_currentClient`) is stored as a field on the `KestrelWebSocketServer` singleton instance

**Rationale:** MySecondBrain is a single-user desktop application. The Word Add-in is the primary WebSocket client. Multiple simultaneous connections would create ambiguity about which client "owns" the chat context and could lead to conflicting message routing.

### 2.7 Message Protocol — JSON over WebSocket

Once a WebSocket connection is established:

```csharp
private async Task ReceiveLoop(WebSocket webSocket, CancellationToken ct)
{
    var buffer = new byte[4096];
    _logger.LogInformation("WebSocket client connected");

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Closing", ct);
                break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
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
```

**Protocol properties:**
- **Frame type:** UTF-8 text frames (`WebSocketMessageType.Text`)
- **Message format:** JSON objects (free-form; schema defined per integration protocol in Feature 13)
- **Buffer size:** 4 KB per frame (multi-frame messages are assembled by the WebSocket implementation)
- **Send:** `SendAsync(string message)` wraps the string in a UTF-8 `ArraySegment<byte>` and calls `webSocket.SendAsync()`
- **Event:** `MessageReceived` event fires with the raw JSON string. Subscribers (e.g., a protocol handler in Feature 13) parse and route messages.

### 2.8 Port Discovery

The actual port is determined at startup and logged:

```csharp
_logger.LogInformation("WebSocket server started on port {Port}", _port);
```

**Discovery methods:**
- **Log file:** `%LOCALAPPDATA%\MySecondBrain\logs\msb-{date}.log` — search for "WebSocket server started on port"
- **Programmatic:** `ILocalWebSocketServer.Port` property (available after `StartAsync` completes)
- **CLI:** `netstat -ano | findstr :{port}` — verify `127.0.0.1:{port}` is in LISTENING state

---

## 3. Integration Point Catalog

All inter-component communication is **in-process** (DI-resolved service calls), not over a network. The 23 integration points cataloged in [`agent-workspace/project-director/planning/integration-points.md`](../project-director/planning/integration-points.md) define service interface contracts, not network API contracts.

---

## 4. Interface-as-Contract: The In-Process API Surface

While MySecondBrain has no HTTP API routes, the **41 interfaces in [`MySecondBrain.Core/Interfaces/`](../../src/MySecondBrain.Core/Interfaces/)** serve as the application's API contract — the boundary through which all layers communicate. Future features consume these interfaces as their "endpoints."

### 4.1 Interface Categories (Logical API Groupings)

| Category | Count | Interfaces |
|----------|-------|-----------|
| **Provider/Adapter** | 9 | `ILLMProvider`, `ILLMProviderFactory`, `ISTTProvider`, `IBackupProvider`, `ISearchProvider`, `ITokenizer`, `ITokenizerFactory`, `IChatImporter`, `IUpdateChecker` |
| **Repository** | 8 | `IChatThreadRepository`, `IMessageRepository`, `IPersonaRepository`, `IModelConfigurationRepository`, `IApiKeyRepository`, `IWikiIndexRepository`, `IUsageRepository`, `ISettingsRepository` |
| **Application Service** | 17 | `ILLMProviderService`, `IChatThreadService`, `IWikiService`, `IEncryptionService`, `IChatEncryptionService`, `IClipboardService`, `IWikiFileWatcher`, `ILocalWebSocketServer`, `ISystemTrayService`, `IGlobalHotkeyService`, `IHwndCaptureService`, `ITextInjectionService`, `IAudioService`, `ICameraService`, `IVideoPlayerService`, `ISpellCheckService`, `IWikiGitService` |
| **Tool System** | 2 | `IToolExecutor`, `IToolOrchestrator` |
| **Content Rendering** | 2 | `IContentBlockRenderer`, `IContentRendererRegistry` |
| **Cross-Cutting** | 3 | `IChatSearchService`, `IAutoCleanupService`, `IThemeProvider` |

### 4.2 Multi-Implementation Interfaces (Provider Groups)

These interfaces have multiple concrete implementations registered via repeated `AddSingleton` calls. Consumers resolve all implementations via `IEnumerable<T>` constructor injection (see [Architecture §3.2](architecture.md#32-multi-implementation-di-pattern-ienumerablet-injection)):

| Interface | Impl Count | Implementations |
|-----------|------------|----------------|
| `ILLMProvider` | 4 | `OpenAIProvider`, `AnthropicProvider`, `GoogleProvider`, `OpenAICompatibleProvider` |
| `ISTTProvider` | 3 | `OpenAIWhisperProvider`, `LocalWhisperProvider`, `WindowsSpeechProvider` |
| `IBackupProvider` | 2 | `GcsBackupProvider`, `LocalFolderBackupProvider` |
| `ISearchProvider` | 2 | `GoogleCustomSearchProvider`, `BingSearchProvider` |
| `ITokenizer` | 3 | `SharpTokenTokenizer`, `AnthropicTokenizer`, `FallbackTokenizer` |
| `IChatImporter` | 2 | `ChatGPTImporter`, `ClaudeImporter` |
| `IToolExecutor` | 5 | `WebSearchToolExecutor`, `TerminalToolExecutor`, `FileGenerateToolExecutor`, `FileEditToolExecutor`, `WikiSearchToolExecutor` |
| `IUpdateChecker` | 2 | `AutoUpdaterDotNet`, `MsixAppInstallerUpdater` |
| `IContentBlockRenderer` | 7 | `MarkdownTextRenderer`, `CodeBlockRenderer`, `ArtifactReferenceRenderer`, `ImageRenderer`, `MediaRenderer`, `ThinkingRenderer`, `ToolCallRenderer` |

### 4.3 Factory/Registry Resolution Interfaces

Some interfaces serve as factory or registry entry points rather than direct service contracts:

| Interface | Pattern | Purpose |
|-----------|---------|---------|
| `ILLMProviderFactory` | Factory | Selects the correct `ILLMProvider` by `ProviderType` from `IEnumerable<ILLMProvider>` |
| `ITokenizerFactory` | Factory | Selects the correct `ITokenizer` by model name from `IEnumerable<ITokenizer>` |
| `IContentRendererRegistry` | Registry | Aggregates `IEnumerable<IContentBlockRenderer>`, sorts by priority, resolves by `CanRender()` |
| `IToolOrchestrator` | Orchestrator | Routes tool calls to the correct `IToolExecutor` from `IEnumerable<IToolExecutor>` |

---

## 5. Future Considerations

If a future feature adds HTTP API endpoints (e.g., a local REST API for scripting or third-party plugin support), they should:
- Bind to `127.0.0.1` only (loopback)
- Use the same embedded Kestrel instance
- Follow the Provider/Adapter pattern for API versioning
- Be documented as extensions to this file
