# Planning Summary — MySecondBrain

## Purpose

This file is the index of the `planning/` directory. It is what the Feature Developer Architect reads first to understand the complete technical architecture of MySecondBrain. Every other file in this directory is summarized here. Use the "Quick Reference" tables to find specific information fast.

---

## Directory Map — All 7 Planning Files

| # | File | Batch | Summary |
|---|------|-------|---------|
| 1 | [`tech-stack.md`](tech-stack.md) | 1 | Complete technology stack: 15 OSS libraries, 12 custom builds, 10 platform features, 2 SaaS services. Every runtime dependency with version, sizing, and sourcing rationale. |
| 2 | [`architecture.md`](architecture.md) | 1 | System design: component diagram (6 groups), data flow (message lifecycle, Tier 1 hotkey flow), architectural patterns (MVVM, Provider/Adapter, Repository, Plugin/Registry), deployment model, cross-cutting concerns. |
| 3 | [`abstractions.md`](abstractions.md) | 1 | All C# interfaces and contracts: 12 service abstractions (ILLMProvider, ISTTProvider, IBackupProvider, ISearchProvider, ITokenizer, IChatImporter, IToolExecutor, IContentBlockRenderer, IThemeProvider, IUpdateChecker), 8 repository interfaces, 2 service interfaces, 13 platform service interfaces, and a dependency map. |
| 4 | [`data-model.md`](data-model.md) | 2 | 13 data entities with attributes, relationships, feature-group mapping, ASCII ER diagram, and special modeling notes: message branching (version-chain), transient vs. permanent threads, soft-delete, wiki index tables, usage aggregation, cascading delete rules. |
| 5 | [`integration-points.md`](integration-points.md) | 2 | 24 integration points: 8 SaaS/cloud (OpenAI, Anthropic, Google Gemini, OpenAI-Compatible, GCS, Web Search, GitHub, Auto-Update) + 16 platform (DPAPI, AES-GCM, Clipboard, FileSystemWatcher, Kestrel, System Tray, Global Hooks, HWND/UIA, SharpToken, STT, Audio, Webcam, Video, SpellCheck, Git, Serilog Destructuring). Each with abstraction, fallback, config. |
| 6 | [`platform-notes.md`](platform-notes.md) | 2 | WPF-specific implementation guidance: MVVM with CommunityToolkit.Mvvm, XAML DataTemplate patterns (chat messages, wiki browser, code blocks, BiDi), DI lifetimes, three-tier window management (WS_EX_NOACTIVATE, overlay positioning, focus), system tray, global hotkeys (RegisterHotKey), PerMonitorV2 DPI, MSIX packaging, auto-update, and 10 known WPF pitfalls with workarounds. |
| 7 | **`planning-summary.md`** | 2 | **This file.** Index and quick reference for the entire planning directory. Architecture decision log. |
| — | [`tech-sourcing.md`](../tech-sourcing.md) | 0 | Upstream: 36 technology sourcing decisions with alternatives analysis and risk levels. The foundation upon which Batch 1 and 2 planning files are built. |

---

## Quick Reference

### Technology Stack Summary

| Layer | Choice | Category |
|-------|--------|----------|
| Runtime | .NET 8.0+ (LTS) | Platform |
| UI Framework | WPF | Platform |
| MVVM | CommunityToolkit.Mvvm | OSS |
| DI | Microsoft.Extensions.DependencyInjection | Platform |
| Database | SQLite + EF Core + FTS5 | OSS |
| Markdown | Markdig → WPF FlowDocument | OSS + Custom |
| LLM — OpenAI | OpenAI NuGet SDK | OSS |
| LLM — Anthropic | Anthropic.SDK (community) | OSS |
| LLM — Google | Google.Cloud.AIPlatform.V1 | OSS |
| LLM — Compatible | HttpClient + System.Text.Json | Custom |
| Tokenizer | SharpToken | OSS |
| Audio | NAudio | OSS |
| Diff | DiffPlex | OSS |
| Charts | LiveCharts2 | OSS |
| Spell Check | WeCantSpell.Hunspell | OSS |
| PDF Export | QuestPDF | OSS |
| Auto-Update | AutoUpdater.NET | OSS |
| Git | LibGit2Sharp | OSS |
| Webcam | AForge.NET | OSS |
| Local STT | Whisper.net | OSS |
| Encryption | DPAPI + AES-256-GCM (.NET) | Platform |
| Global Hooks | P/Invoke (RegisterHotKey) | Custom |
| Text Injection | UIA + P/Invoke | Custom |
| Search | SQLite FTS5 | Platform |
| Charts | LiveCharts2 | OSS |
| WebSocket | ASP.NET Core Kestrel | Platform |
| Backup | Google.Cloud.Storage.V1 | SaaS/OSS |
| Web Search | Google Custom Search / Bing API | SaaS |

### Key Abstractions List

| Interface | Implements | Purpose |
|-----------|-----------|---------|
| `ILLMProvider` | OpenAIProvider, AnthropicProvider, GoogleProvider, OpenAICompatibleProvider | Normalized AI chat/streaming |
| `ILLMProviderFactory` | LLMProviderFactory | Runtime provider resolution |
| `ISTTProvider` | OpenAIWhisperProvider, LocalWhisperProvider, WindowsSpeechProvider | Voice dictation |
| `IBackupProvider` | GcsBackupProvider, LocalFolderBackupProvider | Backup upload/download |
| `ISearchProvider` | GoogleCustomSearchProvider, BingSearchProvider | Web search for AI tool-use |
| `ITokenizer` / `ITokenizerFactory` | SharpTokenTokenizer, FallbackTokenizer | Real-time token counting |
| `IChatImporter` | ChatGPTImporter, ClaudeImporter | Chat history import |
| `IToolExecutor` | WebSearchToolExecutor, TerminalToolExecutor, FileGenerateToolExecutor, FileEditToolExecutor, WikiSearchToolExecutor | Tool execution for AI agents |
| `IToolOrchestrator` | ToolOrchestrator | Function-calling loop |
| `IContentBlockRenderer` | MarkdownTextRenderer, CodeBlockRenderer, ArtifactReferenceRenderer, ImageRenderer, MediaRenderer, ThinkingRenderer, ToolCallRenderer | Markdig AST → WPF elements |
| `IThemeProvider` | WpfThemeProvider | Dark/light + chat themes |
| `IUpdateChecker` | AutoUpdaterDotNet, MsixAppInstallerUpdater | Version check + update |
| `IChatThreadService` | ChatThreadService | Central chat/message/branching service |
| `ILLMProviderService` | LLMProviderService | High-level LLM API wrapper |
| `IWikiService` | WikiService | Wiki indexing, search, versioning, git |
| **Platform Services (§13):** | | |
| `IEncryptionService` | DpapiEncryptionService | DPAPI key encryption |
| `IChatEncryptionService` | AesGcmChatEncryptionService | AES-256-GCM locked chats |
| `IClipboardService` | WpfClipboardService | Clipboard read/write (multi-format) |
| `IWikiFileWatcher` | FileSystemWatcherAdapter | Wiki directory monitoring |
| `ILocalWebSocketServer` | KestrelWebSocketServer | Embedded local WebSocket |
| `ISystemTrayService` | WinFormsSystemTrayService | System tray icon |
| `IGlobalHotkeyService` | GlobalHotkeyService | System-wide hotkey registration |
| `IHwndCaptureService` | Win32HwndCaptureService | Active window capture |
| `ITextInjectionService` | UiaTextInjectionService | Text injection into target window |
| `IAudioService` | NaudioAudioService | Microphone recording + playback |
| `ICameraService` | AForgeCameraService | Webcam still capture |
| `IVideoPlayerService` | WpfVideoPlayerService | Video playback (MediaElement + VLC) |
| `ISpellCheckService` | HunspellSpellCheckService | Spell check with suggestions |
| `IWikiGitService` | LibGit2SharpGitService | Wiki git version control |
| `IChatSearchService` | Fts5ChatSearchService | Full-text chat search (FTS5) |
| `IAutoCleanupService` | PeriodicAutoCleanupService | Transient/trash auto-purge |

### Data Entities List (13)

| Entity | Primary FK | Key Relationships |
|--------|-----------|------------------|
| ApiKey | — | has many ModelConfiguration |
| Persona | → ModelConfiguration | has many ChatThread, Message |
| ModelConfiguration | → ApiKey | has many Persona, Message |
| ChatThread | → Persona | has many Message, Artifact, MediaItem, UsageRecord |
| Message | → ChatThread, → Persona?, → ModelConfiguration?, → self | has UsageRecord, MediaItem |
| Artifact | → ChatThread | can be saved to WikiFile |
| MediaItem | → ChatThread, → Message? | can be saved to WikiFile |
| PromptTemplate | — | independent (transient use) |
| TextAction | → ModelConfiguration (nullable) | independent (transient use); three-dimensional: captureScope (flags) + systemPrompt/modelConfigId + applyMode (enum) |
| UsageRecord | → Message, → ChatThread, → Persona?, → ModelConfiguration | append-only |
| WikiFile | — (index, not source of truth) | has many WikiVersionSnapshot |
| WikiVersionSnapshot | → WikiFile | retention: 30/file, 50MB cap |
| BackupSnapshot | — | standalone |

### External Integrations List (23)

| # | Integration | Type | Abstraction |
|---|------------|------|-------------|
| 1 | OpenAI API | SaaS | ILLMProvider, ISTTProvider |
| 2 | Anthropic API | SaaS | ILLMProvider |
| 3 | Google Gemini API | SaaS | ILLMProvider |
| 4 | OpenAI-Compatible Endpoints | SaaS/Local | ILLMProvider |
| 5 | Google Cloud Storage | SaaS | IBackupProvider |
| 6 | Google Custom Search / Bing | SaaS | ISearchProvider |
| 7 | GitHub (Wiki Git Push) | SaaS | IWikiGitService |
| 8 | Auto-Update Feed | SaaS | IUpdateChecker |
| 9 | Windows DPAPI | Platform | IEncryptionService |
| 10 | AES-256-GCM | Platform | IChatEncryptionService |
| 11 | Windows Clipboard | Platform | IClipboardService |
| 12 | FileSystemWatcher | Platform | IWikiFileWatcher |
| 13 | Kestrel WebSocket | Platform | ILocalWebSocketServer |
| 14 | System Tray (NotifyIcon) | Platform | ISystemTrayService |
| 15 | Global Keyboard Hooks | Platform | IGlobalHotkeyService |
| 16 | HWND Capture + UIA (graduated pipeline) | Platform | IHwndCaptureService, ITextInjectionService |
| 17 | SharpToken | OSS | ITokenizer |
| 18 | Whisper API / Whisper.net | SaaS/OSS | ISTTProvider |
| 19 | NAudio | OSS | IAudioService |
| 20 | AForge.NET Webcam | OSS | ICameraService |
| 21 | MediaElement / LibVLCSharp | Platform/OSS | IVideoPlayerService |
| 22 | Hunspell Spell Check | OSS | ISpellCheckService |
| 23 | LibGit2Sharp | OSS | IWikiGitService |

---

## Architecture Decision Log

Key architectural decisions made during planning, with rationale.

| # | Decision | Rationale | Source |
|---|----------|-----------|--------|
| AD-1 | **WPF over WinUI 3 / Avalonia / Electron** | WPF provides the deepest Windows integration (global hooks, HWND access, system tray, DPI) with 18+ years of maturity. No other .NET framework matches this combination. | [tech-sourcing #1](../tech-sourcing.md#1-ui-framework--wpf-net-application-shell) |
| AD-2 | **SQLite over LiteDB / SQL Server LocalDB** | Zero-config, in-process, full SQL with FTS5, mature EF Core support. The vision explicitly calls for SQLite. No server installation needed. | [tech-sourcing #2](../tech-sourcing.md#2-local-database--sqlite) |
| AD-3 | **Provider Adapter Pattern over LangChain / Semantic Kernel** | Cherry Studio validates the adapter pattern at scale. A full agent framework adds unnecessary orchestration overhead. The vision's tool use is bounded — basic function-calling loop suffices. | [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |
| AD-4 | **Markdig + Custom FlowDocument Renderer over WebView2** | Native WPF text rendering provides proper accessibility, selection, DPI scaling, and bidirectional text. WebView2 adds ~100MB and DPI/scaling quirks. Progressive streaming is harder in FlowDocument but worth the native integration. | [tech-sourcing #4](../tech-sourcing.md#4-markdown--code-rendering-engine) |
| AD-5 | **RegisterHotKey (primary) + WH_KEYBOARD_LL (fallback)** | RegisterHotKey is the sanctioned Windows API — less AV suspicion. Low-level hooks only for combos RegisterHotKey can't handle. Code signing further mitigates AV false positives. | [tech-sourcing #5](../tech-sourcing.md#5-global-keyboard-hooks) |
| AD-6 | **UIA ValuePattern as primary text injection method** | Windows-MCP validates this pattern across 2M+ users. UIA is more reliable than WM_SETTEXT across modern UWP/WPF/Win32 apps. The layered fallback (UIA → WM_SETTEXT → clipboard Ctrl+V) ensures graceful degradation. | [tech-sourcing #6](../tech-sourcing.md#6-hwnd-capture--text-injection-spatial-anchoring) |
| AD-7 | **"Everything is a Thread" — unified ChatThread model** | All three tiers share the same data model and ChatThreadService. The tier is purely a UI manifestation. This avoids data model duplication and enables seamless elevation from Tier 1/2 transient to Tier 3 permanent. | [architecture.md](architecture.md), feature O1 |
| AD-8 | **Version-chain branching model (branchId + versionNumber + isActiveBranch)** | Every message edit creates a version, not an overwrite. The active conversation path follows isActiveBranch=true chain via recursive CTE. This is a core differentiator — no existing chat tool has this model. | [tech-sourcing #31](../tech-sourcing.md#31-message-branching-data-model) |
| AD-9 | **Wiki files on disk are source of truth; SQLite holds read-optimized index** | Plain .md files editable by any external tool. The app maintains an index (headings, cross-links, FTS5 content) for fast search and cross-referencing. FileSystemWatcher keeps the index in sync. | [tech-sourcing #13](../tech-sourcing.md#13-wiki-indexing--cross-linking-engine) |
| AD-10 | **N6 snapshots + Git as complementary version control** | N6 snapshots provide instant undo within the app (fast, no git overhead). Git provides cross-session version history and optional remote push. They serve different purposes and coexist. | [tech-sourcing #28](../tech-sourcing.md#28-git-integration-wiki-version-control), Flag #13 |
| AD-11 | **Singleton DbContext for single-user desktop app** | No concurrent requests in a single-user app. A single DbContext instance avoids connection management overhead. Write operations serialized via SemaphoreSlim. | [platform-notes.md](platform-notes.md#3-dependency-injection--microsoftextensionsdependencyinjection) |
| AD-12 | **Windows-MCP as design reference only, not runtime dependency** | Windows-MCP is Python-based. Adding Python runtime (~50MB+) to a .NET WPF app for UI automation is unjustified. We implement the validated patterns (UIA-first, layered fallback) natively in C#. | [tech-sourcing #6](../tech-sourcing.md#6-hwnd-capture--text-injection-spatial-anchoring) |
| AD-13 | **GCS backup + local folder backup** | GCS provides off-site disaster recovery. Local folder backup provides a zero-dependency alternative. Both implement IBackupProvider. Mitigates Flag #9 (single cloud provider dependency). | [tech-sourcing #26](../tech-sourcing.md#26-backup-to-google-cloud-storage) |
| AD-14 | **Hard-delete transient threads; soft-delete permanent threads only** | Transient threads are ephemeral by design (7-day auto-cleanup). Soft-delete (30-day Trash) adds unnecessary complexity for ephemeral content. Permanent threads go through soft-delete → Trash → 30-day purge. | [data-model.md](data-model.md#soft-delete-chatthread) |
| AD-15 | **MVVM with CommunityToolkit.Mvvm source generators** | Eliminates boilerplate (INotifyPropertyChanged, ICommand). Source generators produce compile-time code — no runtime reflection overhead. Strict MVVM: Views NEVER reference ViewModels; Messenger for cross-VM communication. | [platform-notes.md](platform-notes.md#1-mvvm-pattern--communitytoolkitmvvm) |
| AD-16 | **Diagnostics as W1.3b — immediately after logging infrastructure** | Diagnostics (V) builds directly on Serilog (W1.3) and is placed as W1.3b so every subsequent Wave 1 feature can be built with full diagnostic logging available for debugging during development. The SettingsRepository dependency (W1.4) is soft-resolved: logging runs with in-memory defaults until W1.4 provides persistence. | [roadmap.md](../roadmap.md#feature-w13b--diagnostics--debug-logging) |

### Flagged Decisions Pending Architect Resolution

| Flag | Issue | Options | Source |
|------|-------|---------|--------|
| **Persona FK on Delete** | When a Persona is deleted, what happens to ChatThread.personaId? | (a) Nullify FK — threads retain persona name as string, lose reference. (b) Restrict — prevent deletion if referenced. | [data-model.md](data-model.md#architect-decision-flags) |
| **ModelConfiguration FK on Delete** | When a ModelConfiguration is deleted, what happens to Personas referencing it? | (a) Restrict — warn and block. (b) Nullify — Persona loses default config, must reassign. | [data-model.md](data-model.md#architect-decision-flags) |
| **MediaItem Soft-Delete** | When user deletes from Media Library (G3), soft-delete or hard-delete? | (a) Soft-delete — 30-day Trash, reversible. (b) Hard-delete — media file deleted from disk if not saved elsewhere. | [data-model.md](data-model.md#architect-decision-flags) |
| **Auto-Summarize Cost Transparency** | B8 Auto-Summarize strategy makes a separate API call costing tokens. How to inform user? | (a) Silent — summarize with no warning. (b) Toast notification — "Summarizing older messages (est. N tokens)." (c) Confirmation dialog. | Flag #1, feature B8 |
| **Text Completion Deprecation** | OpenAI deprecating text completion endpoint. What is E2's future? | (a) Remove E2 — standard mode only. (b) Emulate via chat completions API with system prompt. (c) Keep for providers that still support it. | Flag #2, feature E2 |
| **MessageDrafts Table Schema** | Referenced in architecture/abstractions but not defined as vision entity. | Add lightweight schema: threadId, content, cursorPosition, savedAt. | architecture.md, abstractions.md |

---

## Risk Heatmap

| Risk Level | Count | Key Areas |
|-----------|-------|-----------|
| 🔴 **High** | 1 | Terminal execution (H2) — mandatory user confirmation is the primary mitigation |
| 🟠 **Medium-High** | 2 | HWND capture & text injection (layered fallback mitigates), Three-tier interaction architecture (shared state across 3 window types) |
| 🟡 **Medium** | 8 | Markdown progressive rendering, Global hooks (AV false positives), Git integration, Deep Research, Branching model, BiDi text, GCS backup, PDF export |
| 🟢 **Low-Medium** | 4 | LLM providers, FileSystemWatcher reliability, Chat import parsing, Encryption UX |
| ⚪ **Low** | 21 | All other components |

---

## Vision-to-Planning Traceability

Every vision feature group (A-U) is addressed in the planning documents:

| Feature Group | Architecture | Tech Stack | Data Model | Abstractions |
|---------------|-------------|-----------|------------|--------------|
| A. Settings & Config | DI, Settings Repository | DPAPI, SQLite | ApiKey, Persona, ModelConfig | ISettingsRepository, IApiKeyRepository |
| B. Model Configs & Personas | Provider/Adapter pattern | OpenAI/Anthropic/Google SDKs | Persona, ModelConfig, ApiKey | ILLMProvider, IPersonaRepository |
| C. Studio Chat | Content Renderer Registry, Streaming | Markdig, AvalonEdit, WPF FlowDocument | ChatThread, Message, UsageRecord | IContentBlockRenderer, IChatThreadService |
| D. Message Branching | Recursive CTE, Chat Tree | SQLite recursive CTE | Message (branchId, versionNumber) | IMessageRepository |
| E. Chat Modes | ChatThreadService | — | ChatThread (chatMode, thinkingEnabled) | IChatThreadService |
| F. Artifacts | Artifact Renderer | DiffPlex | Artifact | IContentBlockRenderer (ArtifactReferenceRenderer) |
| G. Media Library | Media Renderer | NAudio, AForge, MediaElement | MediaItem | IContentBlockRenderer (MediaRenderer) |
| H. Tool Use | Tool Orchestrator | System.Diagnostics.Process, HttpClient | ChatThread, Message | IToolOrchestrator, IToolExecutor |
| I. Import/Export | ChatImportService | System.Text.Json, QuestPDF | ChatThread, Message | IChatImporter |
| J. Prompt Library | — | SQLite | PromptTemplate | — |
| K. Text Actions & Three-Tier | Three window types + ChatThreadService + graduated UIA capture pipeline | P/Invoke, UIA (TextPattern, ValuePattern, TreeWalker, DocumentRange), Clipboard, Win32 GDI | TextAction (captureScope + applyMode), ChatThread, Message | IGlobalHotkeyService, IHwndCaptureService, ITextInjectionService |
| L. Chat Organization | ChatThreadService | SQLite FTS5 | ChatThread, Message | IChatThreadRepository |
| M. Model Comparison | Multi-stream orchestration | ILLMProvider (parallel streams) | Persona, ModelConfig, ChatThread | ILLMProviderService |
| N. Personal Wiki | Wiki Indexing Engine | Markdig, FileSystemWatcher, LibGit2Sharp | WikiFile, WikiVersionSnapshot | IWikiService |
| O. Data Lifecycle | AutoCleanupService | SQLite VACUUM | ChatThread, Message | IChatThreadRepository |
| P. Windows OS Integration | Three-tier windows, P/Invoke | RegisterHotKey, UIA, Kestrel, NotifyIcon | — | IGlobalHotkeyService, ISystemTrayService |
| Q. Language & RTL | FlowDocument BiDi | WPF FlowDirection | — | — |
| R. Backup & Recovery | IBackupProvider | Google.Cloud.Storage.V1 | BackupSnapshot | IBackupProvider |
| S. Usage Dashboard | IUsageRepository | LiveCharts2, SQLite aggregation | UsageRecord | IUsageRepository |
| U. Soft-Delete Trash | AutoCleanupService | SQLite | ChatThread (isDeleted, deletedAt) | IChatThreadRepository |
| **V. Diagnostics & Debug Logging** | Serilog destructuring policy, Settings → Diagnostics UI | Serilog (existing W1.3), ISettingsRepository | AppSetting (9 key-value pairs) | ILogger\<T\>, ISettingsRepository, IDestructuringPolicy |
| T. Nice-to-Have | Architecture accommodates | (deferred) | (deferred) | (deferred) |

---

*Planning summary document — the index for the complete `planning/` directory. Batch 2 of 2. Planning directory is now complete (7 files). Updated 2026-06-18 with V. Diagnostics & Debug Logging. See [`tech-sourcing.md`](../tech-sourcing.md) for upstream sourcing decisions.*
