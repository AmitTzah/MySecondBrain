# Roadmap — MySecondBrain Wave Map

MySecondBrain is a native Windows desktop application — a unified, provider-agnostic AI chat hub that replaces all LLM chat platforms, paired with a personal wiki for turning conversations into lasting knowledge. Features are organized in four waves: Foundation (infrastructure), Skeleton (app shell), Vertical Slices (end-to-end user-facing features), and Cross-Cutting (polish and hardening).

Source documents:
- Vision: [`vision/vision-summary.md`](vision/vision-summary.md), [`vision/feature-inventory.md`](vision/feature-inventory.md)
- Planning: [`planning/architecture.md`](planning/architecture.md), [`planning/abstractions.md`](planning/abstractions.md), [`planning/data-model.md`](planning/data-model.md), [`planning/tech-stack.md`](planning/tech-stack.md), [`planning/platform-notes.md`](planning/platform-notes.md)
- Tech sourcing: [`tech-sourcing.md`](tech-sourcing.md)

---

## Wave 1: Foundation — 4 Features

Core infrastructure that everything else depends on. Built first, extended by all subsequent waves.

### Feature 1 — Solution Scaffold & CI/CD ✅

.NET 8.0 WPF solution with 7 projects (Core, Data, Services, UI, Package, Tests.Unit, Tests.Integration), 15 OSS NuGet packages, MSIX packaging placeholder, and GitHub Actions CI/CD skeleton.

Dependencies: none.
Status: Built.

### Feature 2 — Dependency Injection Container ✅

All service, repository, and ViewModel registrations via `Microsoft.Extensions.DependencyInjection`. 76+ registrations, 42+ C# interfaces, 13 entity stubs, 8 repository stubs, all provider stubs, 11 ViewModels, and 7 content block renderers.

Dependencies: 1.
Status: Built.

### Feature 3 — Logging Infrastructure ✅

Serilog with rolling file sink to `%LOCALAPPDATA%\MySecondBrain\logs\`, JSON structured output, and thread/machine enrichment.

Dependencies: 2.
Status: Built.

### Feature 4 — Data Layer: All Entities, DbContext & Repositories ✅

EF Core `AppDbContext` with SQLite, all 13 entity classes, FTS5 virtual tables, 8 repository implementations, repository interfaces, initial database migration, and migration strategy.

Dependencies: 2.
Status: Built.

---

## Wave 2: Skeleton — 2 Features

App shell, navigation, all empty screens, theming, and Windows platform infrastructure. Every screen exists as a navigable shell with placeholder content.

### Feature 5 — App Shell, Navigation & Theming

MainWindow three-region shell (sidebar + content + right panel), tabbed navigation with drag-drop reorder, screen routing, and sidebar shell with chat list/Trash/Timeline tabs. All 8 empty screen shells: Studio Chat, Onboarding Wizard, Model Comparison, Settings, Wiki Browser, Usage Dashboard, Media Library, and Global Artifacts Browser. Dark/Light theme system with `DynamicResource` instant toggle, 3 chat visual themes (Classic, Compact, Bubble), and font settings (family, size 10–24px, weight).

Dependencies: 2, 4.
Vision groups: none (infrastructure — enables all screens).

### Feature 6 — Windows OS Platform Infrastructure

System tray integration (`NotifyIcon` with context menu, minimize-to-tray, generation indicator), global hotkey registration (`RegisterHotKey` with `WH_KEYBOARD_LL` fallback for Tier 1 and Tier 2), per-monitor DPI awareness (`PerMonitorV2`), local WebSocket server (Kestrel on 127.0.0.1 with token auth), auto-update framework (`AutoUpdater.NET`), and MSIX packaging with code signing and `.appinstaller` generation.

Dependencies: 2, 5.
Vision groups: P (P1, P2, P5, P6, P8 — core platform infrastructure).

---

## Wave 3: Vertical Slices — 10 Features

End-to-end features spanning database → service → UI. Each feature adds new code to the Wave 1–2 foundation. Ordered by dependency chain.

### Feature 7 — Model Configurations, API Keys & Personas

API key management (add, edit, delete, test, DPAPI encryption, 8 provider types: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAI-Compatible). Model Configuration CRUD (temperature, max tokens, thinking toggle, pricing, context overflow strategy — SlidingWindow/HardStop/AutoSummarize). Auto-fetch available models from provider APIs. Persona CRUD (system prompt, default model config, chat mode). Persona selection per chat with recently-used ordering. Local open-source model support via OpenAI-Compatible provider. Default profile selection (A2). Speech-to-text provider configuration (A10 — OpenAI Whisper API, local Whisper, Windows Speech).

Dependencies: 4, 5.
Vision groups: B, A2, A10.

### Feature 8 — Settings, Onboarding & Diagnostics

Full Settings screen with 16 categories (Providers, Profiles, Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Diagnostics, Maintenance). Appearance settings (chat themes, font). Dark/Light mode toggle. Notification and streaming settings (sound, disable streaming, per-chat mute defaults). Startup behavior (launch on Windows startup, session restore). Auto-update settings. Database maintenance (VACUUM with before/after size display). Onboarding Wizard — five-step guided first-launch setup (Welcome → API Keys → Persona → Wiki Directory → Hotkeys → Finish), each step skippable, re-launchable from Settings. Onboarding Finish screen's "Import from ChatGPT or Claude" button is present but non-functional until Feature 18 builds the import infrastructure. Diagnostics & Debug Logging — global log level selector (Information/Debug/Verbose) and 8 per-category toggles (LLM API Calls, Tier 1 Hotkey Pipeline, Tier 2 Command Bar, Database, Wiki & File System, WebSocket, Startup & Shutdown, System Integration), "Open Logs Folder" and "Clear Logs" buttons, API key redaction via Serilog `IDestructuringPolicy`.

Dependencies: 4, 5, 6, 7. (Soft dependency on 18 for onboarding import button functionality.)
Vision groups: A (A1, A3–A9, A11), A8, V.

### Feature 9 — Studio Chat — Core Workspace

Conversation view with VirtualizingStackPanel. Full Markdown rendering (Markdig → WPF FlowDocument — headings, bold, italic, code blocks with AvalonEdit syntax highlighting for 100+ languages, lists, links, tables, blockquotes). Streaming token-by-token progressive rendering with auto-scroll management. Message actions (Send/Stop with partial response preservation, Regenerate, Continue Generation). Copy MD and Copy Rich per message. Auto-generated chat titling via AI. Error handling with specific messages and Retry. Scroll-to-bottom floating button. Clear conversation with undo. Chat header three-dot menu (⋯). Message selection mode with bulk actions. Offline/network status indicator. Close confirmation during active generation. Pin window / Always on top. Dark/Light quick toggle. Font size quick adjust. Chat header full layout (Persona name, context bar, cost, source banner, font size, dark mode, pin, ⋯ menu). Incognito/temporary chat toggle. Locked chats (AES-256-GCM, password-protected). Chat summarization via AI. Message favoriting (★). Cross-tab completion alert (green dot). Right panel layout (Artifacts panel top + Chat Nav bottom, resizable divider). [Apply] button shell in chat header (grayed-out validation deferred to Feature 18).

**Language & RTL (Q):** English LTR (default, all UI labels). Hebrew RTL auto-detection (Unicode range U+0590–U+05FF, >30% threshold → RTL on message container). Mixed LTR/RTL rendering per segment (WPF FlowDocument BiDi, code blocks always LTR).

Chat Modes (E): Standard chat mode. Text Completion mode. Thinking toggle with reasoning display. Mute notifications per chat. Dynamic system message editing from Persona header popover.

Dependencies: 4, 5, 7.
Vision groups: C (core messaging/rendering), E, Q.

### Feature 10 — Studio Chat — Input, Media & Prompts

Drag & drop files/media into textbox. Paste image from clipboard (Ctrl+V) with thumbnail preview. **Attach File button (📎) in textbox toolbar** — opens standard Windows file picker, multi-select, displays selected files as attachment cards below textbox. **Model-aware file type compatibility** — checks active Model Configuration's provider/model capabilities; warns if attached file types are unsupported (e.g., "⚠️ [Model] does not support [file type]. Attached as metadata only."). Audio input via microphone (NAudio → configured STT provider). Camera capture (webcam photo for vision models). Spell check with Hunspell (red squiggly, right-click suggestions, custom dictionary). Multiple chat tabs (drag-drop reorder, close, reopen). Token usage and context display (real-time local tokenizer, color gradient bar). Keyboard shortcuts (Ctrl+N/W/Tab/F/S). Resizable panels (sidebar, right panel). Textbox toolbar with Persona selector, thinking toggle, mute toggle, tools toggle, and prompt library access. Auto-save drafts every 5 seconds with crash recovery. Textbox input direction auto-detection based on first strong directional character typed (Hebrew → RTL, English → LTR).

Prompt Library (J): Saved reusable prompts with dynamic variables ({{clipboard}}, {{selected_text}}, {{date}}, {{current_wiki_file}}). Organize with tags and folders. Select to insert from textbox toolbar.

Dependencies: 4, 5, 7, 9.
Vision groups: C (input/media), J.

### Feature 11 — Message Branching & Chat Organization

Message Branching (D): Edit any past message (Edit in Place or Edit as Branch). **Quick Branch (↳ Branch) button** on each assistant message — creates a branch instantly without opening the editor, separate from the edit-then-branch flow. Delete any past message (branch data preserved). Branch navigation with indicator ("2/3") and cycle arrows. Chat tree visualization (nodes = messages, edges = parentMessageId). Quote from chat (selected text → quoted block in input). Chat navigation bar (collapsible scrollable message list, click to jump). Duplicate/Fork chat from any message point. Message feedback (thumbs-up/down). Undo/Redo message edits (Ctrl+Z/Y, per-chat stack, depth 50).

Chat Organization (L): Sidebar chat list with sort options, date grouping, pinned section. Chat favoriting with filter. Full-text chat search via SQLite FTS5 (snippets, highlights, click to open). Sidebar filtering (permanent vs. Timeline transient). Chat tags/labels with autocomplete. Pin chats. Chat folders/collections. Chat archiving. Bulk operations. Right-click context menu. Chat color labels. Timeline tab (chronological Tier 1/2 transient action feed).

Dependencies: 4, 5, 9.
Vision groups: D, L.

### Feature 12 — Data Lifecycle & Soft-Delete Trash

Data Lifecycle (O): Unified ChatThread model (Tier 1/2/3 all create identical ChatThread + Message data). IsTransient flagging. Chat elevation (sending reply in transient thread in Studio flips to permanent; elevation triggers on send action, even if API call fails). 7-day auto-cleanup of transient threads with exceptions (favorited, tagged, pinned, archived, has user replies or artifacts — auto-elevated). Garbage collection policy. Database compaction (VACUUM).

Soft-Delete Trash (U): Soft-delete on chat deletion (30-day Trash). Trash view with Restore and Delete Permanently. 30-day auto-purge. Restore preserves folder, tags, pinned status. Permanent delete with garbage collection cascade. Empty Trash with confirmation.

Dependencies: 4, 5, 11.
Vision groups: O, U.

### Feature 13 — Artifacts & Media Library

**Artifacts (F):** AI-generated named artifacts (code, docs, config files) with type inferred from language/extension. Side panel artifact list in right panel (click to view content). Version history per artifact (v1, v2, v3…). Diff view between any two versions (DiffPlex, side-by-side or unified). Version switching with branching on revert. Artifact viewer (syntax highlighting for code, rendered view for Markdown). "Save to Disk" and "Save to Wiki" buttons. Global Artifacts Browser screen (cross-chat listing with search, sort, and filter).

**Media Library (G):** Browsable gallery grid of all media across chats (virtualized). Filtering by type (image, audio, video), source chat, and date range. Search by filename. Media actions (view/play, download, copy to clipboard, open in system app, delete, navigate to source chat). AI image generation inline in chat. AI audio generation with inline player. Inline media rendering in messages (images clickable for full resolution, audio mini player with play/pause/seek, video with embedded player). "Save to Disk" and "View in Library" buttons on all media.

Dependencies: 4, 5, 9.
Vision groups: F, G.

### Feature 14 — Tool Use & Agent Capabilities

Web search tool (AI requests web search via Google Custom Search or Bing API → app executes → results fed back to AI). Terminal/shell execution (ALWAYS requires explicit user confirmation, command displayed with risk-level detection, stdout/stderr captured and returned). File generation (save dialog → AI generates → preview → user confirms). File editing (file picker → AI suggests changes → DiffPlex preview → user confirms). Wiki directory excluded from all file operations. Tool auto-approval settings (global defaults in Settings + per-chat overrides in textbox toolbar). Deep Research — autonomous multi-step research (Plan → Search → Read → Synthesize → Report state machine, real-time progress display, cancelable, cited output). Wiki search tool (AI queries local FTS5 wiki index, incorporates results into responses).

Dependencies: 4, 5, 9.
Vision groups: H.

### Feature 15 — Text Actions & Three-Tier System

Text Actions CRUD with three-dimensional configuration: capture scope (any combination of selection, focusedElement, surroundingContext, fullDocument, screenshot), system prompt + model config (the transform), and apply mode (replaceSelection, insertAtCursor, replaceFocusedElement, appendToFocusedElement, prependToFocusedElement, clipboardOnly, showOnly). Ten built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt, Continue Writing, Improve Flow, Summarize Page, Explain Screen. Textbox toolbar with Text Actions dropdown and preview popup.

**Tier 1 — Global Hotkey Text Actions:** Three-phase flow per TextAction configuration. Capture Phase — graduated UIA pipeline per captureScope flags (TextPattern → ValuePattern → TreeWalker → DocumentRange → screenshot via PrintWindow/BitBlt). Result Phase — editable AI output with Accept/Discard/Open in Studio/Save to Wiki/Retry and Additional Instructions field. Apply Phase — per applyMode injection (HWND, UIA TextPattern/ValuePattern, clipboard) with layered fallbacks and confirmation toast. Orthogonal elevation actions (Open in Studio, Save to Wiki) available regardless of apply mode.

**Tier 2 — Command Bar (Alt+Space):** Spotlight-style centered overlay with inline state (input field + Q&A display + Pop-out/Close/Copy controls) and popped-out state (floating resizable mini-window with Open in Studio, Pin, Minimize, Close). Elevation to Studio creates permanent ChatThread. Dismissal saves as transient thread.

**Tier 3 — Studio Chat:** Full workspace (built in Feature 9). All three tiers share the same ChatThreadService and data model. Elevation from Tier 1/2 flips IsTransient to false.

Dependencies: 4, 5, 6, 7, 9.
Vision groups: K, P9.

### Feature 16 — Personal Wiki / Second Brain

Wiki directory configuration (user selects directory of .md files, FileSystemWatcher with 500ms debounce and polling fallback monitors external changes). Wiki indexing (Markdig AST walker extracts headings, cross-links, word count, plain text content; stored in SQLite wiki index tables with FTS5 for full-text search). Wiki search (dedicated scope with results showing filenames, headings, snippets; click opens in Wiki Browser). Wiki Browser screen — three-region split: file tree (collapsible directory tree), Markdown viewer (rendered content with "Open in External Editor" button), and info panel (Related Sections tab + Backlinks tab + File Info tab with word count, reading time, heading count). **💬 Discuss with AI button** in Wiki Browser — creates a new chat (or opens existing) with the current file's full content pre-loaded as context, enabling wiki→chat→Write to Wiki loop.

**Write to Wiki Pipeline:** "Discuss then confirm" model. Trigger from toolbar or context menu. Pipeline: target file selection → AI generates polished .md with cross-links → Preview Panel (editable, with Append-Only toggle for dated-heading appends) → Save/Refine in Chat/Append Only/Cancel. For updates to existing files: mandatory Diff Viewer before save.

**Versioning & Git:** Automatic pre-modification snapshots (max 30 per file, 50MB total cap, recoverable from Wiki Browser). Optional git version control — initialize git repo from Onboarding Wizard or Settings, auto-commit on file change with 30-second debounce, optional GitHub remote push with DPAPI-encrypted personal access token. Snapshots and git coexist (snapshots for instant undo, git for cross-session history).

**Knowledge Features:** @ mentions for wiki files (type @ in textbox → quick-search dropdown → inject full content or summarized excerpt if >8K tokens). AI wiki access restrictions (no deletions, no renaming, write only via N5 pipeline). AI cross-linking — tiered pipeline: AI reads auto-generated index.md → selects candidates → requests full content → generates draft with suggested links → user reviews and accepts. Backlinks suggested after save. Auto-generated index.md at wiki root (directory tree, all headings with links, cross-links, recently modified, orphan pages). AI memory (`_memory.md` wiki file, "Update Memory" button triggers Write to Wiki pipeline, memory-aware toggle per chat injects full file into context with optional token cap). Find and replace across all wiki files with preview of changes and regex support (snapshots provide undo).

Dependencies: 4, 5.
Vision groups: N.

---

## Wave 4: Cross-Cutting — 2 Features

Features that span across vertical slices. Smaller independent features combined with optimization, hardening, and polish.

### Feature 17 — Model Comparison, Backup & Recovery

**Model Comparison (M):** Send same prompt to 2–4 Personas simultaneously. Side-by-side comparison with independent streaming panels (horizontal or vertical layout). Each panel shows Persona name, response time, token count, and cost. Broadcast mode toggle (typing in one input sends to all). Accept result appends to permanent ChatThread; others saved as branches or discarded. "Accept All as Branches" option.

**Backup & Recovery (R):** Full backup of SQLite database, wiki .md files, and artifacts. Google Cloud Storage backup (zip → upload via GCS SDK, DPAPI-encrypted credentials). Local folder backup alternative (zero-dependency). Backup schedule (daily, weekly, manual; default: daily). Manual "Backup Now" button with progress. Restore from backup (browse list, download, replace, restart with confirmation dialog and warning).

Dependencies: 4, 5, 7, 9.
Vision groups: M, R.

### Feature 18 — Data Portability, Analytics, Localization & Hardening

**Import & Export (I):** Export chat as Markdown (QuestPDF for PDF, JSON). Import from ChatGPT export JSON and Claude export JSON with duplicate detection. Imported chats created as new ChatThreads.

**Usage & Pricing Dashboard (S):** Usage overview screen with summary cards (total tokens, total cost, active models). Time range filters (Today, This Week, This Month, Custom Range, All Time). Usage charts (LiveCharts2 — line chart tokens/time, bar chart cost/time, pie charts by provider and model). Per-chat breakdown table (sortable, click to open chat). Budget alerts (monthly spending limit, 80% warning toast, option to block API calls at 100%). AI feedback summary (aggregated thumbs-up/down per Persona and Model, approval percentages, trend chart, rankings).

**Platform Refinements (P):** Session restore (reopen all chats and tabs from previous session on launch, respects startup setting). HWND validation for Tier 1 [Apply] button state (grayed out when source window closed). Clipboard format preservation refinements (HTML/RTF format-aware capture and restoration for Tier 1).

**Testing, Performance & Security:** E2E testing with FlaUI for WPF automation (full onboarding, send message + streaming, branching workflow, Write to Wiki pipeline, Tier 1 hotkey flow, model comparison, import/export). Visual regression testing (screenshot-based comparison for all 8 screens in dark and light themes). Performance optimization (SQLite FTS5 tuning, `VirtualizingStackPanel` with Recycling for all large lists, image thumbnail caching, R2R compilation, memory management, `IDisposable` audit). Accessibility (keyboard navigation audit, high contrast mode support, screen reader labels via `AutomationProperties`, live regions for streaming and errors). Security hardening (DPAPI and AES-GCM audit, WebSocket auth review, SQL injection surface check, file path traversal prevention, crash reporting with graceful shutdown). Empty state and error state coverage for all screens and panels (descriptive messages with actionable buttons, no blank screens or generic errors).

Dependencies: all Wave 3 features.
Vision groups: I, S, P (P3, P4, P7 — platform refinements).

---

## Feature Count Summary

| Wave | Description | Features |
|------|-------------|----------|
| Wave 1 | Foundation — Infrastructure, data model, abstractions | 4 (built) |
| Wave 2 | Skeleton — App shell, navigation, theming, Windows infrastructure | 2 |
| Wave 3 | Vertical Slices — All user-facing features (DB → service → UI) | 10 |
| Wave 4 | Cross-Cutting — Smaller features, optimization, hardening, polish | 2 |
| **Total** | | **18** |

---

## Dependency Flow Diagram

```
Wave 1: Foundation (4 built)
  F1 ✅  Solution Scaffold & CI/CD
  F2 ✅  DI Container
  F3 ✅  Logging Infrastructure
  F4 ✅  Data Layer (entities, DbContext, repos)
    │
    ▼
Wave 2: Skeleton (2)
  F5 ─── App Shell, Navigation & Theming
  F6 ─── Windows OS Platform Infrastructure
    │
    ├──────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
    ▼          ▼          ▼          ▼          ▼          ▼          ▼
Wave 3: Vertical Slices (10)
  F7 ─── Model Configs, API Keys & Personas (B + A2 + A10)
  │
  ├── F8 ─── Settings, Onboarding & Diagnostics (A + A8 + V) [soft dep on 18]
  │
  ├── F9 ─── Studio Chat — Core Workspace (C + E)
  │     │
  │     ├── F10 ── Studio Chat — Input, Media & Prompts (C + J)
  │     │
  │     ├── F11 ── Message Branching & Chat Organization (D + L)
  │     │     │
  │     │     └── F12 ── Data Lifecycle & Soft-Delete Trash (O + U)
  │     │
  │     ├── F13 ── Artifacts & Media Library (F + G)
  │     │
  │     └── F14 ── Tool Use & Agent Capabilities (H)
  │
  ├── F15 ── Text Actions & Three-Tier System (K + P9)
  │
  └── F16 ── Personal Wiki / Second Brain (N)
    │
    ├──────────┬──────────┐
    ▼          ▼          ▼
Wave 4: Cross-Cutting (2)
  F17 ── Model Comparison, Backup & Recovery (M + R)
  F18 ── Data Portability, Analytics, Localization & Hardening (I + S + Q + P refinements + testing/polish)
```

---

*Feature behavioral details are specified in [`vision/features/`](vision/features/). Screen layouts are in [`vision/screens/`](vision/screens/). Data entity schemas are in [`vision/data/`](vision/data/). Architecture and abstractions are in [`planning/`](planning/).*
