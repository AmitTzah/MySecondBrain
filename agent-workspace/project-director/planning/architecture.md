# Architecture — MySecondBrain

## High-Level System Design

MySecondBrain is a native Windows 10/11 desktop application built on **.NET 8.0 WPF**, architected around a **Three-Tier UI model**, a **provider-agnostic LLM abstraction layer**, and a **local-first SQLite data layer**. All design decisions reference the approved sourcing study in [`tech-sourcing.md`](../tech-sourcing.md).

---

## Component Diagram

The system is organized into six major component groups. Arrows indicate dependency direction (caller → callee).

```
┌─────────────────────────────────────────────────────────────────────┐
│                        THREE-TIER UI LAYER                          │
│  ┌──────────────┐  ┌──────────────────┐  ┌───────────────────────┐ │
│  │ Tier1Overlay │  │ Tier2CommandBar  │  │ Tier3Studio/MainWindow│ │
│  │ (pill popup) │  │ (spotlight bar)  │  │ (full workspace)      │ │
│  └──────┬───────┘  └────────┬─────────┘  └───────────┬───────────┘ │
│         │                   │                        │             │
│         └───────────────────┼────────────────────────┘             │
│                             │                                      │
│              All three tiers share ChatThreadService                │
└─────────────────────────────┬───────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      APPLICATION SERVICES                           │
│                                                                     │
│  ┌────────────────┐  ┌──────────────────┐  ┌────────────────────┐  │
│  │ ChatThread     │  │ Wiki Indexing    │  │ Tool Use           │  │
│  │ Service        │  │ Engine           │  │ Orchestrator       │  │
│  │ (CRUD, branch  │  │ (AST walk, FTS5, │  │ (function-calling  │  │
│  │  nav, search)  │  │  cross-links)    │  │  loop, confirma-   │  │
│  └───────┬────────┘  └────────┬─────────┘  │  tion, execution)   │  │
│          │                    │             └─────────┬──────────┘  │
│          │                    │                       │             │
│  ┌───────┴────────────────────┴───────────────────────┴──────────┐  │
│  │              LLM Provider Abstraction Layer                   │  │
│  │  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌─────────────┐  │  │
│  │  │ OpenAI   │  │ Anthropic │  │ Google   │  │ OpenAI-     │  │  │
│  │  │ Adapter  │  │ Adapter   │  │ Adapter  │  │ Compatible  │  │  │
│  │  └────┬─────┘  └─────┬─────┘  └────┬─────┘  └──────┬──────┘  │  │
│  │       │              │             │               │         │  │
│  │       └──────────────┼─────────────┼───────────────┘         │  │
│  │                      │             │                          │  │
│  │               Common ILLMProvider interface                   │  │
│  │               Normalized StreamChunk DTO                      │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    DATA LAYER (SQLite + EF Core)                     │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │ ChatThread       │  │ Wiki Index       │  │ Settings /       │  │
│  │ Repository       │  │ Repository       │  │ Config Repository│  │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘  │
│           │                     │                      │            │
│           └─────────────────────┼──────────────────────┘            │
│                                 │                                   │
│                     Entity Framework Core                           │
│                     Microsoft.Data.Sqlite                           │
│                     SQLite FTS5 (full-text search)                  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Component Descriptions

### 1. Three-Tier UI Layer

| Component | Description | Sourcing Ref |
|-----------|-------------|-------------|
| **Tier1OverlayWindow** | Transparent, topmost pill overlay near cursor. Three-phase flow per TextAction configuration: Capture (graduated UIA pipeline per captureScope flags: selection→TextPattern, focusedElement→ValuePattern, surroundingContext→TreeWalker, fullDocument→DocumentRange, screenshot→PrintWindow/BitBlt) → Result (editable AI response with Accept/Discard/Open in Studio/Save to Wiki/Retry + Additional Instructions) → Apply (per applyMode: replaceSelection, insertAtCursor, replaceFocusedElement, appendToFocusedElement, prependToFocusedElement, clipboardOnly, showOnly). | [tech-sourcing #32](../tech-sourcing.md#32-three-tier-interaction-architecture) |
| **Tier2CommandBarWindow** | Spotlight-style centered overlay (Alt+Space). Inline state: input + expandable Q&A display. Popped-out state: floating resizable mini-window. Elevation to Studio creates permanent ChatThread. Dismissal saves as transient. | [tech-sourcing #32](../tech-sourcing.md#32-three-tier-interaction-architecture) |
| **MainWindow (Tier 3 Studio)** | Full workspace: multi-tab chat, resizable panels, sidebar chat list, right panel (Artifacts + Chat Nav), streaming Markdown rendering, model comparison, wiki browser, all settings screens. | [tech-sourcing #1](../tech-sourcing.md#1-ui-framework--wpf-net-application-shell) |

**Key architectural insight:** All three tiers share the same `ChatThreadService` singleton and the same `ChatThread` + `Message` data model. The tier is purely a UI manifestation — the underlying data and LLM interaction are identical regardless of which tier originated the conversation.

### 2. LLM Provider Abstraction Layer

| Component | Description | Sourcing Ref |
|-----------|-------------|-------------|
| **ILLMProvider interface** | Common contract: `ChatAsync()`, `ChatStreamAsync()`, `ListModelsAsync()`, `ValidateKeyAsync()`. Normalized `StreamChunk` DTO. | [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |
| **OpenAI Adapter** | Wraps official OpenAI .NET SDK. Covers OpenAI, DeepSeek, Mistral, and any OpenAI-compatible endpoint. | [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |
| **Anthropic Adapter** | Wraps community Anthropic.SDK. Normalizes Anthropic's Messages API into common `StreamChunk`. | [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |
| **Google Adapter** | Wraps Google.Cloud.AIPlatform.V1 SDK for Gemini. | [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |
| **OpenAI-Compatible Adapter** | Generic adapter for any OpenAI-API-compatible service (including local open-source models). User configures endpoint URL + optional API key. | Feature B6; [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |

**Pattern validated by:** Cherry Studio's multi-provider adapter architecture, which normalizes SSE formats into a common streaming chunk DTO across providers.

### 3. ChatThread Service

| Component | Description | Sourcing Ref |
|-----------|-------------|-------------|
| **ChatThreadService** | Singleton service managing ChatThread CRUD, message branching (version-chain model with `branchId`, `versionNumber`, `isActiveBranch`), transient/permanent lifecycle, elevation, auto-save drafts. All three tiers depend on this service. | [tech-sourcing #31](../tech-sourcing.md#31-message-branching-data-model) |
| **ChatSearchService** | Full-text search across chat messages via SQLite FTS5. Returns snippets with highlights, chat name, timestamp. | [tech-sourcing #11](../tech-sourcing.md#11-full-text-search) |
| **ChatImportService** | Parses ChatGPT export JSON and Claude export JSON via `System.Text.Json`. Creates ChatThreads with duplicate detection. | [tech-sourcing #29](../tech-sourcing.md#29-chat-import-parsing-chatgpt--claude) |
| **AutoCleanupService** | Background task: deletes transient threads older than 7 days, purges Trash older than 30 days. Auto-elevates threads with exceptions (favorited, tagged, pinned, archived, has user replies, has artifacts). | Feature O4; [tech-sourcing #31](../tech-sourcing.md#31-message-branching-data-model) |

### 4. Wiki Indexing Engine

| Component | Description | Sourcing Ref |
|-----------|-------------|-------------|
| **WikiIndexer** | Scans wiki directory, parses each `.md` file via Markdig AST walker, extracts headings/links/backlinks/plain text. Stores metadata in SQLite wiki index tables. Auto-updates on file change (debounced FileSystemWatcher). | [tech-sourcing #13](../tech-sourcing.md#13-wiki-indexing--cross-linking-engine) |
| **WikiSearchService** | Full-text wiki search via SQLite FTS5 on wiki content. Powers wiki search (N3), @-mentions (N7), related sections (N4). | [tech-sourcing #11](../tech-sourcing.md#11-full-text-search) |
| **CrossLinkEngine** | Computes forward-links during indexing (AST walk extracts `[text](target.md#heading)`). Computes backlinks by querying the cross-links index. Generates `index.md` from local index data. | [tech-sourcing #13](../tech-sourcing.md#13-wiki-indexing--cross-linking-engine) |
| **WikiVersioningService** | Creates pre-modification snapshots of wiki files (max 30 per file, 50MB cap). Recoverable from Wiki Browser. | Feature N6; [tech-sourcing #13](../tech-sourcing.md#13-wiki-indexing--cross-linking-engine) |
| **WikiGitService** | Git init, auto-commit (debounced 5s), optional GitHub push via LibGit2Sharp. Token encrypted via DPAPI. | [tech-sourcing #28](../tech-sourcing.md#28-git-integration-wiki-version-control) |

### 5. Tool Use Orchestrator

| Component | Description | Sourcing Ref |
|-----------|-------------|-------------|
| **ToolOrchestrator** | Implements the function-calling loop: AI requests tool → orchestrator validates → executes (or prompts user confirmation) → captures result → feeds back to AI. Tools: WebSearch, Terminal, FileGenerate, FileEdit, WikiSearch. | [tech-sourcing #19](../tech-sourcing.md#19-deep-research-orchestration-h6) |
| **WebSearchTool** | Executes web search via Google Custom Search API or Bing Web Search API (user's own API key). Returns structured results. | [tech-sourcing #16](../tech-sourcing.md#16-web-search-integration-tool-use-h1) |
| **TerminalTool** | Executes shell commands via `System.Diagnostics.Process`. ALWAYS requires explicit user confirmation. Displays command, working directory, risk level (detects dangerous commands). Captures stdout/stderr. | [tech-sourcing #17](../tech-sourcing.md#17-terminal--shell-execution-tool-use-h2) |
| **FileTool** | File generation (save dialog → AI generates → preview → confirm) and file editing (file picker → AI suggests changes → diff preview via DiffPlex → confirm). Enforces wiki directory exclusion. | [tech-sourcing #18](../tech-sourcing.md#18-file-generation--editing-tool-use-h3-h4) |
| **DeepResearchOrchestrator** | Custom state machine (Plan → Search → Read → Synthesize → Report) driven by tool-use conversation loop with specialized "deep research" system prompt. Real-time progress display via system messages. | [tech-sourcing #19](../tech-sourcing.md#19-deep-research-orchestration-h6) |

### 6. SQLite Data Layer

| Component | Description | Sourcing Ref |
|-----------|-------------|-------------|
| **Entity Framework Core** | ORM for all data entities (13 entities: ChatThread, Message, Persona, ModelConfiguration, ApiKey, Artifact, MediaItem, PromptTemplate, TextAction, UsageRecord, WikiFile, WikiVersionSnapshot, BackupSnapshot). Migrations for schema evolution across auto-updates. | [tech-sourcing #2](../tech-sourcing.md#2-local-database--sqlite) |
| **SQLite FTS5** | Full-text search virtual tables on chat message content and wiki file content. Ranked search, snippet extraction, highlight markup. | [tech-sourcing #11](../tech-sourcing.md#11-full-text-search) |
| **DPAPI Encryption** | `System.Security.Cryptography.ProtectedData` for API key encryption at rest (tied to Windows user account). `AesGcm` (.NET 8+) for locked chat encryption (PBKDF2 key derivation). | [tech-sourcing #30](../tech-sourcing.md#30-encryption--api-keys--chat-locking) |

---

## Data Flow: Message Lifecycle

The following describes how a user message flows through the system — from textbox input to streaming rendering to SQLite persistence.

### Stream: User sends a message in Studio

```
User types in textbox
        │
        ▼
[1] ChatThreadService.CreateMessage()
    - Creates Message entity (role=User, content=textbox text)
    - Inserts into SQLite via EF Core ChatThreadRepository
    - Updates ChatThread.LastActivityAt
        │
        ▼
[2] ChatThreadService.SendToLLM()
    - Builds conversation context: fetches active branch messages
      from SQLite (recursive CTE following parentMessageId chain
      where isActiveBranch=true)
    - Prepares request: system message + message history + user message
        │
        ▼
[3] LLMProviderService.ChatStreamAsync()
    - Resolves ILLMProvider adapter from ModelConfiguration
    - Calls provider adapter with request
    - Adapter makes HTTP request to provider API via vendor SDK
        │
        ▼
[4] Streaming loop: for each StreamChunk from provider
    ├─ Content delta → MarkdownStreamRenderer.AppendToken()
    │   - Token-by-token rendering via Markdig incremental parse
    │   - Convert Markdig AST → WPF FlowDocument elements
    │   - Update FlowDocument in-place (progressive rendering)
    │   - Scroll-to-bottom management (auto-pause if user scrolled up)
    ├─ Thinking tokens → ThinkingPanel (if thinking enabled)
    ├─ Tool call request → ToolOrchestrator
    │   - Validate tool, check auto-approval settings
    │   - Execute tool or show confirmation dialog
    │   - Feed tool result back to LLM (continue streaming)
    ├─ Usage info → TokenCounter (SharpToken, real-time)
    └─ Finish reason → end of stream
        │
        ▼
[5] On stream complete: ChatThreadService.CreateMessage()
    - Creates Message entity (role=Assistant, content=full Markdown,
      tokenCount, estimatedCost, generationTimeMs, personaId, modelConfigId)
    - Inserts into SQLite via EF Core
    - Updates FTS5 index for full-text search
    - Updates usage records (UsageRecord entity)
    - Triggers auto-save draft cleanup (textbox now empty)
        │
        ▼
[6] UI updates
    - Cross-tab completion alert (green dot on inactive tabs)
    - Sound notification (if not muted)
    - Token/cost display in chat header updated
    - Scroll-to-bottom button shown if user scrolled away
```

### Tier 1 Hotkey Flow (abbreviated)

```
User presses assigned hotkey in any app (e.g., Alt+Q for Rewrite)
        │
        ▼
[1] GlobalKeyboardHook receives hotkey → resolves active TextAction
    (captureScope flags + systemPrompt + modelConfigId + applyMode)
        │
        ▼
[2] Tier1OverlayWindow opens — Capture Phase
    - Graduated UIA pipeline per captureScope flags:
      selection → TextPattern; focusedElement → ValuePattern;
      surroundingContext → TreeWalker; fullDocument → DocumentRange;
      screenshot → PrintWindow/BitBlt
    - Captures HWND + source app name + document title + clipboard formats
    - "Thinking…" pill overlay shown near cursor
        │
        ▼
[3] Same as Studio flow steps [2]→[5] (LLM call + streaming)
    - TextAction's system prompt applied
    - Captured content (text + optional screenshot) = user message
        │
        ▼
[4] Result popup shown with Accept/Discard/Open in Studio/Retry
        │
        ▼
[5] On Accept: apply per TextAction's applyMode
    (replaceSelection → HWND injection; insertAtCursor → UIA TextPattern;
     replaceFocusedElement → UIA ValuePattern; append/prepend;
     clipboardOnly → clipboard; showOnly → no injection)
    On Open in Studio: MainWindow opens, ChatThread elevated (IsTransient=false)
    On Discard: overlay closes, transient ChatThread saved
```

---

## Architectural Patterns

### MVVM — Model-View-ViewModel

**Library:** CommunityToolkit.Mvvm (MIT license)

All UI is built on MVVM. Every WPF Window/UserControl has a corresponding ViewModel. Data binding is the primary mechanism for UI updates; code-behind is minimal and restricted to view-specific concerns (focus management, overlay positioning, window Z-order).

- **Models:** EF Core entity classes (ChatThread, Message, Persona, etc.)
- **ViewModels:** CommunityToolkit.Mvvm `ObservableObject` subclasses with `[RelayCommand]` and `[ObservableProperty]` source generators
- **Views:** WPF XAML with `DynamicResource` references for theming
- **DI:** `Microsoft.Extensions.DependencyInjection` wires ViewModels → Services → Repositories

### Provider/Adapter Pattern

Used for all external integrations where multiple implementations exist:

| Interface | Adapters | Sourcing Ref |
|-----------|----------|-------------|
| `ILLMProvider` | OpenAI, Anthropic, Google, OpenAICompatible | [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client) |
| `ISTTProvider` | OpenAI Whisper API, Whisper.net (local), Windows Speech (System.Speech) | [tech-sourcing #21](../tech-sourcing.md#21-speech-to-text-stt) |
| `IBackupProvider` | Google Cloud Storage, Local Folder | [tech-sourcing #26](../tech-sourcing.md#26-backup-to-google-cloud-storage) |
| `ISearchProvider` | Google Custom Search, Bing Web Search | [tech-sourcing #16](../tech-sourcing.md#16-web-search-integration-tool-use-h1) |
| `ITokenizer` | SharpToken (OpenAI), AnthropicTokenizer, FallbackTokenizer (chars/4) | [tech-sourcing #10](../tech-sourcing.md#10-local-tokenization-real-time-token-counting) |
| `IChatImporter` | ChatGPT JSON parser, Claude JSON parser | [tech-sourcing #29](../tech-sourcing.md#29-chat-import-parsing-chatgpt--claude) |
| `IThemeProvider` | Dark mode, Light mode; 3 chat themes (Classic, Compact, Bubble) | [tech-sourcing #33](../tech-sourcing.md#33-theming--darklight-mode) |
| `IUpdateChecker` | AutoUpdater.NET, MsixAppInstallerUpdater | [tech-sourcing #27](../tech-sourcing.md#27-auto-update-mechanism) |

Full interface contracts are defined in [`abstractions.md`](abstractions.md).

### Repository Pattern — EF Core

Data access is abstracted behind repository interfaces. EF Core `DbContext` provides the unit-of-work; repositories provide domain-specific query methods. This enables testing via in-memory SQLite and clean separation between business logic and data access.

Key repositories:
- `IChatThreadRepository` — ChatThread CRUD, branch queries, search
- `IMessageRepository` — Message CRUD, branch navigation (recursive CTE), FTS5 search
- `IPersonaRepository` — Persona/ModelConfiguration/ApiKey CRUD
- `IWikiIndexRepository` — WikiFile/WikiVersionSnapshot CRUD, cross-link queries
- `IUsageRepository` — UsageRecord queries, aggregation for dashboard
- `ISettingsRepository` — Key-value app settings

### Plugin/Registry Pattern — Content Block Renderers

Chat messages contain heterogeneous content blocks (Markdown text, code blocks with syntax highlighting, artifact references, media embeds). A registry of `IContentBlockRenderer` implementations converts Markdig AST nodes to WPF UI elements:

| Renderer | Handles | Output |
|----------|---------|--------|
| `MarkdownTextRenderer` | Paragraphs, headings, bold, italic, lists, links, tables, blockquotes | WPF `FlowDocument` elements |
| `CodeBlockRenderer` | Fenced code blocks with language declaration | Syntax-highlighted `FlowDocument` via AvalonEdit highlighting engine + copy button |
| `ArtifactReferenceRenderer` | Artifact references | Clickable artifact card → opens in side panel |
| `CitationRenderer` | Inline citation markers (`[1]`, `[2]`) from Deep Research / web search | Clickable superscript links that scroll to Sources footnote section. Each footnote shows index number, linked title, domain, and date-accessed. |
| `ImageRenderer` | Inline images (`![alt](url)`) | WPF `Image` control with click-to-enlarge |
| `MediaRenderer` | Audio/video embeds | NAudio mini player, WPF `MediaElement` |
| `ThinkingRenderer` | Thinking/reasoning tokens | Collapsible "Thinking…" accordion |
| `ToolCallRenderer` | Tool call/result system messages | Styled border with tool name, parameters, result summary |

Renderers are registered in `ContentRendererRegistry` at startup. The `MarkdownStreamRenderer` resolves the appropriate renderer for each AST node during progressive rendering.

### Citation Rendering Pipeline

Citations flow through a dedicated rendering path within the content block renderer system:

```
Message.content (Markdown with [1] markers + [^1]: footnotes)
        │
        ▼
Markdig parses → AST with inline LinkInline nodes and Footnote nodes
        │
        ▼
ContentRendererRegistry.Resolve(node)
        │
        ├── For [1], [2] markers: CitationRenderer (priority 350)
        │   └── Renders as clickable superscript Hyperlink
        │       └── On click: FrameworkElement.BringIntoView()
        │           to navigate to [^1]: footnote Paragraph
        │
        ├── For [^1]: footnotes: CitationRenderer
        │   └── Renders as styled Paragraph with:
        │       • Index number
        │       • Bold Hyperlink (title → source URL)
        │       • Domain in secondary foreground color
        │       • Date-accessed in muted foreground color
        │
        └── For all other nodes: standard renderer chain (MarkdownText, etc.)
```

The citation data itself (index, title, domain, date-accessed) is **not stored in a separate entity** — it is embedded directly in the Message's Markdown `content` field as structured footnotes (see [`data-model.md`](data-model.md#deep-research-citations-embedded-in-message-content)). The `CitationRenderer` is purely a rendering concern: it parses the footnote format from Markdown and produces WPF UI elements with click-to-scroll behavior. This keeps the data model simple (no new entity) while providing the interactive citation experience specified in the vision ([`deep-research.md`](../vision/flows/deep-research.md#step-6-final-output--structured-report)).

**Priority rationale (350):** CitationRenderer sits between `ArtifactReferenceRenderer` (300) and `ImageRenderer` (400) because citation markers (`[1]`) are more specific than generic link rendering but less specific than artifact references. The `ContentRendererRegistry` scans renderers in ascending priority order, so CitationRenderer intercepts footnote-style links before `ImageRenderer` or `MarkdownTextRenderer` process them as generic links.

---

## Deployment Model

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Packaging** | MSIX packaged installer | Modern Windows packaging; supports auto-update via App Installer. |
| **Distribution** | Direct download + code-signed | Code signing mitigates SmartScreen warnings and antivirus false positives for global keyboard hooks. |
| **Auto-Update** | AutoUpdater.NET | Purpose-built for .NET desktop apps. Checks against remote feed (JSON/XML), downloads MSIX, triggers install. Configurable check frequency (startup/daily/weekly/manual). |
| **User Model** | Single-user, local-first | No accounts, no login, no multi-user. All data stored locally in `%LOCALAPPDATA%\MySecondBrain\`. |
| **Database** | SQLite on disk | Zero-config, in-process. Database file at `%LOCALAPPDATA%\MySecondBrain\msb.db`. |
| **Wiki Storage** | User-chosen directory of `.md` files | Plain Markdown files on disk, editable by any external tool. App maintains a read-optimized SQLite index. |
| **Backup** | Google Cloud Storage + Local Folder | Full backup of SQLite DB + wiki `.md` files + artifacts. Scheduled (daily/weekly) or manual. Credentials encrypted via DPAPI. |
| **API Keys** | BYO keys, encrypted via DPAPI | User provides own API keys for each provider. Keys encrypted at rest via `ProtectedData.Protect()` (tied to Windows user account). |
| **WebSocket Server** | ASP.NET Core Kestrel on 127.0.0.1 | Embedded local-only server for external integrations (Word Add-in). Token-based auth. |

---

## Cross-Cutting Concerns

| Concern | Implementation | Sourcing Ref |
|---------|---------------|-------------|
| **Theming** | WPF ResourceDictionary with DynamicResource. Two top-level dictionaries (Dark.xaml, Light.xaml). Three chat templates (Classic, Compact, Bubble). Instant toggle without restart. | [tech-sourcing #33](../tech-sourcing.md#33-theming--darklight-mode) |
| **DPI Awareness** | PerMonitorV2 in app.manifest. WPF device-independent pixels handle scaling natively. | [tech-sourcing #36](../tech-sourcing.md#36-per-monitor-dpi-awareness) |
| **BiDi Text** | WPF FlowDocument built-in Unicode Bidi Algorithm. Per-message direction detection (Hebrew Unicode range U+0590-U+05FF). Code blocks always LTR. | [tech-sourcing #34](../tech-sourcing.md#34-bidirectional-text-rendering-hebrew-rtl) |
| **Global Hotkeys** | RegisterHotKey (primary) + WH_KEYBOARD_LL (fallback). Alt+Q/W/E/R for Tier 1, Alt+Space for Tier 2. Configurable in Settings. | [tech-sourcing #5](../tech-sourcing.md#5-global-keyboard-hooks) |
| **Spell Check** | WeCantSpell.Hunspell with English dictionary. Custom WPF adorner for red squiggly underlines. Right-click suggestions. | [tech-sourcing #25](../tech-sourcing.md#25-spell-checking) |
| **Encryption** | API keys: DPAPI (`ProtectedData`). Locked chats: AES-256-GCM with PBKDF2 key derivation (`.NET 8 AesGcm` + `Rfc2898DeriveKey`). | [tech-sourcing #30](../tech-sourcing.md#30-encryption--api-keys--chat-locking) |
| **Auto-Save Drafts** | `PeriodicTimer` (5-second tick) → serialize textbox content + cursor position to SQLite `MessageDrafts` table. Recovery dialog on tab open. | [tech-sourcing #35](../tech-sourcing.md#35-auto-save-drafts) |
| **Diagnostics Logging** | Serilog rolling file sink → `%LOCALAPPDATA%\MySecondBrain\logs\`. Structured JSON output. 8 per-category toggles + global log level (Information/Debug/Verbose) persisted via `ISettingsRepository`. API key redaction via `IDestructuringPolicy`. "Open Logs Folder" + "Clear Logs" buttons in Settings → Diagnostics. | [Vision V](../vision/features/diagnostics-debug-logging.md), [abstractions §14](abstractions.md#14-diagnostics--logging--serilog-destructuring-policy) |

---

*Architecture document — Batch 1 of planning/ directory. See also: [`tech-stack.md`](tech-stack.md), [`abstractions.md`](abstractions.md).*
