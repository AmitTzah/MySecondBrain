# Kestrel WebSocket Server — External Documentation

## Library ID
/dotnet/aspnetcore (FrameworkReference: Microsoft.AspNetCore.App)

## Overview
ASP.NET Core Kestrel is a cross-platform web server for ASP.NET Core. It can be embedded in-process within a WPF desktop application to host a local WebSocket server on `127.0.0.1` for external integrations (e.g., Word Add-in).

## Key API Reference

### Framework Reference (in .csproj)
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

### Building a Minimal Kestrel WebSocket Server
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseKestrel(options =>
{
    options.Listen(IPAddress.Loopback, port); // 127.0.0.1 only
});

var app = builder.Build();

// Enable WebSocket middleware
app.UseWebSockets(new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

// Map WebSocket endpoint
app.Map("/ws", async (HttpContext context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await EchoLoop(webSocket);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

await app.RunAsync();
```

### WebSocket Receive Loop
```csharp
private static async Task EchoLoop(WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];
    var receiveResult = await webSocket.ReceiveAsync(
        new ArraySegment<byte>(buffer), CancellationToken.None);

    while (!receiveResult.CloseStatus.HasValue)
    {
        // Echo the message back
        await webSocket.SendAsync(
            new ArraySegment<byte>(buffer, 0, receiveResult.Count),
            receiveResult.MessageType,
            receiveResult.EndOfMessage,
            CancellationToken.None);

        receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
    }

    await webSocket.CloseAsync(
        receiveResult.CloseStatus.Value,
        receiveResult.CloseStatusDescription,
        CancellationToken.None);
}
```

### KestrelServerOptions — Listen on Specific Address/Port
```csharp
builder.WebHost.UseKestrel(options =>
{
    options.Listen(IPAddress.Loopback, 0); // 0 = auto-select port
});
```

### WebSocketOptions
```csharp
app.UseWebSockets(new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});
```

## MySecondBrain Usage

### Implementation Pattern: KestrelWebSocketServer (ILocalWebSocketServer)
The `KestrelWebSocketServer` class (in `UI/Services/KestrelWebSocketServer.cs`) wraps Kestrel behind the `ILocalWebSocketServer` interface:

- **StartAsync**: Builds and starts a `WebApplication` with Kestrel on `127.0.0.1:{port}`. Port 0 = OS auto-assigns.
- **StopAsync**: Gracefully stops the Kestrel server.
- **Token Auth**: On WebSocket upgrade request, validates `?token=` query parameter or `Authorization` header against stored token.
- **JSON Protocol**: Messages are UTF-8 JSON strings. Received messages fire `MessageReceived` event. `SendAsync` sends JSON to connected client.
- **Lifecycle**: Started in `App.xaml.cs` `OnStartup`, stopped in `OnExit`.

### Port Management
- Auto-port selection via `options.Listen(IPAddress.Loopback, 0)`
- Actual port exposed via `Port` property
- Port logged at startup for diagnostics
- Configurable via Settings (Feature 8 will add UI)

### Security
- Bound to `127.0.0.1` only — no network exposure
- Token-based auth: auto-generated 32-byte hex string, stored via `ISettingsRepository`
- Token regeneratable via `RegenerateAuthToken()`
- Only one active client connection at a time

### Graceful Shutdown
- Server stops in `App.OnExit()` before DI container dispose
- Active WebSocket connections are closed with normal closure status
- Token survives app restart (persisted in SQLite)

## Source
Microsoft Docs: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets
GitHub: https://github.com/dotnet/aspnetcore
