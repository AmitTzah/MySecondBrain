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
