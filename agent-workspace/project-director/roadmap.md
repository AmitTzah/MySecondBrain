# Roadmap — MySecondBrain Wave Map

Lightweight feature decomposition. 35 features across 4 waves. Each Wave 3 feature is a vertical slice (DB → service → UI) that **adds** new code to pre-built Wave 1-2 infrastructure — Wave 3 features do NOT modify Wave 1-2 code.

> **Revision note:** Originally planned at 245 features. Consolidated to 34 after Feature 3 (2026-06-18) based on efficiency analysis — each feature now represents a coherent functional domain built in one FD spawn. Updated to 35 with W1.3b Diagnostics (2026-06-18).

Source documents:
- Vision: [`vision/vision-summary.md`](vision/vision-summary.md), [`vision/feature-inventory.md`](vision/feature-inventory.md)
- Planning: [`planning/architecture.md`](planning/architecture.md), [`planning/abstractions.md`](planning/abstractions.md), [`planning/data-model.md`](planning/data-model.md), [`planning/tech-stack.md`](planning/tech-stack.md), [`planning/platform-notes.md`](planning/platform-notes.md)
- Tech sourcing: [`tech-sourcing.md`](tech-sourcing.md)

---

## Wave 1: Foundation — 9 Features

Infrastructure that everything else depends on. Core abstractions, data model, project scaffolding. Built first, extended by all subsequent waves.

### Feature W1.1 — Solution Scaffold & CI/CD Pipeline ✅

**.NET 8.0 WPF solution with all 7 projects, 15 OSS NuGet packages, MSIX packaging placeholder, and GitHub Actions CI/CD skeleton.**

Dependencies: none.

Status: **Built and merged to master.**


### Feature W1.2 — Dependency Injection Container ✅

**All service, repository, and ViewModel registrations via `Microsoft.Extensions.DependencyInjection`. 76+ registrations, 42+ C# interfaces, 13 entities, 8 repository stubs, all provider stubs, 11 ViewModels, 7 content block renderers.**

Dependencies: W1.1.

Status: **Built and merged to master.**


### Feature W1.3 — Logging Infrastructure ✅

**Serilog with rolling file sink to `%LOCALAPPDATA%\MySecondBrain\logs\`, JSON structured output, thread/machine enrichment.**

Dependencies: W1.2.

Status: **Built and merged to master.**


### Feature W1.3b — Diagnostics & Debug Logging

**Vision groups: V (Diagnostics & Debug Logging).**

Settings → Diagnostics category with global log level selector (Information/Debug/Verbose) and 8 per-category logging toggles: (1) LLM API Calls, (2) Tier 1 Hotkey Pipeline, (3) Tier 2 Command Bar — ON by default; (4) Database, (5) Wiki & File System, (6) WebSocket, (7) Startup & Shutdown, (8) System Integration — OFF by default. "Open Logs Folder" button opens `%LOCALAPPDATA%\MySecondBrain\logs\` in Explorer. "Clear Logs" button deletes all log files with confirmation. Serilog `IDestructuringPolicy` enforces API key redaction (`[REDACTED]`) across all categories. Nine settings persisted as `AppSetting` key-value pairs via `ISettingsRepository`. Uses existing Serilog infrastructure from W1.3.

Dependencies: W1.2, W1.3. (*Soft dependency on W1.4 for settings persistence — defaults used until W1.4 is built.*)


### Feature W1.4 — Data Layer: All Entities, DbContext & Repositories

**EF Core `AppDbContext` with SQLite connection, all 13 entity classes, FTS5 virtual tables, 8 repository implementations (replacing DI stubs), repository interfaces, initial database migration, and migration strategy for auto-updates.**

Dependencies: W1.2.


### Feature W1.5 — LLM Provider Abstraction Layer

**`ILLMProvider` interface with normalized `StreamChunk` DTO. Four provider implementations: `OpenAIProvider`, `AnthropicProvider`, `GoogleProvider`, `OpenAICompatibleProvider`. `ILLMProviderFactory` for runtime resolution. `ITokenizer` + `ITokenizerFactory` (`SharpTokenTokenizer`, `AnthropicTokenizer`, `FallbackTokenizer`).**

Dependencies: none (abstractions); W1.4 (persistence of model configs).


### Feature W1.6 — Platform Services & Abstractions

**All non-LLM abstractions and implementations: `ISTTProvider` (OpenAI Whisper, local Whisper, Windows Speech), `IBackupProvider` (GCS, local folder), `ISearchProvider` (Google, Bing), `IToolExecutor` × 5 + `IToolOrchestrator`, `IChatImporter` (ChatGPT, Claude), `IContentBlockRenderer` × 7 + `IContentRendererRegistry`, `IThemeProvider`, `IUpdateChecker`, `IEncryptionService` (DPAPI), `IChatEncryptionService` (AES-256-GCM), `IChatSearchService` (FTS5), `IAutoCleanupService` (transient/trash background task).**

Dependencies: none (abstractions); W1.4 (persistence).


### Feature W1.7 — Core Application Services

**`ILLMProviderService` (context overflow strategies, token counting), `IChatThreadService` (CRUD, branching, search, drafts, elevation), `IWikiService` (indexing, search, cross-links, versioning, git).**

Dependencies: W1.4, W1.5, W1.6.


### Feature W1.8 — Testing Infrastructure

**xUnit test project with in-memory SQLite, Moq/NSubstitute for mocking, Coverlet for coverage, EF Core integration tests with physical SQLite for FTS5/CTE validation.**

Dependencies: W1.1, W1.4.


---

## Wave 2: Skeleton — 3 Features

App shell, navigation, empty screens, theming, Windows infrastructure. All 8 screens exist as navigable shells with placeholder content.

### Feature W2.1 — App Shell, All 8 Screens & Theming

**MainWindow three-region shell (sidebar + content + right panel), tabbed navigation with drag-drop reorder, screen routing, sidebar shell with chat list/Trash/Timeline tabs. All 8 empty screen shells: Studio Chat, Onboarding Wizard, Model Comparison, Settings, Wiki Browser, Usage Dashboard, Media Library, Global Artifacts Browser. Dark/Light theme system with `DynamicResource`, 3 chat visual themes (Classic, Compact, Bubble), font settings (family, size 10-24px, weight).**

Dependencies: W1.2, W1.7.


### Feature W2.2 — Windows OS Infrastructure

**System tray integration (`NotifyIcon` with context menu, generation indicator), global hotkey registration (`RegisterHotKey` + `WH_KEYBOARD_LL` fallback, Alt+Q/W/E/R for Tier 1, Alt+Space for Tier 2), per-monitor DPI awareness (`PerMonitorV2`), auto-update framework (`AutoUpdater.NET`), local WebSocket server (Kestrel on 127.0.0.1, token auth).**

Dependencies: W1.2, W2.1.


### Feature W2.3 — MSIX Packaging & CI/CD Hardening

**EV Code Signing Certificate integration, `.appinstaller` file generation for auto-update, version stamping from git tags, release notes generation, CI/CD pipeline hardening (multi-stage: build → unit → integration → MSIX → code sign → deploy). Extends W1.1's MSIX placeholder and CI/CD skeleton.**

Dependencies: W1.1.


---

## Wave 3: Vertical Slices — 18 Features

End-to-end features (DB → service → UI). Each feature ADDS new code to pre-built Wave 1-2 infrastructure. Features do NOT modify Wave 1-2 code. Ordered by dependency chain. One feature per vision group A-U, with closely related domains merged.

### Feature W3.1 — Model Configurations, API Keys & Personas

**Vision groups: B (Model Configurations & Personas).**

API key management (add, edit, delete, test, DPAPI encryption, 8 provider types). Model configurations CRUD (temperature, tokens, thinking, pricing, context overflow strategy). Auto-fetch available models from provider API. "OpenAI-Compatible" provider type for local models (Ollama, LM Studio). Personas CRUD (system prompt, default model config, chat mode). Persona selection per chat. Local open-source model support. Context window overflow strategy (SlidingWindow/HardStop/AutoSummarize). Default profile selection (A2). STT provider configuration (A10).

Dependencies: W1.4–W1.7, W2.1.


### Feature W3.2 — Settings & Configuration

**Vision groups: A (Settings & Configuration, minus model/persona/STT sections handled by W3.1).**

Full Settings screen with 14 categories (Providers, Profiles, Appearance, Wiki, Backup, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Maintenance). Appearance settings (chat themes, font). Dark/Light mode toggle. Notification & streaming settings. Startup behavior (launch on startup, session restore). Auto-update settings. Database maintenance (VACUUM, size display). Onboarding wizard — five-step guided setup on first launch (Welcome → API Keys → Persona → Wiki → Hotkeys → Finish), skippable steps, re-launchable from Settings.

**Note:** Onboarding wizard Step 3 (Wiki directory selection) and Settings → Wiki category display wiki directory, indexing status, and git version control UI — these sections are non-functional until W3.13 builds the wiki infrastructure. The UI controls can be built now; backend wiki functionality activates when W3.13 is complete.

Dependencies: W1.4, W1.7, W2.1, W2.2, W3.1. (*Soft dependency on W3.13 for wiki-related UI functionality.*)


### Feature W3.3 — Studio Chat: Messaging, Rendering & Modes

**Vision groups: C (core messaging), E (Chat Modes & Controls).**

Conversation view with `VirtualizingStackPanel`. Full Markdown rendering (Markdig → WPF FlowDocument) with all constructs. Code block rendering with syntax highlighting (AvalonEdit, 100+ languages). Streaming response display (token-by-token progressive rendering). Message actions (Send/Stop, partial response preservation). Conditional [Apply] button for Tier 1 elevation. Copy MD / Copy Rich per message. Auto-generated chat titling. Continue generation. Error handling with specific messages and Retry. Scroll-to-bottom button. Auto-scroll behavior. Clear conversation with undo. Chat header three-dot menu (⋯). Message selection mode with bulk actions. Offline/network status indicator. Close confirmation with active generation. Pin window / Always on top. Dark/Light quick toggle. Font size quick adjust. Chat header full layout. Incognito/temporary chat toggle. Locked chats (AES-256-GCM, password-protected). Chat summarization. Message favoriting. Auto-save drafts (5-second timer, recovery). Cross-tab completion alert (green dot). Right panel layout (Artifacts + Chat Nav).

**Chat Modes (E):** Standard chat mode (conversational). Text completion mode (raw prompt → completion). Thinking toggle with reasoning display. Mute notifications per chat. Dynamic system message editing from header popover.

Dependencies: W1.4–W1.7, W2.1, W3.1.


### Feature W3.4 — Studio Chat: Input, Media & Workspace

**Vision groups: C (input, media, multi-tab).**

Drag & drop files/media into textbox. Paste image from clipboard (Ctrl+V). Model-aware file compatibility warnings. Audio input via microphone (NAudio → STT). Camera capture (webcam photo for vision models). Spell check with Hunspell (red squiggly, suggestions, custom dictionary). Multiple chat tabs (drag-drop reorder, close, reopen, indicators). Token usage & context display (real-time, color gradient bar). Keyboard shortcuts (Ctrl+N/W/Tab/F/S). Resizable panels (sidebar, right panel). Model comparison button in toolbar. Dynamic system message editing access. Duplicate/Fork chat access. Textbox toolbar (Persona selector, thinking, mute, tools, prompts, text actions).

Dependencies: W1.4–W1.7, W2.1, W3.3.


### Feature W3.5 — Message Branching & Manipulation

**Vision groups: D (Message Manipulation & Branching).**

Edit any past message (Edit in Place or Edit as Branch). Delete any past message (branch data preserved). Branch navigation (indicator "2/3", cycle arrows, re-render). Chat tree visualization (nodes = messages, edges = parentMessageId). Quote from chat (selected text → quoted block). Chat navigation bar (collapsible message list, click to jump). Duplicate/Fork chat from any message point. Message feedback (thumbs-up/down). Undo/Redo message edits (Ctrl+Z/Y, per-chat stack, depth 50).

Dependencies: W1.4, W1.7, W2.1, W3.3.


### Feature W3.6 — Artifacts System

**Vision groups: F (Artifacts & Side Panel).**

AI-generated named artifacts (code, docs, config files) with type inference. Side panel artifact list (right of chat, click to view). Version history per artifact (v1, v2, v3...). Diff view between any two versions (DiffPlex, side-by-side/unified). Version switching with branching on revert. Artifact viewer (syntax highlighting for code, rendered Markdown). "Save to Disk" and "Save to Wiki" buttons. Global Artifacts Browser (cross-chat listing, search, sort, filter).

Dependencies: W1.4, W1.7, W2.1.


### Feature W3.7 — Media Library

**Vision groups: G (Media Library & Multi-Modal).**

Browsable gallery grid of all media across chats (virtualized). Filtering by type, source chat, date range. Search by filename. Media actions (view/play, download, copy, open in system app, delete, navigate to source chat). AI image generation inline in chat. AI audio generation with inline player. Inline media rendering in messages (images clickable, audio mini player, video with MediaElement + LibVLCSharp fallback). "Save to Disk" and "View in Library" buttons.

Dependencies: W1.4, W1.7, W2.1, W3.4.


### Feature W3.8 — Tool Use & Agents

**Vision groups: H (Tool Use / Agent Capabilities).**

Web search tool (AI requests → execute via Google/Bing → results fed back). Terminal/shell execution (ALWAYS requires confirmation, risk-level detection). File generation (save dialog → AI generates → preview → confirm). File editing (file picker → AI suggests → DiffPlex preview → confirm). Tool auto-approval settings (global defaults + per-chat overrides). Deep Research (Plan → Search → Read → Synthesize → Report state machine, real-time progress, cancelable). Wiki search tool (AI queries local FTS5 index).

Dependencies: W1.4–W1.7, W2.1, W3.3.


### Feature W3.9 — Import/Export & Prompt Library

**Vision groups: I (Import & Export), J (Prompt Library).**

Export chat as Markdown (QuestPDF for PDF, JSON). Import from ChatGPT export JSON and Claude export JSON with duplicate detection. Prompt library browser with `{{variables}}` (`{{clipboard}}`, `{{selected_text}}`, `{{date}}`, `{{current_wiki_file}}`). Prompt management (create, edit, delete, folders, tags). Search/filter prompts.

Dependencies: W1.4, W1.7, W2.1.


### Feature W3.10 — Text Actions & Three-Tier System

**Vision groups: K (Text Actions & Three-Tier Interaction).**

Text Actions CRUD with three-dimensional configuration: **captureScope** (flags: `selection`, `focusedElement`, `surroundingContext`, `fullDocument`, `screenshot` — any combination), **systemPrompt + modelConfigId** (transform), and **applyMode** (single choice: `replaceSelection`, `insertAtCursor`, `replaceFocusedElement`, `appendToFocusedElement`, `prependToFocusedElement`, `clipboardOnly`, `showOnly`). 10 built-in defaults spanning all capture/apply combinations. Textbox toolbar with all controls including Text Actions dropdown with preview popup.

Tier 1 — Global hotkey text actions with graduated UIA capture pipeline (TextPattern → ValuePattern → TreeWalker → DocumentRange → screenshot with Win32 PrintWindow/BitBlt), three-phase flow (Capture → Result Popup → Apply per applyMode), clipboard restoration on fallback. Tier 2 — Command Bar (Alt+Space, Spotlight-style overlay, inline state + popped-out floating mini-window, elevation to Studio). Tier 3 — Studio Chat (full workspace). Tier 1/2 elevation to permanent Studio thread. Orthogonal elevation actions (Open in Studio, Save to Wiki) available regardless of apply mode.

Dependencies: W1.4–W1.7, W2.1, W2.2, W3.1.


### Feature W3.11 — Chat Organization, Search & Data Lifecycle

**Vision groups: L (Chat Organization & Search), O (Data Model & Lifecycle), U (Soft-Delete Trash).**

Sidebar chat list (sorted, grouped by date, pinned section). Chat favoriting (star/unstar, filter). Full-text chat search (SQLite FTS5, snippets, highlights, click to open). Sidebar filtering (permanent vs. Timeline transient). Chat tags/labels with autocomplete. Pin chats. Chat folders/collections. Chat archiving. Bulk operations (multi-select delete, archive, export, tag). Right-click context menu. Chat sorting options. Chat color labels. Timeline tab (chronological transient action feed). Soft-delete on chat deletion (30-day Trash). Trash view with Restore and Delete Permanently. 30-day auto-purge background task. Restore from Trash (preserves folder/tags/pinned). Permanent delete with garbage collection cascade. Empty Trash. Transient thread lifecycle (Tier 1/2 → IsTransient=true). 7-day auto-cleanup with exception rules (favorited, tagged, pinned, archived, has user replies). Garbage collection policy for media/artifacts. Database compaction (VACUUM).

Dependencies: W1.4, W1.7, W2.1.


### Feature W3.12 — Model Comparison

**Vision groups: M (Model Comparison).**

Model comparison mode (send same prompt to 2-4 Personas). Comparison setup (Persona selector, horizontal/vertical layout, broadcast mode toggle). Comparison results (simultaneous streaming, per-panel response time/tokens/cost). Accept comparison result (append to permanent ChatThread, others as branches or discarded). "Accept All as Branches" option.

Dependencies: W1.4–W1.7, W2.1, W3.1.


### Feature W3.13 — Personal Wiki / Second Brain

**Vision groups: N (Personal Wiki / Second Brain).**

Wiki directory configuration (FileSystemWatcher + polling fallback). Wiki indexing (Markdig AST walker, headings/cross-links/word count extraction, FTS5). Wiki search (dedicated scope, results with headings and snippets). Wiki Browser (three-region split: file tree, Markdown viewer, info panel with Related Sections/Backlinks/File Info). Write to Wiki pipeline ("Discuss then confirm" model: target selection → AI generates → Preview Panel → Save/Refine/Append/Cancel, Diff Viewer for updates). Automatic wiki versioning (pre-modification snapshots, max 30/file, 50MB cap). **Optional git version control for wiki** (initialize git repo from onboarding wizard Step 3 or Settings → Wiki; auto-commit on file change with 30-second debounce via file system watcher; optional GitHub connection with personal access token for auto-push; git is additive — N6 snapshots remain the primary versioning mechanism). @ mentions for wiki files (type @ → quick-search → inject content). AI wiki access restrictions (no delete/rename, write only via N5 pipeline). Append-only mode (dated heading, diff shows append only). AI cross-linking (tiered pipeline: index.md → candidates → full content → draft links → user review). Auto-generated index.md. AI memory (`_memory.md`, memory-aware toggle, token cap). Find & replace across wiki with preview (regex support, snapshots for undo).

Dependencies: W1.4, W1.7, W2.1.


### Feature W3.14 — Windows OS Integration

**Vision groups: P (Windows OS Integration).**

Session restore (P7: reopen all chats/tabs from previous session on launch, respects A6 setting). System tray refinements (full context menu actions including Open Studio, New Chat, Command Bar, Recent Chats, Settings, Exit; minimize-to-tray behavior; generation progress indicator overlay). WebSocket server refinements (configurable port beyond default, operational health check). Per-monitor DPI refinements (overlay positioning accounts for DPI scaling when moved between monitors, ensuring Tier 1/2 overlays render at correct size and position). HWND validation for [Apply] button state (grayed out when source window closed — builds on HWND capture already implemented in W3.10).

**Note:** HWND capture, spatial anchoring, clipboard format preservation (P2/P3/P4), and the graduated UIA capture pipeline (P9) are built by W3.10 as part of the Tier 1 three-phase flow. W3.14 adds session restore and refines the Windows integration infrastructure established in W2.2 and W3.10.

Dependencies: W1.4, W1.7, W2.1, W2.2, W3.10.


### Feature W3.15 — Language & RTL Support

**Vision groups: Q (Language & RTL).**

English LTR (default language, all UI labels, Hunspell English dictionary). Hebrew RTL detection (Unicode range U+0590-U+05FF, >30% threshold → RTL on message container). Mixed LTR/RTL rendering (WPF FlowDocument BiDi, per-segment direction, code blocks always LTR). Textbox input direction auto-detection.

Dependencies: W2.1, W3.3, W3.4.


### Feature W3.16 — Backup & Recovery

**Vision groups: R (Backup & Recovery).**

Google Cloud Storage backup (zip SQLite DB + wiki + artifacts, upload via GCS SDK, DPAPI-encrypted credentials). Local folder backup alternative. Backup schedule (daily, weekly, manual). Manual "Backup Now" button with progress. Restore from backup (browse list, download, replace, restart, confirmation dialog with warning).

Dependencies: W1.4, W1.6, W2.1.


### Feature W3.17 — Usage & Pricing Dashboard

**Vision groups: S (Usage & Pricing Dashboard).**

Usage overview screen (summary cards: total tokens, total cost, active models). Time range filters (Today, This Week, This Month, Custom Range, All Time). Usage charts (LiveCharts2: line chart tokens/time, bar chart cost/time, pie charts by provider/model). Per-chat breakdown table (sortable, click to open chat). Budget alerts (monthly spending limit, 80% warning toast, option to block API calls at 100%). AI feedback summary (aggregated thumbs-up/down per Persona/Model, approval percentages, trend chart, rankings).

Dependencies: W1.4, W1.7, W2.1.


### Feature W3.18 — Wave 3 Testing

**Feature-level unit tests across all Wave 3 features. Service integration tests (`ChatThreadService`, `LLMProviderService`, `IWikiService`, `IToolOrchestrator`) with real SQLite and mocked external APIs. Streaming & rendering tests (progressive Markdown, `StreamChunk` normalization across all 4 providers, code block fence detection mid-stream).**

Dependencies: W1.8, W3.1–W3.17.


---

## Wave 4: Cross-Cutting — 5 Features

Features that span across all vertical slices. Optimization, hardening, and polish.

### Feature W4.1 — Search & Performance Optimization

**SQLite FTS5 tuning (porter stemmer, unicode61, content type weighting, ranking). Database performance (index tuning on hot paths, connection pooling, write serialization audit, VACUUM scheduling). UI virtualization (`VirtualizingStackPanel` with Recycling for all large lists, image thumbnail caching, lazy loading, R2R compilation, splash screen). Memory management (`IDisposable` on all ViewModels, Messenger cleanup, DataTemplate leak prevention, long-session profiling).**

Dependencies: all Wave 3 features.


### Feature W4.2 — Accessibility & Internationalization

**Keyboard navigation audit (Tab order, access keys, focus indicators). High contrast mode support (Windows High Contrast theme, override WPF styles). Screen reader labels (AutomationProperties on all interactive elements, live regions for streaming/errors). UI string externalization to `.resx` files. Hebrew UI localization (all strings, RTL layout for all screens, Settings → Language toggle). Date/time/number localization per locale.**

Dependencies: all Wave 3 features.


### Feature W4.3 — E2E Testing & Visual Regression

**E2E test project with WinAppDriver or FlaUI for WPF automation. Tests: full onboarding, send message + streaming response, branching workflow, Write to Wiki pipeline, Tier 1 hotkey flow, model comparison flow, import/export flow. CI integration with headless test runner. Visual regression testing (screenshot-based comparison for all 8 screens in dark/light themes, CI step flags visual diffs).**

Dependencies: all Wave 3 features, W1.8.


### Feature W4.4 — Security Hardening & DevOps

**Security audit (DPAPI usage, AES-GCM implementation, WebSocket auth, SQL injection surface, file path traversal, command injection). Data integrity checks (PRAGMA integrity_check on startup, wiki index consistency verification, corrupted state recovery). Crash reporting (unhandled exception handler, stack trace logging, graceful shutdown with draft save). Performance monitoring (startup time, API latency histograms, memory tracking, optional diagnostics dashboard). CI/CD pipeline hardening (multi-stage: build → unit → integration → E2E → code sign → MSIX → deploy, release notes generation).**

Dependencies: all Wave 3 features, W1.1, W2.3.


### Feature W4.5 — Final Polish: Empty States, Error States & Edge Cases

**Empty state audit (every screen and panel follows pattern from edge-cases.md: descriptive message + actionable button, no blank screens). Error state audit (every error path shows specific actionable message, no generic "Something went wrong"). Edge case coverage (all global and per-feature-group edge cases from edge-cases.md not already covered: first-time user, network errors, permission errors, data limits, and all A-U feature group edge cases).**

Dependencies: all Wave 3 features.


---

## Feature Count Summary

| Wave | Description | Features |
|------|-------------|----------|
| Wave 1 | Foundation — Infrastructure, data model, abstractions | 9 (3 built) |
| Wave 2 | Skeleton — App shell, navigation, theming, Windows infra | 3 |
| Wave 3 | Vertical Slices — All user-facing features (A-U) | 18 |
| Wave 4 | Cross-Cutting — Perf, a11y, i18n, E2E, security, polish | 5 |
| **Total** | | **35** |

---

## Dependency Flow Diagram

```
Wave 1 (Foundation) — 9 Features
    │
    ├── W1.1 Solution Scaffold & CI/CD ✅
    ├── W1.2 DI Container ✅
    ├── W1.3 Logging ✅
    ├── W1.3b Diagnostics & Debug Logging (V)
    ├── W1.4 Data Layer (all entities, DbContext, repos)
    ├── W1.5 LLM Provider Layer (ILLMProvider × 4, ITokenizer)
    ├── W1.6 Platform Services (STT, Backup, Search, Tools, Import, Renderers, Theme, Update, Encryption)
    ├── W1.7 Core Services (ILLMProviderService, ChatThreadService, WikiService)
    └── W1.8 Testing Infrastructure
          │
          ▼
Wave 2 (Skeleton) — 3 Features
    │
    ├── W2.1 App Shell, All 8 Screens & Theming
    ├── W2.2 Windows OS Infrastructure
    └── W2.3 MSIX Packaging & CI/CD Hardening
          │
          ▼
Wave 3 (Vertical Slices) — 18 Features
    │
    ├── W3.1 Model Configs, API Keys & Personas (B)
    ├── W3.2 Settings & Configuration (A + A8)
    ├── W3.3 Studio Chat: Messaging, Rendering & Modes (C-core + E)
    ├── W3.4 Studio Chat: Input, Media & Workspace (C-input/tabs)
    ├── W3.5 Message Branching & Manipulation (D)
    ├── W3.6 Artifacts System (F)
    ├── W3.7 Media Library (G)
    ├── W3.8 Tool Use & Agents (H)
    ├── W3.9 Import/Export & Prompt Library (I + J)
    ├── W3.10 Text Actions & Three-Tier System (K)
    ├── W3.11 Chat Organization, Search & Data Lifecycle (L + O + U)
    ├── W3.12 Model Comparison (M)
    ├── W3.13 Personal Wiki / Second Brain (N)
    ├── W3.14 Windows OS Integration (P)
    ├── W3.15 Language & RTL Support (Q)
    ├── W3.16 Backup & Recovery (R)
    ├── W3.17 Usage & Pricing Dashboard (S)
    └── W3.18 Wave 3 Testing
          │
          ▼
Wave 4 (Cross-Cutting) — 5 Features
    │
    ├── W4.1 Search & Performance Optimization
    ├── W4.2 Accessibility & Internationalization
    ├── W4.3 E2E Testing & Visual Regression
    ├── W4.4 Security Hardening & DevOps
    └── W4.5 Final Polish
```

---

---

*Acceptance criteria are derived by the FD Architect from vision docs. DO NOT implement from this file alone — use [`vision/feature-inventory.md`](vision/feature-inventory.md) and individual feature specs in [`vision/features/`](vision/features/) for detailed behavioral requirements.*
