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
E2E: Not applicable — Wave 1 foundation infrastructure, verified via CI build pipeline.
Status: Built.

### Feature 2 — Dependency Injection Container ✅

All service, repository, and ViewModel registrations via `Microsoft.Extensions.DependencyInjection`. 76+ registrations, 42+ C# interfaces, 13 entity stubs, 8 repository stubs, all provider stubs, 11 ViewModels, and 7 content block renderers.

Dependencies: 1.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via 8 unit tests.
Status: Built.

### Feature 3 — Logging Infrastructure ✅

Serilog with rolling file sink to `%LOCALAPPDATA%\MySecondBrain\logs\`, JSON structured output, and thread/machine enrichment.

Dependencies: 2.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via unit tests.
Status: Built.

### Feature 4 — Data Layer: All Entities, DbContext & Repositories ✅

EF Core `AppDbContext` with SQLite, all 13 entity classes, FTS5 virtual tables, 8 repository implementations, repository interfaces, initial database migration, and migration strategy.

Dependencies: 2.
E2E: Not applicable — Wave 1 foundation infrastructure, verified via 104 data layer unit tests.
Status: Built.

---

## Wave 2: Skeleton — 2 Features

App shell, navigation, all empty screens, theming, and Windows platform infrastructure. Every screen exists as a navigable shell with placeholder content.

### Feature 5 — App Shell, Navigation & Theming

MainWindow three-region shell (sidebar + content + right panel), tabbed navigation with drag-drop reorder, screen routing, and sidebar shell with chat list/Trash/Timeline tabs. All 8 empty screen shells: Studio Chat, Onboarding Wizard, Model Comparison, Settings, Wiki Browser, Usage Dashboard, Media Library, and Global Artifacts Browser. Dark/Light theme system with `DynamicResource` instant toggle, 3 chat visual themes (Classic, Compact, Bubble), and font settings (family, size 10–24px, weight).

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (shell layout: sidebar, tab bar, right panel).

Dependencies: 2, 4.
E2E: Launch app via FlaUI, verify MainWindow title is "MySecondBrain" and three-region shell renders (NavChats sidebar radio button, ChatView content area, RightPanelSplitter with 📄 Artifacts header visible, SidebarSplitter visible). Click each navigation button (NavChats, NavWiki, NavMedia, NavArtifacts, NavUsage, NavSettings) and verify the corresponding view UserControl appears and the right panel hides on non-chat screens. Click ThemeToggleBtn to switch between Dark/Light themes and verify the icon toggles with round-trip consistency. Expand ChatThemeCombo and verify Classic, Compact, and Bubble options are present and individually selectable. Click IncreaseFontBtn and DecreaseFontBtn and verify FontSizeDisplay updates; rapid clicks do not crash the app. Verify all 7 ContentBlockRenderer implementations have correct ascending priority values (MarkdownText=100 through ToolCall=700).
Vision groups: none (infrastructure — enables all screens).

### Feature 6 — Windows OS Platform Infrastructure

System tray integration (`NotifyIcon` with context menu, minimize-to-tray, generation indicator), global hotkey registration (`RegisterHotKey` with `WH_KEYBOARD_LL` fallback for Tier 1 and Tier 2), per-monitor DPI awareness (`PerMonitorV2`), local WebSocket server (Kestrel on 127.0.0.1 with token auth), auto-update framework (`AutoUpdater.NET`), and MSIX packaging with code signing and `.appinstaller` generation.

Dependencies: 2, 5.
E2E: Verify ISystemTrayService resolves as WinFormsSystemTrayService from DI with an 8-item context menu in order (New Chat, Open Studio, Command Bar, separator, Recent Chats, Settings, separator, Exit) and all 5 menu click events fire correctly. Verify UpdateRecentChats populates the Recent Chats submenu with clickable items and shows a disabled placeholder when empty. Verify SetGenerationIndicator swaps the NotifyIcon between normal and generating icons, and restores correctly. Verify IGlobalHotkeyService resolves with 6 default hotkeys registered (CommandBar, Rewrite, Summarize, Explain, Translate, ContinueWriting), system hotkey conflict detection works (Win+D, Win+L, Alt+F4, Alt+Tab detected; Ctrl+Alt+Z not), and register/unregister lifecycle succeeds. Verify PerMonitorV2 is configured in the .csproj and the launched window has positive bounds >200px. Verify ILocalWebSocketServer resolves as KestrelWebSocketServer with a 64-character hex auth token, RegenerateAuthToken produces a new valid token, and the health endpoint at http://127.0.0.1:{port}/health returns 200 OK. Verify IUpdateChecker resolves with both AutoUpdaterDotNet and MsixAppInstallerUpdater implementations, CurrentVersion is non-zero, and UpdateFeedUrl is a valid HTTPS URL. Verify all 4 platform services (ISystemTrayService, IGlobalHotkeyService, ILocalWebSocketServer, IUpdateChecker) are registered as Singletons in DI and resolve concretely.
Vision groups: P (P1, P2, P5, P6, P8 — core platform infrastructure).

### Feature 5b — Visual Design System: Colors, Typography & Spacing

Define a comprehensive design token system — color palette (brand, semantic, neutral), font ramp (10–24px with line heights), spacing scale (4px base unit), corner radius tokens, shadow definitions, and transition durations. Produces an updated WPF `ResourceDictionary` with all tokens consumable via `DynamicResource` by every screen built in Wave 3. Covers both Dark and Light themes. Includes a "Design System" page in Settings → Appearance showing all tokens with live preview.

HTML mock reference: (cross-cutting — applies to all 8 vision screens; tokens designed to match the visual language implied by [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) color scheme).

Dependencies: 5.
E2E: Not applicable — design tokens verified via visual regression tests planned within Feature 19.
Vision groups: cross-cutting (affects all screens).

---

## Wave 3: Vertical Slices — 11 Features

End-to-end features spanning database → service → UI. Each feature adds new code to the Wave 1–2 foundation. Ordered by dependency chain.

### Feature 7 — Model Configurations, API Keys & Personas

API key management (add, edit, delete, test, DPAPI encryption, 8 provider types: OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAI-Compatible). Model Configuration CRUD (temperature, max tokens, thinking toggle, pricing, context overflow strategy — SlidingWindow/HardStop/AutoSummarize). Auto-fetch available models from provider APIs. Persona CRUD (system prompt, default model config, chat mode). Persona selection per chat with recently-used ordering. Local open-source model support via OpenAI-Compatible provider. Default profile selection (A2). Speech-to-text provider configuration (A10 — OpenAI Whisper API, local Whisper, Windows Speech).

Dependencies: 4, 5.
E2E: Navigate to Settings → Providers, verify SettingsView loads with default Providers category. Click Add API Key, verify form title "Add API Key" appears, select OpenAI from provider dropdown, enter display name and API key, click Test Key and verify the button is re-enabled after test completes. Save the key and verify it appears in the saved keys list (masked display with copy button). Navigate to Profiles, click New Model Configuration, set display name, select API key from provider combo, set model identifier (gpt-4o) in editable combo, set temperature slider to 0.7, configure context overflow to SlidingWindow, and save. Verify saved config appears in list. Click New Persona, set name and system prompt, select the saved model configuration, choose Standard chat mode, and save. Verify saved persona appears in list. Persona picker dialog (Ctrl+N) verified via UIA element discovery (PersonaPickerDialog, PersonaPickerSearchBox, PersonaPickerList, PersonaPickerSelectBtn) — keyboard shortcut unreliable in automated FlaUI. All created entities self-deleted within each test via 🗑️ buttons + MessageBox confirmation.
Vision groups: B, A2, A10.

### Feature 8 — Settings, Onboarding & Diagnostics

Full Settings screen with 16 categories (Providers, Profiles, Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Diagnostics, Maintenance). Appearance settings (chat themes, font). Dark/Light mode toggle. Notification and streaming settings (sound, disable streaming, per-chat mute defaults). Startup behavior (launch on Windows startup, session restore). Auto-update settings. Database maintenance (VACUUM with before/after size display). Onboarding Wizard — five-step guided first-launch setup (Welcome → API Keys → Persona → Wiki Directory → Hotkeys → Finish), each step skippable, re-launchable from Settings. Onboarding Finish screen's "Import from ChatGPT or Claude" button is present but non-functional until Feature 18 builds the import infrastructure. Diagnostics & Debug Logging — global log level selector (Information/Debug/Verbose) and 8 per-category toggles (LLM API Calls, Tier 1 Hotkey Pipeline, Tier 2 Command Bar, Database, Wiki & File System, WebSocket, Startup & Shutdown, System Integration), "Open Logs Folder" and "Clear Logs" buttons, API key redaction via Serilog `IDestructuringPolicy`.

HTML mock references: [`vision/screens/settings.html`](vision/screens/settings.html), [`vision/screens/onboarding-wizard.html`](vision/screens/onboarding-wizard.html).

Dependencies: 4, 5, 6, 7. (Soft dependency on 18 for onboarding import button functionality.)
E2E: Verify SettingsView loads with correct title. Verify all 16 settings category sidebar items are present (Providers, Profiles, Appearance, Wiki, Backup, Text Actions, Hotkeys, Tools, Language, Notifications, Startup, Updates, Pricing, Security, Diagnostics, Maintenance) and each shows correct header when selected. In Appearance section, verify Dark and Light theme RadioButtons exist and toggle between them. Verify "🔄 Re-run Onboarding Wizard" hyperlink exists in Settings sidebar. In Diagnostics section, verify log level ComboBox has Information/Debug/Verbose options, change log level between values with round-trip restore, toggle LLM API Calls category off then on, and verify "Open Logs Folder" and "Clear Logs" buttons exist in Log File Management section. Onboarding Wizard 5-step flow tested end-to-end with fresh test database (Feature 9): Welcome→API Keys→Persona→Wiki Directory→Hotkeys→Finish, each step skippable, re-launchable from Settings, wizard not shown after completion.
Vision groups: A (A1, A3–A9, A11), A8, V.

### Feature 9 — E2E Test Suite Rewrite & Authoring Guide

Rewrite all 70+ E2E tests from scratch: switch from `IClassFixture<E2eFixture>` (app restarts per class) to `ICollectionFixture<E2eFixture>` (one launch, all tests). Replace fragile class-level `Dispose()` cleanup with self-cleaning tests that delete created data via the app's own 🗑️ delete buttons within the same `[Fact]` body. Introduce `MSB_DB_PATH` environment variable for a separate test database (`e2e-test.db`) — fresh DB every run, deleted on teardown. Write [`agent-workspace/external-docs/e2e-authoring-guide.md`](../../agent-workspace/external-docs/e2e-authoring-guide.md) encoding all conventions: fixture pattern, test database, self-cleaning tests, no-dead-time rules, selector strategy, helper conventions, onboarding wizard testing, and MessageBox handling. Extract duplicated helpers (`FindById`, `FindByName`, `FindByNameContains`, `NavigateToSettings`, `SelectSettingsCategory`, `ConfirmMessageBox`, `SetPasswordInput`) into a shared `E2eTestBase` abstract class.

Dependencies: 4, 5, 6, 7, 8. (Tests all Wave 2-3 features F5-F8.)
E2E: Run `dotnet test tests/e2e/MySecondBrain.Tests.E2E --configuration Debug` and verify exit code 0 with all tests passing. After suite completion, verify the test database (`e2e-test.db`) contains zero user-created entities (no stale API keys, model configs, personas, or chat threads). Verify the test suite runs in a single app launch — only one `[FIXTURE] Launching app` log line and one `[FIXTURE] Cleaning up` log line in the output. Verify the E2E authoring guide exists at the expected path and covers all required sections (fixture pattern, test database, self-cleaning tests, no-dead-time, selector strategy, helper conventions, onboarding wizard testing, MessageBox handling).
Vision groups: none (cross-cutting testing infrastructure — enables quality for all future features).

### Feature 10 — Studio Chat — Core Workspace

Conversation view with VirtualizingStackPanel. Full Markdown rendering (Markdig → WPF FlowDocument — headings, bold, italic, code blocks with AvalonEdit syntax highlighting for 100+ languages, lists, links, tables, blockquotes). Streaming token-by-token progressive rendering with auto-scroll management. Message actions (Send/Stop with partial response preservation, Regenerate, Continue Generation). Copy MD and Copy Rich per message. Auto-generated chat titling via AI. Error handling with specific messages and Retry. Scroll-to-bottom floating button. Clear conversation with undo. Chat header three-dot menu (⋯). Message selection mode with bulk actions. Offline/network status indicator. Close confirmation during active generation. Pin window / Always on top. Dark/Light quick toggle. Font size quick adjust. Chat header full layout (Persona name, context bar, cost, source banner, font size, dark mode, pin, ⋯ menu). Incognito/temporary chat toggle. Locked chats (AES-256-GCM, password-protected). Chat summarization via AI. Message favoriting (★). Cross-tab completion alert (green dot). Right panel layout (Artifacts panel top + Chat Nav bottom, resizable divider). [Apply] button shell in chat header (grayed-out validation deferred to Feature 19).

**Language & RTL (Q):** English LTR (default, all UI labels). Hebrew RTL auto-detection (Unicode range U+0590–U+05FF, >30% threshold → RTL on message container). Mixed LTR/RTL rendering per segment (WPF FlowDocument BiDi, code blocks always LTR).

Chat Modes (E): Standard chat mode. Text Completion mode. Thinking toggle with reasoning display. Mute notifications per chat. Dynamic system message editing from Persona header popover.

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (conversation area, thinking blocks, message actions, right panel artifacts + chat nav).

Dependencies: 4, 5, 7.
E2E: Create a new chat with a persona that has a configured API key. Type a message requesting code generation and press Enter. Verify streaming response begins token-by-token with progressive Markdown rendering including a fenced code block with syntax highlighting and a Copy button on hover. Verify Stop button is visible during generation. After completion, verify message footer shows generation time. Click Copy MD on the assistant message, paste into Notepad, verify raw Markdown was copied. Click Copy Rich and verify formatted content on clipboard. Click Regenerate and verify a new response replaces the old one with the original preserved as a branch. Verify chat header shows auto-generated title, persona name, and context window bar. Open the ⋯ menu and verify Clear Conversation, Export Chat, Duplicate Chat, Chat Tree, and Edit System Message options are present.
Vision groups: C (core messaging/rendering), E, Q.

### Feature 11 — Studio Chat — Input, Media & Prompts

Drag & drop files/media into textbox. Paste image from clipboard (Ctrl+V) with thumbnail preview. **Attach File button (📎) in textbox toolbar** — opens standard Windows file picker, multi-select, displays selected files as attachment cards below textbox. **Model-aware file type compatibility** — checks active Model Configuration's provider/model capabilities; warns if attached file types are unsupported (e.g., "⚠️ [Model] does not support [file type]. Attached as metadata only."). Audio input via microphone (NAudio → configured STT provider). Camera capture (webcam photo for vision models). Spell check with Hunspell (red squiggly, right-click suggestions, custom dictionary). Multiple chat tabs (drag-drop reorder, close, reopen). Token usage and context display (real-time local tokenizer, color gradient bar). Keyboard shortcuts (Ctrl+N/W/Tab/F/S). Resizable panels (sidebar, right panel). Textbox toolbar with Persona selector, thinking toggle, mute toggle, tools toggle, and prompt library access. Auto-save drafts every 5 seconds with crash recovery. Textbox input direction auto-detection based on first strong directional character typed (Hebrew → RTL, English → LTR).

Prompt Library (J): Saved reusable prompts with dynamic variables ({{clipboard}}, {{selected_text}}, {{date}}, {{current_wiki_file}}). Organize with tags and folders. Select to insert from textbox toolbar.

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (input area, toolbar, attachment row, textbox + send, token count).

Dependencies: 4, 5, 7, 10.
E2E: In an active Studio Chat, click the 📎 Attach File button in the textbox toolbar, select a .txt file and a .png image from the file picker, and verify both appear as attachment cards below the textbox with filenames and remove buttons. Verify model-aware compatibility warnings appear on unsupported file types. Paste an image from clipboard via Ctrl+V and verify it appears as a thumbnail in the attachment row. Click the microphone button, verify recording indicator pulses red, click again to stop, and verify transcribed text appears in the textbox. Open the Prompt Library, select a saved template, and verify it inserts into the textbox with variables resolved. Verify the real-time token counter in the chat header updates as text is typed, showing a colored bar transitioning from green to yellow to red as the context limit approaches.
Vision groups: C (input/media), J.

### Feature 12 — Message Branching & Chat Organization

Message Branching (D): Edit any past message (Edit in Place or Edit as Branch). **Quick Branch (↳ Branch) button** on each assistant message — creates a branch instantly without opening the editor, separate from the edit-then-branch flow. Delete any past message (branch data preserved). Branch navigation with indicator ("2/3") and cycle arrows. Chat tree visualization (nodes = messages, edges = parentMessageId). Quote from chat (selected text → quoted block in input). Chat navigation bar (collapsible scrollable message list, click to jump). Duplicate/Fork chat from any message point. Message feedback (thumbs-up/down). Undo/Redo message edits (Ctrl+Z/Y, per-chat stack, depth 50).

Chat Organization (L): Sidebar chat list with sort options, date grouping, pinned section. Chat favoriting with filter. Full-text chat search via SQLite FTS5 (snippets, highlights, click to open). Sidebar filtering (permanent vs. Timeline transient). Chat tags/labels with autocomplete. Pin chats. Chat folders/collections. Chat archiving. Bulk operations. Right-click context menu. Chat color labels. Timeline tab (chronological Tier 1/2 transient action feed).

HTML mock reference: [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html) (sidebar chat list with tags, folders, sort; branching UI with indicators; message action buttons).

Dependencies: 4, 5, 10.
E2E: In a chat with multiple messages, click the edit icon on a past user message, modify text, select Edit as Branch, and verify branch indicator "2/2" appears. Click the ↳ Branch button on an assistant message and verify a new branch is created instantly. Click branch navigation arrows to cycle between versions and verify subsequent messages re-render. Open the Chat Tree visualization, verify nodes represent messages, click a node to navigate to that branch point. Select text in a past message, click Quote, and verify the selected text is inserted as a Markdown blockquote in the textbox. In the sidebar, favorite a chat via star icon, assign tags, pin a chat to the top, and verify all persist. Press Ctrl+Shift+F to open global search, type a query, verify FTS5 results appear with highlighted snippets grouped by chat, click a result to navigate to the matching message.
Vision groups: D, L.

### Feature 13 — Data Lifecycle & Soft-Delete Trash

Data Lifecycle (O): Unified ChatThread model (Tier 1/2/3 all create identical ChatThread + Message data). IsTransient flagging. Chat elevation (sending reply in transient thread in Studio flips to permanent; elevation triggers on send action, even if API call fails). 7-day auto-cleanup of transient threads with exceptions (favorited, tagged, pinned, archived, has user replies or artifacts — auto-elevated). Garbage collection policy. Database compaction (VACUUM).

Soft-Delete Trash (U): Soft-delete on chat deletion (30-day Trash). Trash view with Restore and Delete Permanently. 30-day auto-purge. Restore preserves folder, tags, pinned status. Permanent delete with garbage collection cascade. Empty Trash with confirmation.

Dependencies: 4, 5, 12.
E2E: Right-click a chat in the sidebar and select Delete. Verify confirmation dialog warns about 30-day retention. Click Move to Trash and verify toast confirms the action. Navigate to 🗑️ Trash tab, verify the deleted chat appears with deletion date, Restore button, and Delete Permanently button. Click Restore and verify the chat reappears in the main list with its original folder, tags, and pinned status preserved. Delete another chat, navigate to Trash, click Delete Permanently, verify confirmation dialog warns about permanent data loss, confirm, and verify the chat is gone. Click Empty Trash, verify confirmation with item count, confirm, and verify all items are permanently removed.
Vision groups: O, U.

### Feature 14 — Artifacts & Media Library

**Artifacts (F):** AI-generated named artifacts (code, docs, config files) with type inferred from language/extension. Side panel artifact list in right panel (click to view content). Version history per artifact (v1, v2, v3…). Diff view between any two versions (DiffPlex, side-by-side or unified). Version switching with branching on revert. Artifact viewer (syntax highlighting for code, rendered view for Markdown). "Save to Disk" and "Save to Wiki" buttons. Global Artifacts Browser screen (cross-chat listing with search, sort, and filter).

**Media Library (G):** Browsable gallery grid of all media across chats (virtualized). Filtering by type (image, audio, video), source chat, and date range. Search by filename. Media actions (view/play, download, copy to clipboard, open in system app, delete, navigate to source chat). AI image generation inline in chat. AI audio generation with inline player. Inline media rendering in messages (images clickable for full resolution, audio mini player with play/pause/seek, video with embedded player). "Save to Disk" and "View in Library" buttons on all media.

HTML mock references: [`vision/screens/global-artifacts-browser.html`](vision/screens/global-artifacts-browser.html), [`vision/screens/media-library.html`](vision/screens/media-library.html).

Dependencies: 4, 5, 10.
E2E: Trigger an AI generation that produces an artifact (e.g., "Create a Python Flask app in an artifact"). Verify the artifact appears in the right panel under 📄 Artifacts with its name and type. Click the artifact to view its content with syntax highlighting. Request a change and verify version v2 is created. Open version history, select v1 and v2, click Compare, and verify DiffPlex side-by-side diff shows red/green changes. Click Save to Disk and verify the file is written. Navigate to Global Artifacts Browser via sidebar, verify all artifacts across chats are listed with name, type, parent chat, date, and version count, and filter by type. Navigate to Media Library screen, verify a gallery grid of all media items with filtering by type, click an image to view full resolution, and verify View in Library navigates to the source chat.
Vision groups: F, G.

### Feature 15 — Tool Use & Agent Capabilities

Web search tool (AI requests web search via Google Custom Search or Bing API → app executes → results fed back to AI). Terminal/shell execution (ALWAYS requires explicit user confirmation, command displayed with risk-level detection, stdout/stderr captured and returned). File generation (save dialog → AI generates → preview → user confirms). File editing (file picker → AI suggests changes → DiffPlex preview → user confirms). Wiki directory excluded from all file operations. Tool auto-approval settings (global defaults in Settings + per-chat overrides in textbox toolbar). Deep Research — autonomous multi-step research (Plan → Search → Read → Synthesize → Report state machine, real-time progress display, cancelable, cited output). Wiki search tool (AI queries local FTS5 wiki index, incorporates results into responses).

Dependencies: 4, 5, 10.
E2E: In Studio Chat with tools enabled, send a message requesting a web search. Verify the tool call appears as a styled system message showing the search query, results feed back to the AI, and a summarized response appears. Request file generation, verify a save dialog appears, the AI generates content, a DiffPlex preview shows the content, and confirming writes the file with a success toast. Request terminal execution, verify a confirmation dialog appears showing the exact command, working directory, and risk level. Click Deny and verify AI is notified execution was denied. Click Approve on a subsequent attempt and verify stdout is captured and displayed. Trigger Deep Research with a query, verify the Plan→Search→Read→Synthesize→Report state machine displays real-time progress with Searching, Reading N of M sources, and Synthesizing status messages, and the final report appears with clickable inline citations.
Vision groups: H.

### Feature 16 — Text Actions & Three-Tier System

Text Actions CRUD with three-dimensional configuration: capture scope (any combination of selection, focusedElement, surroundingContext, fullDocument, screenshot), system prompt + model config (the transform), and apply mode (replaceSelection, insertAtCursor, replaceFocusedElement, appendToFocusedElement, prependToFocusedElement, clipboardOnly, showOnly). Ten built-in defaults: Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt, Continue Writing, Improve Flow, Summarize Page, Explain Screen. Textbox toolbar with Text Actions dropdown and preview popup.

**Tier 1 — Global Hotkey Text Actions:** Three-phase flow per TextAction configuration. Capture Phase — graduated UIA pipeline per captureScope flags (TextPattern → ValuePattern → TreeWalker → DocumentRange → screenshot via PrintWindow/BitBlt). Result Phase — editable AI output with Accept/Discard/Open in Studio/Save to Wiki/Retry and Additional Instructions field. Apply Phase — per applyMode injection (HWND, UIA TextPattern/ValuePattern, clipboard) with layered fallbacks and confirmation toast. Orthogonal elevation actions (Open in Studio, Save to Wiki) available regardless of apply mode.

**Tier 2 — Command Bar (Alt+Space):** Spotlight-style centered overlay with inline state (input field + Q&A display + Pop-out/Close/Copy controls) and popped-out state (floating resizable mini-window with Open in Studio, Pin, Minimize, Close). Elevation to Studio creates permanent ChatThread. Dismissal saves as transient thread.

**Tier 3 — Studio Chat:** Full workspace (built in Feature 10). All three tiers share the same ChatThreadService and data model. Elevation from Tier 1/2 flips IsTransient to false.

HTML mock reference: (no dedicated screen mock — Tier 1/2 are overlay windows; Text Actions configuration lives in [`vision/screens/settings.html`](vision/screens/settings.html) categories ⚡ Text Actions and ⌨️ Hotkeys).

Dependencies: 4, 5, 6, 7, 10.
E2E: Press Alt+Space to invoke Tier 2 Command Bar. Verify a centered overlay appears with placeholder "Ask anything…". Type a question, press Enter, and verify the bar expands with streaming compact Markdown response. Click the Pop-out button and verify the bar detaches into a floating resizable mini-window with Open in Studio, Pin, Minimize, and Close controls. Click Open in Studio and verify the conversation becomes a permanent ChatThread. Press Alt+Q (Rewrite) in a text editor with text selected, verify the Thinking… pill overlay appears near cursor, then the result popup shows the Text Action name, source application, editable transformed text, and Accept/Discard/Open in Studio/Save to Wiki/Retry buttons. Click Accept and verify the result applies to the source editor per the apply mode with a confirmation toast. Navigate to Settings → Text Actions, verify 10 built-in defaults are listed. Create a custom Text Action with capture scope fullDocument+screenshot and apply mode clipboardOnly, assign a hotkey, and verify it appears in the list.
Vision groups: K, P9.

### Feature 17 — Personal Wiki / Second Brain

Wiki directory configuration (user selects directory of .md files, FileSystemWatcher with 500ms debounce and polling fallback monitors external changes). Wiki indexing (Markdig AST walker extracts headings, cross-links, word count, plain text content; stored in SQLite wiki index tables with FTS5 for full-text search). Wiki search (dedicated scope with results showing filenames, headings, snippets; click opens in Wiki Browser). Wiki Browser screen — three-region split: file tree (collapsible directory tree), Markdown viewer (rendered content with "Open in External Editor" button), and info panel (Related Sections tab + Backlinks tab + File Info tab with word count, reading time, heading count). **💬 Discuss with AI button** in Wiki Browser — creates a new chat (or opens existing) with the current file's full content pre-loaded as context, enabling wiki→chat→Write to Wiki loop.

**Write to Wiki Pipeline:** "Discuss then confirm" model. Trigger from toolbar or context menu. Pipeline: target file selection → AI generates polished .md with cross-links → Preview Panel (editable, with Append-Only toggle for dated-heading appends) → Save/Refine in Chat/Append Only/Cancel. For updates to existing files: mandatory Diff Viewer before save.

**Versioning & Git:** Automatic pre-modification snapshots (max 30 per file, 50MB total cap, recoverable from Wiki Browser). Optional git version control — initialize git repo from Onboarding Wizard or Settings, auto-commit on file change with 30-second debounce, optional GitHub remote push with DPAPI-encrypted personal access token. Snapshots and git coexist (snapshots for instant undo, git for cross-session history).

**Knowledge Features:** @ mentions for wiki files (type @ in textbox → quick-search dropdown → inject full content or summarized excerpt if >8K tokens). AI wiki access restrictions (no deletions, no renaming, write only via N5 pipeline). AI cross-linking — tiered pipeline: AI reads auto-generated index.md → selects candidates → requests full content → generates draft with suggested links → user reviews and accepts. Backlinks suggested after save. Auto-generated index.md at wiki root (directory tree, all headings with links, cross-links, recently modified, orphan pages). AI memory (`_memory.md` wiki file, "Update Memory" button triggers Write to Wiki pipeline, memory-aware toggle per chat injects full file into context with optional token cap). Find and replace across all wiki files with preview of changes and regex support (snapshots provide undo).

HTML mock reference: [`vision/screens/wiki-browser.html`](vision/screens/wiki-browser.html).

Dependencies: 4, 5.
E2E: In Settings → Wiki, select a wiki directory containing .md files and verify the path displays. Navigate to Wiki Browser via sidebar, verify the three-region split: left file tree, center Markdown viewer rendering the selected file with headings/links/code blocks, right info panel with Related Sections, Backlinks, and File Info tabs. Click a file in the tree and verify File Info tab shows word count, reading time, and heading count. Click Open in External Editor and verify the file opens in the system default .md editor. Click 💬 Discuss with AI and verify a new Studio chat opens with the file's content pre-loaded as context. In Studio, have a conversation, click Write to Wiki, choose Create new wiki file, enter a filename, and verify AI generates a polished .md summary with suggested cross-links highlighted. Review and edit in the Preview Panel, click Save to Wiki, and verify the file is written with a confirmation toast. For an update to an existing file, verify the mandatory Diff Viewer appears before Commit to Wiki is clickable.
Vision groups: N.

---

## Wave 4: Cross-Cutting + Polish — 4 Features

Features that span across vertical slices. Smaller independent features combined with optimization, hardening, polish, and motion design.

### Feature 18 — Model Comparison, Backup & Recovery

**Model Comparison (M):** Send same prompt to 2–4 Personas simultaneously. Side-by-side comparison with independent streaming panels (horizontal or vertical layout). Each panel shows Persona name, response time, token count, and cost. Broadcast mode toggle (typing in one input sends to all). Accept result appends to permanent ChatThread; others saved as branches or discarded. "Accept All as Branches" option.

**Backup & Recovery (R):** Full backup of SQLite database, wiki .md files, and artifacts. Google Cloud Storage backup (zip → upload via GCS SDK, DPAPI-encrypted credentials). Local folder backup alternative (zero-dependency). Backup schedule (daily, weekly, manual; default: daily). Manual "Backup Now" button with progress. Restore from backup (browse list, download, replace, restart with confirmation dialog and warning).

HTML mock reference: [`vision/screens/model-comparison.html`](vision/screens/model-comparison.html).

Dependencies: 4, 5, 7, 10.
E2E: In Studio Chat, click the ⚖ Compare button in the textbox toolbar. Select 2-3 Personas via checkboxes (verify Start Comparison is disabled until ≥2 selected), enter a prompt, choose horizontal layout, and click Start Comparison. Verify side-by-side panels stream responses independently with persona name, model name, and real-time metrics. Toggle 🔗 Broadcast mode on and verify per-panel inputs are replaced by a single centered broadcast input. Send a follow-up and verify it goes to all panels. Click Accept on one panel and verify the accepted conversation is appended to the originating chat with other conversations auto-saved as branches, confirmed by toast. Navigate to Settings → Backup, configure a local folder backup destination, click Backup Now, verify progress bar completes with a confirmation toast. Set backup schedule to Daily. Click Restore from Backup, verify available backups are listed with dates and sizes, select one, confirm the warning dialog, and verify restore completes.
Vision groups: M, R.

### Feature 19 — Data Portability, Analytics, Localization & Hardening

**Import & Export (I):** Export chat as Markdown (QuestPDF for PDF, JSON). Import from ChatGPT export JSON and Claude export JSON with duplicate detection. Imported chats created as new ChatThreads.

**Usage & Pricing Dashboard (S):** Usage overview screen with summary cards (total tokens, total cost, active models). Time range filters (Today, This Week, This Month, Custom Range, All Time). Usage charts (LiveCharts2 — line chart tokens/time, bar chart cost/time, pie charts by provider and model). Per-chat breakdown table (sortable, click to open chat). Budget alerts (monthly spending limit, 80% warning toast, option to block API calls at 100%). AI feedback summary (aggregated thumbs-up/down per Persona and Model, approval percentages, trend chart, rankings).

**Platform Refinements (P):** Session restore (reopen all chats and tabs from previous session on launch, respects startup setting). HWND validation for Tier 1 [Apply] button state (grayed out when source window closed). Clipboard format preservation refinements (HTML/RTF format-aware capture and restoration for Tier 1).

**Testing, Performance & Security:** E2E testing with FlaUI for WPF automation (full onboarding, send message + streaming, branching workflow, Write to Wiki pipeline, Tier 1 hotkey flow, model comparison, import/export). Visual regression testing (screenshot-based comparison for all 8 screens in dark and light themes). Performance optimization (SQLite FTS5 tuning, `VirtualizingStackPanel` with Recycling for all large lists, image thumbnail caching, R2R compilation, memory management, `IDisposable` audit). Accessibility (keyboard navigation audit, high contrast mode support, screen reader labels via `AutomationProperties`, live regions for streaming and errors). Security hardening (DPAPI and AES-GCM audit, WebSocket auth review, SQL injection surface check, file path traversal prevention, crash reporting with graceful shutdown). Empty state and error state coverage for all screens and panels (descriptive messages with actionable buttons, no blank screens or generic errors).

HTML mock references: [`vision/screens/usage-dashboard.html`](vision/screens/usage-dashboard.html), [`vision/screens/settings.html`](vision/screens/settings.html) (import/export, analytics sections).

Dependencies: all Wave 3 features.
E2E: In a chat with multiple messages, press Ctrl+S, select Markdown format, pick a save location, and verify the exported .md file contains all messages with roles, timestamps, and code blocks. Export as PDF and verify the .pdf renders with formatting preserved. Navigate to Settings → Import, select a ChatGPT export JSON file, verify the preview shows chat title, message count, and date range, click Import, and verify a new ChatThread appears in the sidebar with messages and timestamps preserved. Repeat with a Claude export JSON. Navigate to Usage Dashboard, verify summary cards display total tokens, total cost, most used model, and most used Persona. Select This Month filter, verify line chart (tokens/time), bar chart (cost/time), and pie charts (by provider, by model) update. Click a row in the per-chat breakdown table to open that chat. Verify AI Feedback Summary shows approval percentages per Persona and Model with trend charts. Close and relaunch the app, verify session restore reopens all previously open chats and tabs when enabled in Settings → Startup. Verify the Tier 1 [Apply] button is grayed out with "Source application is no longer available" when the source window has been closed.
Vision groups: I, S, P (P3, P4, P7 — platform refinements).

### Feature 20 — UI Polish: All Screens Visual Refinement Pass

Apply the Visual Design System (Feature 5b) consistently across all 8 screens. Implement proper spacing, typography, color application, empty state illustrations, and responsive panel behavior. Ensure every screen looks professional and polished — no placeholder gray-on-gray look. Addresses: card/panel elevation and borders, consistent header styling, focus indicators, scrollbar styling, and sidebar visual hierarchy. Includes visual regression test snapshots for all 8 screens in both Dark and Light themes.

HTML mock references: all 8 vision screens as visual targets — [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html), [`vision/screens/wiki-browser.html`](vision/screens/wiki-browser.html), [`vision/screens/media-library.html`](vision/screens/media-library.html), [`vision/screens/global-artifacts-browser.html`](vision/screens/global-artifacts-browser.html), [`vision/screens/usage-dashboard.html`](vision/screens/usage-dashboard.html), [`vision/screens/settings.html`](vision/screens/settings.html), [`vision/screens/onboarding-wizard.html`](vision/screens/onboarding-wizard.html), [`vision/screens/model-comparison.html`](vision/screens/model-comparison.html).

Dependencies: all Wave 3 features, 5b.
E2E: Not applicable — visual consistency verified via planned visual regression test snapshots for all 8 screens in Dark and Light themes.
Vision groups: cross-cutting (affects all screens).

### Feature 21 — UI Polish: Micro-interactions & Motion Design

Add subtle motion and interaction feedback throughout the app: hover transitions on buttons/list items (150ms color fade), smooth panel resize animations, tab open/close transitions, message appear/fade-in during streaming, scroll-to-bottom smooth behavior, toast notification slide-in/out, thinking block expand/collapse animation, sidebar collapse/expand transition, overlay fade-in for Tier 1 pill and Tier 2 command bar. All animations respect the Windows "Turn off all unnecessary animations" accessibility setting (`SystemParametersInfo` SPI_GETCLIENTAREAANIMATION).

HTML mock reference: (cross-cutting — tooltip toast behavior and hover states shown in [`vision/screens/studio-chat.html`](vision/screens/studio-chat.html); animation timing inspired by Windows 11 Fluent design language).

Dependencies: 5b, 20.
E2E: Not applicable — motion design verified via automated animation timing tests and visual inspection; all animations respect Windows accessibility setting.
Vision groups: cross-cutting (affects all screens).

---

## Feature Count Summary

| Wave | Description | Features |
|------|-------------|----------|
| Wave 1 | Foundation — Infrastructure, data model, abstractions | 4 (built) |
| Wave 2 | Skeleton — App shell, navigation, theming, Windows infrastructure, design system | 3 |
| Wave 3 | Vertical Slices — All user-facing features (DB → service → UI) | 11 |
| Wave 4 | Cross-Cutting + Polish — Smaller features, optimization, hardening, polish, motion | 4 |
| **Total** | | **22** |

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
Wave 2: Skeleton + Design System (3)
  F5 ─── App Shell, Navigation & Theming
  │
  ├── F5b ── Visual Design System: Colors, Typography & Spacing
  │
  F6 ─── Windows OS Platform Infrastructure
    │
    ├──────────┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
    ▼          ▼          ▼          ▼          ▼          ▼          ▼          ▼
Wave 3: Vertical Slices (11)
  F7 ─── Model Configs, API Keys & Personas (B + A2 + A10)
  │
  ├── F8 ─── Settings, Onboarding & Diagnostics (A + A8 + V) [soft dep on 19]
  │
  ├── F9 ─── E2E Test Suite Rewrite & Authoring Guide (tests F5-F8)
  │
  ├── F10 ── Studio Chat — Core Workspace (C + E)
  │     │
  │     ├── F11 ── Studio Chat — Input, Media & Prompts (C + J)
  │     │
  │     ├── F12 ── Message Branching & Chat Organization (D + L)
  │     │     │
  │     │     └── F13 ── Data Lifecycle & Soft-Delete Trash (O + U)
  │     │
  │     ├── F14 ── Artifacts & Media Library (F + G)
  │     │
  │     └── F15 ── Tool Use & Agent Capabilities (H)
  │
  ├── F16 ── Text Actions & Three-Tier System (K + P9)
  │
  └── F17 ── Personal Wiki / Second Brain (N)
    │
    ├──────────┬──────────┬──────────┐
    ▼          ▼          ▼          ▼
Wave 4: Cross-Cutting + Polish (4)
  F18 ── Model Comparison, Backup & Recovery (M + R)
  F19 ── Data Portability, Analytics, Localization & Hardening (I + S + Q + P refinements + testing)
  F20 ── UI Polish: All Screens Visual Refinement Pass
  F21 ── UI Polish: Micro-interactions & Motion Design
```

---

*Feature behavioral details are specified in [`vision/features/`](vision/features/). Screen layouts are in [`vision/screens/`](vision/screens/). Data entity schemas are in [`vision/data/`](vision/data/). Architecture and abstractions are in [`planning/`](planning/).*
