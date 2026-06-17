# API Routes Knowledge — MySecondBrain

> **Global API endpoints, route definitions, middleware patterns, and protocol-level details.**  
> Source: Feature 1/245 — .NET 8.0 WPF Solution Scaffold.

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

The Kestrel server is started in `App.xaml.cs` `OnStartup` or via an `IHostedService`:

```
ServiceCollection → Add Kestrel → Listen on 127.0.0.1:{dynamic-port}
```

### 2.2 WebSocket Endpoint Convention

A single WebSocket endpoint accepts connections from the Word Add-in:

| Path | Direction | Purpose |
|------|-----------|---------|
| `/ws/word-addin` | Bidirectional | Word Add-in exchanges text/commands with desktop app |

**Message protocol:** JSON frames over WebSocket. Exact message schema defined when the Word Add-in integration feature is implemented.

---

## 3. Integration Point Catalog

All inter-component communication is **in-process** (DI-resolved service calls), not over a network. The 23 integration points cataloged in [`agent-workspace/project-director/planning/integration-points.md`](../project-director/planning/integration-points.md) define service interface contracts, not network API contracts.

---

## 4. Future Considerations

If a future feature adds HTTP API endpoints (e.g., a local REST API for scripting or third-party plugin support), they should:
- Bind to `127.0.0.1` only (loopback)
- Use the same embedded Kestrel instance
- Follow the Provider/Adapter pattern for API versioning
- Be documented as extensions to this file
