# Technology Sourcing Decisions

## Platform

**Target:** Native Windows desktop application (WPF .NET 8.0+), Windows 10 (22H2+) and Windows 11 exclusively.  
**Form Factor:** Desktop window with system tray minimization; two overlay windows (Tier 1 pill, Tier 2 Command Bar).  
**Distribution:** MSIX packaged installer with auto-update; code-signed for antivirus trust.  
**Architecture:** Local-first, single-user, SQLite database, BYO API keys, provider-agnostic LLM integration.

---

## Executive Summary: Build vs. Use

Every technology choice falls into one of three buckets. Here's the quick picture.

### 🟢 We Use (Open-Source Libraries & SDKs) — 15 components

These are solved problems. We integrate mature, well-maintained libraries rather than reinventing them.

| Building Block | What It Does | Why We Use It |
|---------------|-------------|---------------|
| **SQLite + EF Core** | All local data: chats, messages, settings, wiki index, search | Zero-config, in-process, FTS5 full-text search built in. The desktop database standard. |
| **Markdig** | Parse Markdown to AST; render to WPF FlowDocument | The .NET Markdown standard — fast, extensible, handles all syntax we need. |
| **OpenAI SDK** (NuGet) | OpenAI + DeepSeek + Mistral + any OpenAI-compatible endpoint | Official, vendor-maintained. Handles auth, streaming, retries, errors. One SDK covers 70% of providers. |
| **Anthropic SDK** (community) | Anthropic Claude API | Separate API structure from OpenAI. Community .NET SDK is well-maintained. |
| **Google Cloud SDK** | Gemini API + GCS backup | Official SDKs for both Gemini chat and Google Cloud Storage backup. |
| **SharpToken** | Real-time token counting as user types | C# port of OpenAI's tiktoken — accurate, offline, no API call needed. |
| **NAudio** | Microphone recording + audio playback | The .NET audio standard — mature, MIT license, handles WAV/MP3. |
| **DiffPlex** | Line-by-line diff for artifact versions + wiki updates | Standard .NET diff library, fast, MIT license. |
| **LiveCharts2** | Usage dashboard charts (line, bar, pie) | Modern WPF-native charts with animations, dark/light theming, MVVM binding. |
| **WeCantSpell.Hunspell** | Spell check with red squiggly underlines | Hunspell is the industry standard (Chrome, Firefox, LibreOffice). .NET port available. |
| **QuestPDF** | Export chat to PDF | Native .NET PDF generation, MIT license, no external binary needed. |
| **AutoUpdater.NET** | Check for updates, download, install, restart | Purpose-built for .NET desktop apps. Handles all the edge cases. |
| **LibGit2Sharp** | Git init, commit, push for wiki version control | In-process git — no git.exe needed. Used by GitHub Desktop. |
| **AForge.NET** | Webcam photo capture | Simple API, sufficient for still-image capture from webcam. |
| **Whisper.net** | Local, offline speech-to-text | .NET binding for Whisper.cpp. Privacy-preserving alternative to Whisper API. |

**Reference-only (not runtime dependencies):** Cherry Studio validates the multi-provider adapter pattern. Windows-MCP validates the UIA-first text injection approach. PowerToys validates the overlay window model.

---

### 🔵 We Build (Custom Code) — 12 components

These are what makes MySecondBrain *MySecondBrain*. They're either unique to our vision, deeply Windows-specific, or too simple to justify a library.

| Custom Build | What It Does | Why We Build It |
|-------------|-------------|-----------------|
| **Three-Tier Architecture** | Tier 1 pill overlay → Tier 2 Command Bar → Tier 3 Studio, all sharing one ChatThread data model | This IS our product. No library or framework implements this interaction model. Unique competitive moat. |
| **Message Branching Model** | Every message edit creates a version branch; navigate between branches; visual chat tree | The data model is unique — SQLite tables with recursive CTE queries. No off-the-shelf library for branching chat trees. |
| **Wiki Indexing Engine** | Scan .md files, extract headings/links/backlinks, auto-generate index.md, compute related sections | Markdig parses the files, but all the indexing logic, cross-link detection, and index.md generation is our secret sauce. |
| **HWND Capture & Text Injection** | Capture text from any Windows app, push AI result back into the original window | Inherently Windows-specific. UIA + WM_SETTEXT + clipboard fallback. Windows-MCP proves the pattern works but it's Python — we implement in C#. |
| **Global Keyboard Hooks** | Alt+Q/W/E/R anywhere in Windows triggers Tier 1; Alt+Space triggers Tier 2 | Thin P/Invoke wrapper (~5 functions). RegisterHotKey is the primary mechanism. Well-understood Windows pattern. |
| **Tool Use Orchestration** | AI requests web search → app executes → feeds results back. Same for terminal, file I/O, wiki search. | Standard function-calling protocol, but the tool execution loop (confirm → run → capture → feed back) and the deep research state machine are custom. |
| **LLM Provider Abstraction** | ILLMProvider interface + per-provider adapters (OpenAI, Anthropic, Google, OpenAICompatible) | Thin glue code. The SDKs handle the heavy lifting; our abstraction normalizes streaming chunks and error handling. Cherry Studio validates this pattern. |
| **Chat Import Parsers** | Parse ChatGPT export JSON and Claude export JSON into ChatThreads | Format-specific parsing with defensive error handling. ~200 lines each. Too niche for a library. |
| **Deep Research Pipeline** | AI plans → searches → reads → synthesizes → cited report with progress display | Custom state machine driven by tool-use conversation loop. No framework needed for this bounded workflow. |
| **Theming System** | Dark/light mode + 3 chat visual themes (Classic, Compact, Bubble) | WPF ResourceDictionary with DynamicResource references. Native WPF capability — no library needed. |
| **Markdown → WPF Renderer** | Convert Markdig AST to WPF FlowDocument with syntax-highlighted code blocks | The conversion logic is custom, but it uses Markdig (parse) + AvalonEdit highlighting engine (syntax colors). Progressive rendering during streaming is the hard part. |
| **Auto-Save Drafts** | Save textbox content every 5 seconds; recover after crash | Trivial timer + SQLite. Too simple for a library. |

---

### ⚪ Platform Provides (Built into .NET/WPF) — 7 components

These come free with the platform. No code to write beyond configuration.

| Platform Feature | What It Gives Us |
|-----------------|-----------------|
| **WPF itself** | The entire UI framework: windows, controls, data binding, styling, layout |
| **DPAPI** (System.Security.Cryptography.ProtectedData) | Encrypt API keys at rest, tied to Windows user account. One method call. |
| **SQLite FTS5** | Full-text search across all chat messages and wiki content. Just CREATE VIRTUAL TABLE. |
| **System.Windows.Clipboard** | Read/write clipboard with multi-format support (text, HTML, RTF). |
| **FileSystemWatcher** | Monitor wiki directory for external .md file changes. |
| **MediaElement** | Play video files inline in chat. Built-in WPF control. |
| **Kestrel** (ASP.NET Core) | Embedded local WebSocket server for Word Add-in integration. |
| **FlowDocument BiDi** | Bidirectional text rendering — Hebrew RTL detection and mixed LTR/RTL display. |
| **PerMonitorV2 DPI** | Crisp rendering at any monitor scaling. Just an app.manifest setting. |
| **System.Diagnostics.Process** | Execute terminal commands, capture stdout/stderr. One class. |

---

### Why This Split?

The rule of thumb: **build what makes us unique, use what's solved, lean on the platform.**

- **Unique = Build:** The three-tier interaction model, branching chat trees, wiki indexing pipeline, and HWND text injection are what differentiate MySecondBrain from every other AI chat tool. These are our competitive advantage — building them ourselves gives us maximum control and the exact UX we want.

- **Solved = Use:** Markdown parsing, diff computation, spell checking, audio recording, PDF generation, chart rendering, token counting — these are commodity problems with excellent open-source solutions. Using them lets us focus on what's unique.

- **Platform = Free:** DPAPI encryption, clipboard handling, file watching, video playback, DPI awareness, and bidirectional text come for free with WPF/.NET on Windows. We'd be foolish to replace them with third-party alternatives.

Every component above is analyzed in detail in the sections below, including alternatives considered, tradeoffs, and risk levels.

---

## Component-by-Component Analysis

### 1. UI Framework — WPF .NET Application Shell

- **Vision Requirement:** Native Windows desktop app with 8 screens, per-monitor DPI awareness, system tray, three-tier interaction model (global overlays + full workspace), dark/light theming, resizable panels, and bidirectional text rendering (Hebrew RTL).
- **Sourcing Recommendation:** Custom Build on WPF with open-source library augmentation.
- **Recommended Approach:** .NET 8.0 WPF with `Microsoft.Extensions.DependencyInjection` for DI, `CommunityToolkit.Mvvm` for MVVM pattern, Material Design In XAML or custom WPF resource dictionaries for theming, and `WPF-UI` (or similar) for modern Fluent Design styling. Core WPF handles per-monitor DPI natively.
- **Rationale:** WPF is the only first-class .NET desktop framework with deep Windows integration capabilities — global keyboard hooks via P/Invoke, HWND access, system tray via `System.Windows.Forms.NotifyIcon` interop, and per-monitor DPI awareness via `PerMonitorV2`. No other .NET UI framework (WinUI 3, MAUI, Avalonia) provides the same combination of maturity, Windows-specific API access, and ecosystem. Custom build is mandatory because the three-tier overlay model (Tier 1 pill popup, Tier 2 Command Bar, Tier 3 Studio) is unique and not captured by any off-the-shelf app shell.
- **Alternatives Considered:**
  - **WinUI 3:** Modern, but immature tooling, limited HWND interop, and poor system tray support. Rejected.
  - **Avalonia:** Cross-platform but adds abstraction overhead; Windows-specific features (global hooks, HWND injection) would require platform-specific code anyway. Rejected.
  - **Electron + React:** Web-based rendering cannot achieve per-monitor DPI or native HWND injection. Global keyboard hooks require native addons. Rejected per explicit out-of-scope "No web interface."
  - **MAUI:** Mobile-first, Windows desktop support is secondary. Rejected.
- **Tradeoffs:** Gain: Maximum Windows integration depth, mature tooling, native performance. Lose: Cross-platform portability (explicitly out of scope), slower UI iteration than web-based frameworks.
- **Risk Level:** Low. WPF is a 18+ year mature framework with extensive documentation and community.

---

### 2. Local Database — SQLite

- **Vision Requirement:** All application data (chat threads, messages, personas, model configs, settings, API keys metadata, wiki index, usage records, artifacts, media references, version snapshots) stored locally. Single-user, no sync.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** `Microsoft.Data.Sqlite` (ADOT.NET provider) with Entity Framework Core for object-relational mapping and migrations. Full-Text Search via SQLite FTS5 extension for chat message search and wiki content indexing.
- **Rationale:** SQLite is the de facto standard for local-first desktop applications. It requires zero configuration, runs in-process, and supports the full data model complexity — branching message trees, versioned artifacts, full-text search, and 13 interrelated entities. EF Core provides a mature migration pipeline for schema evolution across auto-updates. FTS5 is built-in and sufficient for the search scope (single-user, local). The vision explicitly calls for SQLite (A9 VACUUM, O6 compaction).
- **Alternatives Considered:**
  - **LiteDB:** Embedded NoSQL; simpler but lacks FTS, joins, and mature ORM support needed for the relational model. Rejected.
  - **SQL Server LocalDB:** Requires separate installation, heavier. Contradicts local-first simplicity. Rejected.
  - **Realm:** Mobile-first, .NET support is community-maintained. Rejected.
  - **Plain JSON files:** Cannot support full-text search, branching queries, or transactional integrity at scale. Rejected.
- **Tradeoffs:** Gain: Zero-install, in-process, full SQL with FTS, mature EF Core support. Lose: No built-in encryption (addressed via DPAPI at field level + AES-256-GCM for locked chats).
- **Risk Level:** Low. SQLite is battle-tested and specified in the vision.

---

### 3. LLM Provider HTTP Client

- **Vision Requirement:** Provider-agnostic API integration with OpenAI, Anthropic, Google (Gemini), DeepSeek, Mistral, and any OpenAI-compatible endpoint. Must support: chat completions API, text completions API (deprecated — E2 concern), streaming (SSE/token-by-token), thinking/reasoning tokens, tool-use/function calling, vision (image input), model list fetching, and API key validation.
- **Sourcing Recommendation:** Open-Source Library with custom abstraction layer. **Reference implementation:** Cherry Studio (multi-provider adapter pattern, SSE normalization, provider configuration UI).
- **Recommended Approach:** Adopt the **provider adapter pattern** validated by Cherry Studio: an `ILLMProvider` interface (`.ChatAsync()`, `.ChatStreamAsync()`, `.ListModelsAsync()`, `.ValidateKeyAsync()`) with per-provider adapter implementations. Normalize all provider SSE formats into a common `StreamChunk` DTO (content delta, tool calls, thinking tokens, finish reason, usage). Use vendor SDKs where available:
  - **OpenAI / OpenAI-compatible:** `OpenAI` NuGet package (official .NET SDK) — covers OpenAI, DeepSeek, Mistral, and any OpenAI-compatible endpoint.
  - **Anthropic:** `Anthropic.SDK` NuGet package (community, well-maintained) or direct HTTP to Anthropic's Messages API.
  - **Google Gemini:** `Google.Cloud.AIPlatform.V1` or `GenerativeLanguage` client library.
- **Rationale:** Cherry Studio validates the adapter pattern at scale — it's a production multi-provider desktop app with millions of users. The key architectural insight is that each provider's differences (auth, request format, SSE event structure, error codes) are isolated in adapter classes, while the UI and chat logic only see the common abstraction. The `StreamChunk` normalization pattern is essential for MSB's streaming Markdown rendering (C4), thinking display (E3), and real-time token counting (C11).
- **Alternatives Considered:**
  - **Fully Custom HTTP Client:** Maximum control but requires implementing authentication, streaming, error handling, retries, and model list fetching for every provider. Cherry Studio proves the adapter pattern is sufficient — no need to go lower-level.
  - **LangChain / Semantic Kernel:** Full agent frameworks that add orchestration overhead. The vision's tool use (H1-H7) is bounded — MSB needs basic tool-calling loop with function-calling protocol, not a full agent framework. Cherry Studio implements tool use directly via the function-calling protocol without a framework.
  - **LiteLLM (proxy):** Adds a network hop. Contradicts local-first, BYO-keys architecture. Cherry Studio also connects directly to providers.
  - **Cherry Studio code adoption:** Cherry Studio is TypeScript/Electron. Direct code copying to C# is not feasible, but the adapter architecture, SSE normalization patterns, and error handling strategies transfer directly.
- **Tradeoffs:** Gain: Adapter pattern is proven by Cherry Studio; vendor SDKs handle auth/streaming/errors; thin abstraction preserves flexibility. Lose: SDK dependency versions may lag API changes; must maintain adapters as provider APIs evolve (Cherry Studio faces the same maintenance burden).
- **Risk Level:** Low for OpenAI/OpenAI-compatible. Medium for Anthropic (community SDK dependency). Low for Google (official SDK). Overall risk reduced by Cherry Studio's validation of the adapter pattern.

---

### 4. Markdown & Code Rendering Engine

- **Vision Requirement:** Full Markdown rendering in chat messages (headings, bold, italic, code blocks with syntax highlighting, lists, links, tables, blockquotes, images inline) with progressive rendering during streaming. Also: Markdown rendering in Wiki Browser (N4), Markdown export (I1), and rendered artifact content (F6).
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** `Markdig` for Markdown parsing (fast, extensible, .NET-native). For WPF rendering, convert Markdig's AST to WPF `FlowDocument` elements with custom renderers for code blocks (syntax highlighting via `AvalonEdit` or `ICSharpCode.TextEditor` highlighting engine). For streaming, use Markdig's incremental parsing or re-parse on each chunk and diff-update the FlowDocument.
- **Rationale:** Markdig is the standard .NET Markdown parser — faster than CommonMark.NET, more feature-complete than MarkdownDeep. It supports all required Markdown features including tables, task lists, and custom extensions. Converting to FlowDocument enables native WPF text rendering with proper selection, copy/paste, and accessibility. Syntax highlighting via AvalonEdit's highlighting engine covers 100+ languages.
- **Alternatives Considered:**
  - **Markdown.Xaml:** Direct Markdown-to-FlowDocument converter. Simpler but less flexible for custom rendering and streaming. Rejected.
  - **WebView2 + marked/highlight.js:** Embed a Chromium control, render Markdown as HTML. Would handle streaming beautifully and support the full web ecosystem. However, WebView2 adds ~100MB to install size, has DPI/scaling quirks, and cannot support bidirectional text at the native WPF level. Rejected for primary rendering but may be useful for PDF preview.
  - **Custom parser:** Unnecessary given Markdig's maturity.
- **Tradeoffs:** Gain: Native WPF text rendering with proper accessibility, selection, and DPI scaling; Markdig is fast and extensible. Lose: Progressive streaming requires custom FlowDocument update logic (web-based rendering handles this more naturally).
- **Risk Level:** Medium. Progressive Markdown rendering during streaming is the hardest part — code blocks must detect opening/closing fences mid-stream. This is a known challenge across all native chat UIs.

---

### 5. Global Keyboard Hooks

- **Vision Requirement:** System-wide hotkey detection (Alt+Q/W/E/R for Tier 1 Text Actions, Alt+Space for Tier 2 Command Bar) regardless of focused application. Must detect hotkeys without stealing focus. Configurable assignments (K1, Settings). Conflict detection.
- **Sourcing Recommendation:** Custom Build (thin P/Invoke wrapper) with reference to open-source implementations.
- **Recommended Approach:** Windows `SetWindowsHookEx` with `WH_KEYBOARD_LL` (low-level keyboard hook) via P/Invoke. Register hotkeys via `RegisterHotKey` as primary mechanism (more reliable, less likely to trigger AV), with `WH_KEYBOARD_LL` as fallback for key combinations that `RegisterHotKey` cannot handle.
- **Reference Implementation:** Study how PowerToys Run and AutoHotkey implement global hotkeys. Both are mature, open-source Windows projects with robust keyboard hook handling. **Ref doc:** [`ref-powertoys-global-hotkeys.md`](external-docs/ref-powertoys-global-hotkeys.md)
- **Rationale:** This is a well-understood Windows API pattern. `RegisterHotKey` is the sanctioned Windows API that doesn't trigger antivirus (unlike low-level hooks). The P/Invoke surface is minimal (~5 functions). The complexity lies in conflict detection and user-configurable reassignment, not the hook mechanism itself.
- **Alternatives Considered:**
  - **Full keyboard hook library (e.g., NHotkey):** Adds dependency for trivial P/Invoke. The library may not be maintained. Rejected.
  - **UI Automation:** Cannot detect global hotkeys. Rejected.
- **Tradeoffs:** Gain: Minimal code, maximum control, no external dependency. Lose: Must handle AV false positive risk via code signing + preferring `RegisterHotKey`. Must test across Windows 10/11.
- **Risk Level:** Medium. ⚠️ Vision Flag #6: Antivirus false positives. Mitigated by code signing, `RegisterHotKey` preference, and documentation.

---

### 6. HWND Capture & Text Injection (Spatial Anchoring)

- **Vision Requirement:** Capture active window handle (HWND), source application name, and document title before showing Tier 1 overlay. Push AI-transformed text back into source window (Phase 3 Apply), replacing originally highlighted text. Fallback to clipboard + simulated Ctrl+V if injection fails.
- **Sourcing Recommendation:** Custom Build with P/Invoke. **Reference implementation:** Windows-MCP (UI Automation patterns, text injection, element discovery).
- **Recommended Approach:**
  - **Capture:** `GetForegroundWindow()` for HWND, `GetWindowText()` for title, `GetWindowThreadProcessId` + `Process.GetProcessById` for app name.
  - **Injection:** UI Automation `ValuePattern.SetValue()` as primary method (validated by Windows-MCP as the most reliable across UWP/WPF/Win32 apps). `SendMessage` with `WM_SETTEXT` or `EM_REPLACESEL` as fallback for classic Win32 controls. `SendInput` for Ctrl+V simulation as final fallback.
  - **Detection:** Use UI Automation (`System.Windows.Automation`) to probe whether the target control supports `ValuePattern` or `TextPattern`. Use `FlaUI` as a more ergonomic .NET wrapper around UIA.
  - **Prototyping strategy:** For rapid prototyping, integrate Windows-MCP as an MCP client via localhost stdio to accelerate Windows automation development before implementing native C# equivalents. This avoids early investment in P/Invoke and UIA interop while validating the approach.
- **Rationale:** This is inherently Windows-specific, deeply tied to Win32 API and UIA. The layered approach (UIA → WM_SETTEXT → clipboard) is validated by Windows-MCP's production deployment across 2M+ users. The key insight from Windows-MCP: UIA `ValuePattern` works broadly across modern Windows apps and should be the primary method, not the fallback.
- **Alternatives Considered:**
  - **Windows Input Simulator library:** Only handles SendInput; doesn't cover HWND text injection. Complementary but insufficient alone.
  - **FlaUI / UIAutomation wrapper:** Should be used as the UIA helper library. More ergonomic than raw `System.Windows.Automation`. Validated by the broader .NET UI automation community.
  - **Windows-MCP as runtime dependency:** Adding Python + uiautomation as a runtime dependency for a .NET WPF app adds significant overhead (~50MB+). Rejected for production, viable for prototyping. The architectural patterns transfer; code does not.
  - **Full MCP integration:** Running Windows-MCP as a sidecar process that MSB communicates with via MCP protocol. Adds process management complexity and IPC latency. Rejected for production but useful as a reference for the layered fallback strategy.
- **Tradeoffs:** Gain: Windows-MCP validates the UIA-first approach, reducing risk. The layered fallback strategy is proven. Lose: Still fragile — some applications don't expose UIA patterns. Clipboard fallback ensures graceful degradation.
- **Risk Level:** High → **Medium-High** (downgraded). Windows-MCP provides a validated reference for UIA-based text injection across diverse Windows applications, reducing the unknown-unknown risk. The fallback strategy remains essential.

---

### 7. Clipboard Format Preservation

- **Vision Requirement:** During Tier 1 capture, detect clipboard DataFormats (HTML, RTF, plain text). On Apply, return text in the richest available format. Support for Copy MD (raw Markdown) and Copy Rich (HTML/RTF) per message (C6).
- **Sourcing Recommendation:** Custom Build using .NET `System.Windows.Clipboard`. **Reference implementation:** Windows-MCP Clipboard tool (format-aware read/write patterns).
- **Recommended Approach:** WPF's `System.Windows.Clipboard` provides `GetDataObject()` and `GetFormats()` for format detection, and `SetDataObject()` for multi-format clipboard writing. For Markdown-to-HTML conversion (Copy Rich), use Markdig to render Markdown to HTML, then place both HTML and RTF on clipboard. Windows-MCP's clipboard tool validates the pattern of preserving multiple formats simultaneously.
- **Rationale:** .NET provides the complete clipboard API surface needed. The custom work is in format detection logic and Markdown-to-RTF conversion (for rich copy). No external library needed. Windows-MCP's clipboard tool serves as a reference for format-preserving clipboard operations.
- **Alternatives Considered:**
  - **Clipboard library (e.g., TextCopy):** Adds dependency for trivial functionality. Rejected.
- **Tradeoffs:** Gain: Zero dependencies, full control. Lose: Must implement Markdown-to-RTF conversion (Markdown → HTML is easy via Markdig; HTML → RTF requires custom conversion or a helper).
- **Risk Level:** Low. Clipboard API is stable and well-documented.

---

### 8. Local WebSocket Server

- **Vision Requirement:** Local WebSocket server on 127.0.0.1 for external integrations (Microsoft Word Add-in for creative writing persona). Token-based authentication. Configurable port. Starts with app, stops on exit.
- **Sourcing Recommendation:** Custom Build using ASP.NET Core Kestrel or `System.Net.WebSockets`.
- **Recommended Approach:** Use ASP.NET Core's Kestrel server in embedded mode (no IIS/HTTP.sys dependency). Configure as a minimal web host listening only on `127.0.0.1` with a configurable port. Implement a simple JSON-based protocol over WebSocket with token authentication (token generated on first run, displayed in Settings). Use middleware pipeline for auth, routing to a single WebSocket handler.
- **Rationale:** Kestrel is mature, well-audited, and designed for embedded scenarios. It handles WebSocket upgrade, connection management, and graceful shutdown natively. The protocol surface is minimal — a single WebSocket endpoint for external apps to submit prompts and receive streaming responses. This is a well-trodden pattern.
- **Alternatives Considered:**
  - **Raw `System.Net.WebSockets.HttpListener`:** More manual work for connection management, TLS (not needed), and lifecycle. Rejected.
  - **Fleck / WebSocketSharp:** Lightweight WebSocket libraries. Would work but Kestrel provides better lifecycle integration with the .NET host.
  - **Named Pipes:** Windows-specific, simpler, but only accessible from the same machine (which is fine). However, WebSocket is more universally integrable (Word JavaScript Add-in can use WebSocket). Stay with WebSocket per vision spec.
- **Tradeoffs:** Gain: Production-grade embedded server, minimal code. Lose: Adds ASP.NET Core dependency (~5-10MB). Worth it for security and reliability.
- **Risk Level:** Low. ⚠️ Vision Flag #5: localhost-only binding is enforced at the Kestrel level. Token auth is minimal — acceptable for a single-user local service.

---

### 9. System Tray Integration

- **Vision Requirement:** Minimize to system tray. Left-click restores. Right-click menu: New Chat, Open Studio, Command Bar, Recent Chats, Settings, Exit. Visual indicator when AI is generating. App lifecycle: minimize to tray on close (configurable).
- **Sourcing Recommendation:** Custom Build using WPF interop with `System.Windows.Forms.NotifyIcon`.
- **Recommended Approach:** Use `System.Windows.Forms.NotifyIcon` via WPF/WinForms interop. WPF doesn't have a native tray icon control, but the interop is stable and supported. Custom context menu via `ContextMenuStrip`. Generation indicator via icon overlay or `NotifyIcon.ShowBalloonTip`.
- **Rationale:** This is a standard Windows pattern. The WinForms interop is the recommended Microsoft approach for WPF system tray. The custom work is in the context menu logic and generation-state indicator.
- **Alternatives Considered:**
  - **H.NotifyIcon (community library):** WPF-native wrapper around NotifyIcon. Reduces boilerplate. Worth evaluating for simpler XAML-based context menus.
  - **Hardcoded Win32 `Shell_NotifyIcon`:** Maximum control but unnecessary P/Invoke complexity. Rejected.
- **Tradeoffs:** Gain: Standard Windows behavior, minimal code. Lose: Visual customization of context menu is limited (WinForms menus, not WPF).
- **Risk Level:** Low.

---

### 10. Local Tokenization (Real-Time Token Counting)

- **Vision Requirement:** Real-time token count as user types in textbox. Context window display with "X / Y tokens" in chat header. Pre-send validation against model's max context. Token counting for usage records (S). Must be local — no API call for counting.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** `SharpToken` (C# port of OpenAI's `tiktoken`) for OpenAI-compatible tokenizers. For Anthropic, use Anthropic's tokenizer or a community port. For Google, use Gemini's published tokenizer or approximate. Provide a fallback approximation (character count / 4) for unknown providers. The `ITokenizer` abstraction allows per-model tokenizer selection.
- **Rationale:** SharpToken is a direct C# port of tiktoken and covers all OpenAI models. It's fast, accurate, and matches API-reported token counts. The abstraction layer handles the reality that different providers use different tokenizers.
- **Alternatives Considered:**
  - **Character-count estimation (chars/4):** Too inaccurate for context window management (can be off by 30-40%). Only acceptable as fallback.
  - **API-based counting:** Requires network call, defeats real-time feedback. Rejected.
  - **Custom tokenizer implementation:** Duplicates existing work. Rejected.
- **Tradeoffs:** Gain: Accurate, real-time, offline token counting. Lose: Must maintain tokenizer updates as new models add new tokenizers. SharpToken needs updates for new OpenAI models.
- **Risk Level:** Low. SharpToken is mature and maintained.

---

### 11. Full-Text Search

- **Vision Requirement:** Search all chat messages (permanent + transient within window) with results showing snippets, chat name, timestamp, highlights (L3). Also: wiki content search (N3), @-mention quick-search for wiki files (N7), global artifact search (F7), prompt library search (J1).
- **Sourcing Recommendation:** Custom Build using SQLite FTS5.
- **Recommended Approach:** SQLite FTS5 virtual tables for chat message content and wiki file content. FTS5 provides ranked full-text search, snippet extraction (`snippet()`), and highlight markup. The search UI queries FTS5 and joins against ChatThread for metadata. Wiki search uses a separate FTS5 index on the wiki_file index table.
- **Rationale:** FTS5 is built into SQLite and requires zero additional infrastructure. It's sufficient for single-user, local search — no need for Elasticsearch or Lucene.NET. The vision's search scope (messages + wiki files) maps naturally to two FTS5 virtual tables.
- **Alternatives Considered:**
  - **Lucene.NET:** More powerful (facets, fuzzy search, complex queries) but adds significant complexity and index management overhead. Overkill for single-user local search.
  - **Custom inverted index:** Recreating what FTS5 already does. Rejected.
- **Tradeoffs:** Gain: Zero additional dependencies, tight SQLite integration, sufficient for single-user scope. Lose: Less sophisticated ranking than Lucene; no fuzzy search or typo tolerance without custom FTS5 tokenizer work.
- **Risk Level:** Low. FTS5 is mature and well-documented.

---

### 12. File System Watcher (Wiki Monitoring)

- **Vision Requirement:** Monitor wiki directory for external changes (files added, edited, deleted outside the app). Trigger wiki re-indexing on change. Detect and report externally modified files in Wiki Browser file tree. Debounce rapid successive changes.
- **Sourcing Recommendation:** Custom Build using .NET `System.IO.FileSystemWatcher`.
- **Recommended Approach:** `FileSystemWatcher` with `NotifyFilter` for file name, last write, and directory changes. Debounce via `System.Reactive` or a simple timer-based buffer (accumulate changes over 500ms windows before triggering re-index). ⚠️ Vision Flag #14: auto-commit on file change (git wiki) must handle debouncing and must not commit while file is being written.
- **Rationale:** `FileSystemWatcher` is built into .NET and provides the exact functionality needed. The custom work is in debouncing and change batching — well-understood patterns.
- **Alternatives Considered:**
  - **Polling (timer-based directory scan):** Simpler, no watcher reliability issues, but uses more CPU for large directories. Could be used as fallback if FileSystemWatcher proves unreliable on certain file systems. Keep as option.
  - **Low-level `ReadDirectoryChangesW`:** More control but unnecessary P/Invoke. FileSystemWatcher wraps this. Rejected.
- **Tradeoffs:** Gain: Built-in, zero dependency. Lose: FileSystemWatcher has known reliability issues on network drives and some edge cases. Polling fallback mitigates this.
- **Risk Level:** Low-Medium. Mitigated by polling fallback.

---

### 13. Wiki Indexing & Cross-Linking Engine

- **Vision Requirement:** Parse all .md files in wiki directory. Extract: filename, H1-H6 headings, full content, word count, cross-links (extract `[text](path.md#heading)` patterns). Powers: wiki search (N3), @-mentions (N7), related sections (N4), backlinks detection (N4), auto-generated index.md (N11).
- **Sourcing Recommendation:** Custom Build using Markdig.
- **Recommended Approach:** Use Markdig to parse each .md file and walk the AST to extract headings, links, and plain text content. Store extracted metadata in SQLite wiki index tables. Cross-links are extracted by pattern-matching `[text](target.md#heading)` links during AST walk. Backlinks are computed by querying the cross-links index. Related sections: compute keyword overlap between current file's extracted terms and other files' content via SQLite FTS5 or simple TF-IDF.
- **Rationale:** Markdig provides a complete, fast AST. The extraction logic is straightforward AST walking. No specialized wiki engine needed — the wiki is plain .md files, and the index is a read-optimized SQLite representation.
- **Alternatives Considered:**
  - **Obsidian-compatible index:** Could adopt Obsidian's `.obsidian/` metadata format for interoperability, but the vision doesn't require it. More complex than needed.
  - **Full wiki engine (e.g., Wiki.js):** Server-based, contradicts local-first. Rejected.
- **Tradeoffs:** Gain: Simple, fast, tight integration with Markdig and SQLite FTS5. Lose: The related-sections algorithm (keyword overlap) is basic; no semantic understanding without adding embeddings (future enhancement).
- **Risk Level:** Low. Straightforward AST walking.

---

### 14. Diff Engine

- **Vision Requirement:** Side-by-side or unified diff view for: artifact version comparison (F4), wiki file update review before commit (N5 Step 5), find-and-replace preview (N13). Red = removed, green = added. Line-level diff.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** `DiffPlex` (MIT license, .NET-native) for line-level and word-level diff computation. Custom WPF rendering to display the diff results in side-by-side or unified view with color highlighting.
- **Rationale:** DiffPlex is the standard .NET diff library — fast, well-tested, and provides both line and word-level diffs. The rendering layer (WPF controls for side-by-side diff) is custom but straightforward — two scroll-synchronized text boxes with background coloring.
- **Alternatives Considered:**
  - **DiffMatchPatch (Google):** More features but C# port is less maintained. Rejected.
  - **Custom Myers diff implementation:** Unnecessary reinvention. Rejected.
- **Tradeoffs:** Gain: Fast, mature library, handles large files well. Lose: Rendering remains custom work.
- **Risk Level:** Low.

---

### 15. Charting & Data Visualization (Usage Dashboard)

- **Vision Requirement:** Line chart (tokens over time), bar chart (cost over time), pie charts (by provider, by model), approval trend chart (S3, S6). Interactive tooltips. Time-range filterable.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** `LiveCharts2` (MIT license, WPF-native, maintained) or `OxyPlot` (MIT, mature, WPF support). Both provide line, bar, and pie charts with MVVM binding, tooltips, and interactive features. LiveCharts2 has more modern aesthetics; OxyPlot has more chart types and is more stable.
- **Rationale:** Both libraries are mature, MIT-licensed, and WPF-compatible. The charting requirements are standard business charts — no need for a commercial library. LiveCharts2 is recommended for its modern animation and styling defaults that match the vision's dark/light theming.
- **Alternatives Considered:**
  - **ScottPlot:** Excellent for scientific plots but less suited for business dashboards. Rejected.
  - **Syncfusion / Telerik / DevExpress:** Commercial, expensive, and heavy. Overkill for 4 chart types. Rejected.
  - **WebView2 + Chart.js:** Adds WebView2 dependency (~100MB) for charts alone. Rejected.
  - **Custom WPF Drawing:** High effort for basic charts. Rejected.
- **Tradeoffs:** Gain: Free, WPF-native, MVVM-friendly. Lose: Limited to the library's chart types (sufficient for the vision).
- **Risk Level:** Low.

---

### 16. Web Search Integration (Tool Use H1)

- **Vision Requirement:** AI can request web search. App executes search, feeds results back to AI. Search query visible as system message. Used standalone and as part of Deep Research (H6).
- **Sourcing Recommendation:** SaaS / Custom Build.
- **Recommended Approach:** Search API integration: **Google Custom Search API** or **Bing Web Search API** (user brings own API key, same pattern as LLM providers). The app makes an HTTP request to the search API and returns structured results (title, URL, snippet) to the AI model. This is the most reliable approach. Alternative: open default browser with search query (free but janky).
- **Rationale:** A search API returns structured, parseable results that can be fed to the AI model programmatically. The BYO-API-key pattern is already established for LLM providers. Google Custom Search API has a free tier (100 queries/day) sufficient for individual use.
- **Alternatives Considered:**
  - **Browser automation (Selenium/Puppeteer):** Fragile, slow, website-dependent. Rejected.
  - **DuckDuckGo Instant Answer API (free, no key):** Limited results, less reliable. Could be a secondary fallback.
  - **SerpAPI (SaaS):** Adds another paid SaaS dependency. Contradicts BYO-keys ethos. Rejected.
- **Tradeoffs:** Gain: Reliable structured results. Lose: Requires another API key (Google/Bing). Adds cost per query (low but nonzero).
- **Risk Level:** Low. Search APIs are stable and well-documented.

---

### 17. Terminal / Shell Execution (Tool Use H2)

- **Vision Requirement:** AI requests shell command execution. ALWAYS requires explicit user confirmation. Command displayed with working directory and risk level. User approves → runs in terminal → output captured → fed back to AI. No auto-approval possible.
- **Sourcing Recommendation:** Custom Build using `System.Diagnostics.Process`. **Reference implementation:** Windows-MCP PowerShell tool (process execution, stdout/stderr capture, timeout handling).
- **Recommended Approach:** Use `Process.Start()` with `RedirectStandardOutput`, `RedirectStandardError`, and configurable working directory. Support both cmd.exe and PowerShell execution. Run in a background thread with timeout protection. Display command in confirmation dialog with risk-level heuristics (detect `rm`, `del`, `format`, `sudo`, `reg`, etc. and flag as HIGH risk). Capture stdout/stderr, feed back to AI as tool result. Windows-MCP's PowerShell tool validates this pattern: redirect stdout/stderr, capture exit codes, handle timeouts gracefully.
- **Rationale:** This is a standard process execution pattern. The complexity is purely in the security model and UX — not the execution mechanism. `System.Diagnostics.Process` is the correct .NET API. Windows-MCP proves this pattern works for AI-driven command execution in production.
- **Alternatives Considered:**
  - **PowerShell SDK:** Would enable structured output but adds dependency and complexity. Standard shell execution is simpler and more universal.
  - **Sandboxed execution (Docker, Windows Sandbox):** Adds massive complexity. The vision's security model is user approval, not sandboxing.
  - **Windows-MCP as execution backend:** Offloading command execution to Windows-MCP adds Python dependency for a capability that .NET handles natively. Rejected for production; useful as a reference for the execution pattern.
- **Tradeoffs:** Gain: Simple, standard, full control. Windows-MCP validates the execution pattern. Lose: Security relies entirely on user judgment. ⚠️ Vision Flag #4: risk is real. Mitigated by mandatory confirmation, risk highlighting, and clear documentation.
- **Risk Level:** High (security). Running arbitrary AI-suggested commands is inherently dangerous. The mandatory confirmation dialog with risk-level highlighting is the primary mitigation. Consider adding a command allow-list as a future enhancement.

---

### 18. File Generation & Editing (Tool Use H3, H4)

- **Vision Requirement:** AI creates new files on disk (user approves target path via save dialog). AI modifies existing files (user approves target file, reviews changes before applying). Cannot target wiki directory (N8 restriction).
- **Sourcing Recommendation:** Custom Build using .NET `System.IO`.
- **Recommended Approach:** File I/O via standard .NET APIs. For file editing, combine with the Diff Engine (component #14) to show pending changes as a diff before applying. Enforce wiki directory exclusion via path validation before any file operation.
- **Rationale:** File I/O is a core .NET capability. No library needed. The custom work is in the UX flow: save dialog → AI generates → preview → confirm.
- **Alternatives Considered:** None. This is basic file I/O.
- **Tradeoffs:** None. Standard platform capability.
- **Risk Level:** Low. File I/O is well-understood. Wiki exclusion is enforced via path prefix check.

---

### 19. Deep Research Orchestration (H6)

- **Vision Requirement:** Autonomous multi-step research: AI formulates plan → multiple web searches → reads sources → synthesizes → produces cited report. Real-time progress display. User can cancel. Duration: 1-5 minutes.
- **Sourcing Recommendation:** Custom Build (orchestration loop) + Web Search integration (#16).
- **Recommended Approach:** Custom orchestration loop: maintain a state machine (Plan → Search → Read → Synthesize → Report) driven by the AI model's tool-use responses. The AI model is given a "deep research" system prompt that instructs it to follow the multi-step plan. The app executes tool calls (search, fetch page content) and feeds results back. Progress is reported via system messages in the chat. This is essentially a structured tool-use conversation, not a separate subsystem.
- **Rationale:** The vision's deep research is a tool-use conversation with specialized prompting and progress display. It does not need a separate agent framework. The existing tool-use infrastructure (H1 web search, H2 terminal) provides the tools; the deep research prompt provides the orchestration.
- **Alternatives Considered:**
  - **LangChain / Semantic Kernel agent loop:** Would provide a more structured agent framework but adds significant complexity and abstraction. The vision's agent needs are bounded (plan→search→read→synthesize). Rejected for initial implementation; may be revisited if future agent capabilities expand.
  - **Dedicated research agent service:** Overkill for a single-user desktop app. Rejected.
- **Tradeoffs:** Gain: Minimal additional infrastructure, leverages existing tool-use system. Lose: Less sophisticated than a full agent framework; relies on the model's ability to follow the deep research prompt.
- **Risk Level:** Medium. Depends on model adherence to the research protocol. Prompt engineering is the key risk mitigation.

---

### 20. PDF Export (Chat Export I1)

- **Vision Requirement:** Export chat as PDF. Preserves formatting, code highlighting, images. Rendered version of Markdown export.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** Convert Markdown to HTML (via Markdig), then HTML to PDF via **wkhtmltopdf** (wrapped via `Haukcode.WkHtmlToPdfDotNet` or process invocation) or **QuestPDF** (native .NET, generates PDF from C# layout code). QuestPDF is preferred for avoiding an external process dependency, but requires manual layout construction. wkhtmltopdf gives pixel-perfect Markdown rendering but adds a ~30MB native binary dependency.
- **Rationale:** PDF generation is a well-solved problem. QuestPDF (MIT, native .NET) is recommended for keeping the dependency footprint small and the build self-contained. The tradeoff is that QuestPDF requires programmatic layout rather than HTML→PDF conversion.
- **Alternatives Considered:**
  - **PuppeteerSharp (headless Chromium):** Adds ~150MB+ Chromium dependency. Massive overkill. Rejected.
  - **iTextSharp / iText 7:** AGPL license or commercial. License incompatible with the project. Rejected.
  - **PdfSharp / MigraDoc:** Mature but limited HTML rendering. Rejected.
- **Tradeoffs:** Gain (QuestPDF): Native .NET, no external process, MIT license, small footprint. Lose: Must build PDF layout programmatically from Markdown AST. Gain (wkhtmltopdf): Perfect Markdown→HTML→PDF fidelity. Lose: External binary dependency.
- **Risk Level:** Low-Medium. Both options are viable. Recommend QuestPDF for initial implementation with wkhtmltopdf as fallback if layout quality is insufficient.

---

### 21. Speech-to-Text (STT)

- **Vision Requirement:** Voice dictation in Studio textbox (C21). Configured STT provider (A10): OpenAI Whisper API, local Whisper, or OpenAI-compatible STT endpoint. Transcribed text appears editable in textbox.
- **Sourcing Recommendation:** SaaS (OpenAI Whisper API) + Open-Source (local Whisper via Whisper.net).
- **Recommended Approach:** 
  - **Primary (cloud):** OpenAI Whisper API via the OpenAI SDK (same as LLM client). Simple HTTP POST with audio data.
  - **Alternative (local):** `Whisper.net` (MIT license, .NET binding for Whisper.cpp) for local, offline STT. Runs entirely on-device. Model download on first use (~1-4GB depending on model size).
  - **Architecture:** `ISTTProvider` interface with two implementations. Provider selection in Settings (A10).
- **Rationale:** OpenAI Whisper API is the easiest path — already uses the same SDK as LLM calls. Whisper.net provides a privacy-preserving local alternative. The abstraction allows either.
- **Alternatives Considered:**
  - **Windows built-in speech recognition (`System.Speech`):** Free, no API key, but lower accuracy and requires Windows language pack configuration. Worth offering as a third, zero-cost option.
  - **Azure Cognitive Services Speech:** Adds another cloud dependency. Rejected.
- **Tradeoffs:** Gain: Flexible — cloud for convenience, local for privacy. Lose: Local Whisper requires model download and GPU for reasonable speed.
- **Risk Level:** Low. Both approaches are well-established.

---

### 22. Audio Recording & Playback

- **Vision Requirement:** Record audio from microphone (C21) for STT. Playback AI-generated audio (G5) and user-uploaded audio files (G6) inline with mini player (play/pause/seek).
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** **NAudio** (MIT license, mature .NET audio library) for audio recording (wave input) and playback. NAudio handles microphone capture, WAV encoding, and playback with position tracking for the seek bar. For compressed formats (MP3), use NAudio with its MP3 decoder or the `MP3Sharp` library.
- **Rationale:** NAudio is the standard .NET audio library — mature, well-documented, and handles all required audio scenarios. No competitor matches its breadth and stability in the .NET ecosystem.
- **Alternatives Considered:**
  - **Windows.Media.Audio (UWP):** Modern but requires WinRT interop from WPF. More complex than NAudio.
  - **SDL2 / OpenAL bindings:** Game-audio libraries, overkill for recording/playback.
- **Tradeoffs:** Gain: Mature, MIT license, full-featured. Lose: API is verbose (low-level audio concepts).
- **Risk Level:** Low.

---

### 23. Webcam Capture

- **Vision Requirement:** Capture photo from webcam (C22). Attach to message for vision-capable models. Live preview + "Capture" button.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** **AForge.NET** or **Emgu.CV** (OpenCV wrapper) for webcam access, or **DirectShow** via `DirectShowLib`. AForge.NET is lighter weight and sufficient for still-image capture. Display live preview, capture frame on button click, save as JPEG to media storage.
- **Rationale:** Webcam capture is a standard computer vision task. AForge.NET provides a simple `VideoCaptureDevice` that enumerates cameras and captures frames. Emgu.CV is more powerful but adds a ~40MB native dependency.
- **Alternatives Considered:**
  - **Windows.Media.Capture (UWP):** Modern API but requires WinRT interop. More complex to integrate.
  - **OpenCVSharp:** Another OpenCV wrapper. Similar to Emgu.CV.
- **Tradeoffs:** Gain: Simple API for basic capture. Lose: AForge.NET is aging (last major update 2017) but functional for basic capture.
- **Risk Level:** Low. Webcam capture is a solved problem.

---

### 24. Video Playback

- **Vision Requirement:** Inline video player in chat messages with standard controls (G6). Play user-uploaded video files and AI-generated video (future T5).
- **Sourcing Recommendation:** Custom Build using WPF `MediaElement`.
- **Recommended Approach:** WPF's built-in `MediaElement` control provides video playback with standard controls (play/pause/seek/volume). It wraps Windows Media Player and supports common formats (MP4, WMV, AVI). For broader codec support, consider **LibVLCSharp** (VLC binding for .NET).
- **Rationale:** `MediaElement` is built into WPF and sufficient for common formats. LibVLCSharp adds codec universality (plays virtually anything) at the cost of a ~50MB VLC dependency. Start with MediaElement; evaluate LibVLCSharp if format support is insufficient.
- **Alternatives Considered:**
  - **FFmpeg.AutoGen:** Lower-level, more complex. Rejected for playback.
- **Tradeoffs:** Gain: Built-in (MediaElement), no dependency. Lose: Limited codec support without codec packs. LibVLC backup available.
- **Risk Level:** Low.

---

### 25. Spell Checking

- **Vision Requirement:** Red squiggly underline for misspelled words in textbox (C34). Right-click suggestions. "Add to Dictionary." English dictionary (configurable). Toggle in Settings.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** **NHunspell** (C# wrapper for Hunspell, same engine as LibreOffice/Firefox) or **WeCantSpell.Hunspell** (newer, more maintainable C# port). Both provide the spell-check engine with English dictionaries (~500KB). Integrate with WPF textbox via custom `SpellCheck.CustomDictionary` or adorner-based squiggly underlines.
- **Rationale:** Hunspell is the industry standard — used by Chrome, Firefox, LibreOffice, and many others. The .NET wrappers are mature and provide suggestion APIs for the right-click menu.
- **Alternatives Considered:**
  - **WPF built-in `SpellCheck.IsEnabled`:** Only supports OS-installed dictionaries, limited suggestion UI customization. Rejected for lack of "Add to Dictionary" and language configurability.
  - **Windows Spell Check API (WinRT):** Modern but WinRT interop complicates the WPF integration. Rejected.
- **Tradeoffs:** Gain: Industry-standard engine, customizable, small dictionary files. Lose: Must implement the squiggly-underline adorner and suggestion UI manually.
- **Risk Level:** Low.

---

### 26. Backup to Google Cloud Storage

- **Vision Requirement:** Full backup of SQLite DB, wiki .md files, and artifacts to Google Cloud Storage. Scheduled (daily/weekly) or manual. Credentials encrypted via DPAPI. Restore from backup list. (R1-R4)
- **Sourcing Recommendation:** SaaS (GCS) + Open-Source Library (GCS SDK).
- **Recommended Approach:** `Google.Cloud.Storage.V1` NuGet package (official Google SDK) for upload/download. Credentials: service account JSON key file, stored encrypted via DPAPI (reuse API key encryption pattern). Zip/tar the backup contents before upload. Restore: download → extract → replace local files → restart app.
- **Rationale:** GCS is specified in the vision. The official SDK is well-maintained and handles auth, resumable uploads, and error handling. ⚠️ Vision Flag #9: single cloud provider dependency. Consider also supporting local file backup to a user-specified folder as a simpler, zero-dependency alternative. This should be added as a backup target option alongside GCS.
- **Alternatives Considered:**
  - **AWS S3 / Azure Blob Storage:** Would be alternatives but GCS is the vision's choice. The backup abstraction should support multiple providers.
  - **Rclone wrapper:** External process dependency. Rejected.
  - **Local file backup only:** Simpler but no off-site disaster recovery.
- **Tradeoffs:** Gain: Off-site backup, mature SDK. Lose: GCS dependency (Flag #9); user needs a GCP account. Recommend adding local folder backup as an alternative target.
- **Risk Level:** Medium. GCS dependency is the concern. Mitigated by adding local backup option.

---

### 27. Auto-Update Mechanism

- **Vision Requirement:** Check for updates on startup, daily, weekly, or manual (A7). Notify when available. Download and install. App restarts after update.
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** **NetSparkle** or **AutoUpdater.NET** (MIT license, purpose-built for .NET desktop apps). Handles version checking against a remote feed (JSON/XML), downloading MSIX/MSI installer, and triggering installation. Alternatively, for MSIX-packaged apps, use the Microsoft Store / App Installer auto-update mechanism built into Windows.
- **Rationale:** AutoUpdater.NET is mature, supports incremental downloads, and integrates with MSIX. If distributing via MSIX with App Installer, Windows handles updates natively. NetSparkle adds Sparkle-style (macOS) update UX with release notes.
- **Alternatives Considered:**
  - **Custom updater:** Recreating download, verification, and installer-launch logic. High effort for low differentiation. Rejected.
  - **Squirrel.Windows:** Popular but complex; designed for ClickOnce-style deployment. Rejected.
  - **Microsoft Store:** Requires store submission. User may prefer direct distribution. Optional channel.
- **Tradeoffs:** Gain: Mature, purpose-built, handles edge cases. Lose: Adds dependency but highly focused.
- **Risk Level:** Low.

---

### 28. Git Integration (Wiki Version Control)

- **Vision Requirement:** Initialize git repository in wiki directory. Auto-commit on file change (debounced). Optional GitHub remote push with Personal Access Token. Token encrypted via DPAPI. (Onboarding Step 3, Flag #11, Flag #13, Flag #14)
- **Sourcing Recommendation:** Open-Source Library.
- **Recommended Approach:** **LibGit2Sharp** (MIT license, .NET binding for libgit2). Provides full git functionality in-process: `git init`, `git add`, `git commit`, `git push`, `git remote`. No external git executable required. Auto-commit logic: buffer changes via file system watcher, debounce 5 seconds, commit with auto-generated message. GitHub push: use Personal Access Token for HTTPS auth, stored encrypted via DPAPI.
- **Rationale:** LibGit2Sharp is the standard .NET git library — used by GitHub Desktop, GitKraken, and Azure DevOps. It handles all git operations in-process with no external dependency. ⚠️ Vision Flag #13: git version control may make N6 (snapshot-based versioning) partially redundant. The architect recommends keeping N6 snapshots as instant undo within the app (fast, no git overhead) and using git for cross-session version history and remote backup.
- **Alternatives Considered:**
  - **Execute `git.exe` as external process:** Simpler code, depends on git being installed. LibGit2Sharp avoids the external dependency.
  - **Custom snapshot system only (N6):** Already implemented but lacks remote push and standard git tooling.
- **Tradeoffs:** Gain: In-process, no git installation required, full git functionality. Lose: LibGit2Sharp adds ~15MB native binaries. Push requires network and GitHub token.
- **Risk Level:** Medium. Auto-commit debouncing (Flag #14), token security (Flag #11), and merge conflict handling need careful implementation.

---

### 29. Chat Import Parsing (ChatGPT / Claude)

- **Vision Requirement:** Import chat history from ChatGPT export (JSON) and Claude export (JSON). Parse conversation structure, preserve timestamps and roles. Create ChatThread for each imported conversation. Duplicate detection. (I2)
- **Sourcing Recommendation:** Custom Build using `System.Text.Json`.
- **Recommended Approach:** Write two format-specific parsers implementing `IChatImporter`. Use `System.Text.Json` for JSON parsing with schema validation. ChatGPT's export format is well-documented; Claude's format may require adaptation as their export evolves. Map imported data to the ChatThread + Message data model. Duplicate detection: compare chat title + first message content hash.
- **Rationale:** The formats are JSON and well-documented. A custom parser is straightforward and avoids depending on third-party libraries that may not track format changes. The parsing logic is ~200-300 lines per format.
- **Alternatives Considered:**
  - **Generic chat import library:** Doesn't exist for these specific formats.
  - **Pandoc-based conversion:** Overkill and doesn't preserve chat structure.
- **Tradeoffs:** Gain: Full control, format-aware validation, clear error messages. Lose: New export format versions may break parsing. Must monitor ChatGPT/Claude export format changes.
- **Risk Level:** Low-Medium. Format stability is outside our control. Defensive parsing (skip unknown fields, report warnings) mitigates this.

---

### 30. Encryption — API Keys & Chat Locking

- **Vision Requirement:** API keys encrypted at rest via Windows DPAPI (B1). Locked chats: AES-256-GCM encryption with password-derived key (PBKDF2/Argon2), per-chat password or global default (C31). Backup credentials encrypted via DPAPI (R1). GitHub token encrypted via DPAPI (Flag #11).
- **Sourcing Recommendation:** Custom Build using .NET cryptography APIs.
- **Recommended Approach:**
  - **DPAPI:** `System.Security.Cryptography.ProtectedData.Protect()` / `Unprotect()` with `DataProtectionScope.CurrentUser`. Scope: tied to the local Windows user account.
  - **AES-256-GCM:** `System.Security.Cryptography.AesGcm` (.NET 8+ built-in). Key derivation via `Rfc2898DeriveKey` (PBKDF2) with configurable iterations. Salt stored alongside ciphertext.
- **Rationale:** .NET provides all needed cryptographic primitives. DPAPI is the correct choice for API keys (no user password needed, tied to Windows login). AesGcm is built into .NET 8+. No external crypto library needed.
- **Alternatives Considered:**
  - **Azure Key Vault / cloud KMS:** Contradicts local-first, single-user. Rejected.
  - **BouncyCastle:** More algorithms but unnecessary for AES-GCM + PBKDF2. Rejected.
- **Tradeoffs:** Gain: Zero external crypto dependencies, platform-native encryption. Lose: DPAPI keys are lost if the Windows user profile is corrupted. AES-GCM chat encryption is irrecoverable if password is lost (by design — Flag #10).
- **Risk Level:** Low for implementation. Medium for UX (Flag #10: permanent lockout risk from lost chat passwords — must be clearly communicated).

---

### 31. Message Branching Data Model

- **Vision Requirement:** Every message is versioned. Editing creates branches (D1). Navigate between branches (D3). Visual chat tree (D4). Fork chat (D7). Accept comparison result creates branches (M4). All branches preserved, nothing overwritten.
- **Sourcing Recommendation:** Custom Build on SQLite.
- **Recommended Approach:** Implement a version-chain data model in SQLite:
  - Messages have: `branchId` (groups versions together), `versionNumber`, `isActiveBranch`, `parentMessageId` (previous message in conversation chain).
  - The active conversation path is determined by following `parentMessageId` chain where `isActiveBranch=true`.
  - Branch navigation: change `isActiveBranch` flags along the chain, re-render subsequent messages.
  - Chat tree visualization: query all messages in thread, build a tree graph from `parentMessageId` relationships, render via custom WPF `Canvas` or a graph layout library.
- **Rationale:** This data model is custom and central to the app's differentiation. It must be purpose-built on SQLite — no off-the-shelf library handles this specific branching chat model. The SQL queries for branch traversal are straightforward recursive CTEs. The tree visualization rendering is the most complex aspect.
- **Alternatives Considered:**
  - **Graph database (Neo4j, etc.):** Overkill, adds server dependency. Rejected.
  - **Event sourcing pattern:** Each message edit is an event; active state is a projection. Elegant but adds complexity. The version-chain approach is simpler.
- **Tradeoffs:** Gain: Purpose-built for the exact branching model. Lose: Must implement tree visualization from scratch (can reference graph layout algorithms).
- **Risk Level:** Medium. The data model is well-understood; the tree visualization rendering is the novel work.

---

### 32. Three-Tier Interaction Architecture

- **Vision Requirement:** Three distinct interaction modes sharing the same underlying ChatThread data model:
  - Tier 1: Minimal pill overlay near cursor, three-phase (Capture → Result → Apply)
  - Tier 2: Spotlight-style Command Bar, inline Q&A + popped-out mini-window
  - Tier 3: Full Studio workspace
  - Seamless elevation between tiers. Transient → permanent lifecycle.
- **Sourcing Recommendation:** Custom Build on WPF.
- **Recommended Approach:** Three WPF window types sharing a common `ChatThreadService`:
  - `Tier1OverlayWindow`: Transparent, topmost, positioned near cursor. Captures HWND, displays pill/result popup.
  - `Tier2CommandBarWindow`: Topmost, centered overlay. Expands inline, detaches to resizable floating window.
  - `MainWindow` (Tier 3): Full Studio workspace.
  - All three share the same `ChatThreadService` (singleton), `LLMProviderService`, and data context.
  - Elevation: Tier 1/2 windows create a ChatThread, pass its ID to MainWindow, and close themselves.
- **Rationale:** This is the most architecturally distinctive aspect of MySecondBrain. No existing product implements this three-tier model. It must be custom-built. The key architectural insight is that all three tiers share the same ChatThread data model — the tier is purely a UI manifestation.
- **Alternatives Considered:** None. This is the app's core innovation and must be custom-built.
- **Tradeoffs:** Gain: Unique, differentiating interaction model. Lose: Significant architectural complexity; three window types with shared state.
- **Risk Level:** Medium-High. Managing shared state across three window types, focus management, and overlay Z-order requires careful implementation.

---

### 33. Theming & Dark/Light Mode

- **Vision Requirement:** Dark mode (default) and light mode (A5). Three chat visual themes: Classic, Compact, Bubble (A3). Customizable font family, size, weight. Instant toggle without restart. Independent of chat themes.
- **Sourcing Recommendation:** Custom Build on WPF Resource Dictionaries.
- **Recommended Approach:** WPF `ResourceDictionary` with theme key-value pairs. Two top-level dictionaries (Dark.xaml, Light.xaml) for app chrome. Three chat-specific templates (Classic, Compact, Bubble) for message layout. `DynamicResource` references throughout enable instant theme switching. Font settings stored in SQLite and bound via `DynamicResource` or attached properties. ⚠️ Vision Flag: A5 architecture note — WPF Resource Dictionaries are the correct approach.
- **Rationale:** WPF's resource dictionary system is purpose-built for theming. `DynamicResource` enables runtime theme switching without restart. The three chat themes are template variations (different `DataTemplate` for message items).
- **Alternatives Considered:**
  - **CSS-like theming (e.g., MaterialDesignInXAML):** Would work but adds dependency for what WPF does natively. The built-in approach gives full control.
  - **Third-party theme library:** Adds bloat. Custom resource dictionaries are sufficient.
- **Tradeoffs:** Gain: Native WPF, instant switching, full control. Lose: Must maintain two color palettes; every new UI element needs theme-aware styling.
- **Risk Level:** Low.

---

### 34. Bidirectional Text Rendering (Hebrew RTL)

- **Vision Requirement:** Auto-detect Hebrew (Unicode range U+0590-U+05FF) and render RTL. Mixed LTR/RTL messages render segments in correct direction. Textbox input respects typing direction. Code blocks always LTR. (Q1-Q3)
- **Sourcing Recommendation:** Custom Build using WPF `FlowDirection`.
- **Recommended Approach:**
  - **Detection:** Scan message content for Hebrew Unicode characters. If >threshold% Hebrew characters, set `FlowDirection="RightToLeft"` on the message container.
  - **Mixed content:** Use WPF's built-in bidirectional algorithm (Unicode Bidi Algorithm via `FlowDocument`). WPF `TextBlock` and `FlowDocument` natively handle mixed LTR/RTL.
  - **Textbox:** Set `FlowDirection` based on first strong directional character typed. Toggle via Settings (Q: "Auto-detect RTL").
  - ⚠️ Vision Flag #8: WPF's built-in BiDi support is mature and should handle this. The main complexity is in code blocks — must enforce LTR `FlowDirection` on code block elements regardless of content.
- **Rationale:** WPF has excellent built-in bidirectional text support via the Unicode Bidi Algorithm. The custom work is in per-message/per-segment direction detection and enforcing LTR on code blocks.
- **Alternatives Considered:**
  - **Custom text layout engine:** Enormous effort. WPF handles this natively. Rejected.
  - **RichTextBlock (WinRT):** Requires WinRT interop, no benefit over WPF FlowDocument. Rejected.
- **Tradeoffs:** Gain: WPF-native, well-tested BiDi algorithm. Lose: Per-segment direction control within a single message requires careful FlowDocument element insertion.
- **Risk Level:** Medium. Bidirectional text rendering is complex. WPF handles the hard parts; the custom work is in detection and per-block direction enforcement.

---

### 35. Auto-Save Drafts

- **Vision Requirement:** Textbox content auto-saves every 5 seconds (C36). Per-chat draft storage. Recovery dialog after crash/tab close. Cleanup on successful send or empty textbox.
- **Sourcing Recommendation:** Custom Build.
- **Recommended Approach:** `System.Timers.Timer` or `PeriodicTimer` for 5-second tick. On tick: serialize current textbox content + cursor position to SQLite `MessageDrafts` table (keyed by ChatThread ID). On tab open: check for existing draft, show recovery dialog. On send: delete draft. On empty textbox + 5s: delete draft.
- **Rationale:** This is straightforward state persistence. No external library needed. The pattern is identical to any auto-save implementation.
- **Alternatives Considered:** None. Trivial custom implementation.
- **Tradeoffs:** None.
- **Risk Level:** Low.

---

### 36. Per-Monitor DPI Awareness

- **Vision Requirement:** Full per-monitor DPI awareness. Crisp rendering at any scaling (100%-200%+). Adapts when window moves between monitors with different DPI. (P8)
- **Sourcing Recommendation:** Custom Build using WPF's built-in DPI support.
- **Recommended Approach:** WPF's `PerMonitorV2` DPI awareness mode (set in app.manifest). WPF handles DPI scaling natively for vector-based UI. For bitmap resources (icons, images), provide multi-resolution assets or use vector formats (SVG via `SharpVectors`). Test across common DPI configurations.
- **Rationale:** WPF is fundamentally DPI-aware (device-independent pixels). `PerMonitorV2` is the modern Windows 10/11 DPI mode that handles per-monitor scaling. This is a configuration, not a library choice.
- **Alternatives Considered:** None. WPF handles this natively.
- **Tradeoffs:** Gain: Native, automatic. Lose: Bitmap icons need multi-resolution or SVG conversion.
- **Risk Level:** Low.

---

## Reference Implementation Analyses

The following reference implementations were studied for specific components. Detailed analyses are saved to `agent-workspace/external-docs/`.

| Reference | Studied For | File |
|-----------|-------------|------|
| **Cherry Studio** | Multi-provider LLM adapter pattern, SSE normalization, provider configuration UI, tool use, streaming | [`ref-cherry-studio-llm-providers.md`](../external-docs/ref-cherry-studio-llm-providers.md) |
| **PowerToys Run** | Spotlight-style command bar, global hotkeys, overlay window management | [`ref-powertoys-global-hotkeys.md`](../external-docs/ref-powertoys-global-hotkeys.md) |
| **Windows-MCP** | UI Automation, text injection, process execution, clipboard, screenshot | [`ref-windows-mcp-automation.md`](../external-docs/ref-windows-mcp-automation.md) |
| **ChatGPT Desktop** | Three-tier interaction model, streaming Markdown rendering, chat workspace UX | [`ref-chatgpt-desktop-chat-ux.md`](../external-docs/ref-chatgpt-desktop-chat-ux.md) |
| **Obsidian** | Personal wiki file tree, Markdown rendering, backlinks, graph view, cross-linking | [`ref-obsidian-wiki-system.md`](../external-docs/ref-obsidian-wiki-system.md) |
| **Claude Desktop** | Artifacts panel, version history, thinking/reasoning display | [`ref-claude-desktop-artifacts.md`](../external-docs/ref-claude-desktop-artifacts.md) |

---

## Sourcing Summary

| # | Component | Category | Recommendation | Risk |
|---|-----------|----------|----------------|------|
| 1 | UI Framework (WPF Shell) | Platform | .NET 8.0 WPF + CommunityToolkit.Mvvm | Low |
| 2 | Local Database | Open-Source | SQLite + EF Core + FTS5 | Low |
| 3 | LLM Provider HTTP Client | Open-Source + Ref: Cherry Studio | Adapter pattern: OpenAI SDK + Anthropic SDK + Google SDK | Low-Med |
| 4 | Markdown & Code Rendering | Open-Source | Markdig + AvalonEdit highlighting | Medium |
| 5 | Global Keyboard Hooks | Custom (P/Invoke) | RegisterHotKey + WH_KEYBOARD_LL fallback | Medium |
| 6 | HWND Capture & Text Injection | Custom (P/Invoke) + Ref: Windows-MCP | UIA ValuePattern → WM_SETTEXT → clipboard fallback | Med-High |
| 7 | Clipboard Format Preservation | Custom (.NET) | System.Windows.Clipboard | Low |
| 8 | Local WebSocket Server | Platform (Kestrel) | ASP.NET Core Kestrel embedded | Low |
| 9 | System Tray Integration | Custom (WinForms) | NotifyIcon interop | Low |
| 10 | Local Tokenization | Open-Source | SharpToken + ITokenizer abstraction | Low |
| 11 | Full-Text Search | Platform (SQLite) | SQLite FTS5 | Low |
| 12 | File System Watcher | Custom (.NET) | FileSystemWatcher + debounce + polling fallback | Low-Med |
| 13 | Wiki Indexing & Cross-Linking | Custom + Open-Source | Markdig AST walker + SQLite index | Low |
| 14 | Diff Engine | Open-Source | DiffPlex | Low |
| 15 | Charting / Data Visualization | Open-Source | LiveCharts2 or OxyPlot | Low |
| 16 | Web Search Integration | SaaS | Google Custom Search API or Bing API | Low |
| 17 | Terminal / Shell Execution | Custom (.NET) | System.Diagnostics.Process | High |
| 18 | File Generation & Editing | Custom (.NET) | System.IO + diff preview | Low |
| 19 | Deep Research Orchestration | Custom | Tool-use conversation loop | Medium |
| 20 | PDF Export | Open-Source | QuestPDF (preferred) or wkhtmltopdf | Low-Med |
| 21 | Speech-to-Text (STT) | SaaS + Open-Source | OpenAI Whisper API + Whisper.net | Low |
| 22 | Audio Recording & Playback | Open-Source | NAudio | Low |
| 23 | Webcam Capture | Open-Source | AForge.NET | Low |
| 24 | Video Playback | Platform (WPF) | MediaElement + LibVLCSharp fallback | Low |
| 25 | Spell Checking | Open-Source | WeCantSpell.Hunspell | Low |
| 26 | Backup to GCS | SaaS + Open-Source | Google.Cloud.Storage.V1 SDK | Medium |
| 27 | Auto-Update Mechanism | Open-Source | AutoUpdater.NET or NetSparkle | Low |
| 28 | Git Integration (Wiki) | Open-Source | LibGit2Sharp | Medium |
| 29 | Chat Import Parsing | Custom | System.Text.Json format-specific parsers | Low-Med |
| 30 | Encryption (DPAPI + AES-GCM) | Platform (.NET) | System.Security.Cryptography | Low-Med |
| 31 | Message Branching Data Model | Custom (SQLite) | Version-chain model with recursive CTE | Medium |
| 32 | Three-Tier Interaction Architecture | Custom (WPF) | Three window types, shared ChatThreadService | Med-High |
| 33 | Theming & Dark/Light Mode | Custom (WPF) | ResourceDictionary + DynamicResource | Low |
| 34 | Bidirectional Text (RTL) | Platform (WPF) | FlowDocument BiDi with per-block direction | Medium |
| 35 | Auto-Save Drafts | Custom | Timer + SQLite draft table | Low |
| 36 | Per-Monitor DPI Awareness | Platform (WPF) | PerMonitorV2 app.manifest | Low |

### Categorical Distribution

| Category | Count | Components |
|----------|-------|------------|
| **Custom Build** | 12 | Global Hooks, HWND Injection, Clipboard, System Tray, File Watcher, Wiki Indexing, Deep Research, File I/O, Chat Import, Branching Model, Three-Tier, Theming |
| **Open-Source Library** | 13 | SQLite, LLM SDKs, Markdig, SharpToken, DiffPlex, Charts, NAudio, AForge, Hunspell, QuestPDF, AutoUpdate, LibGit2Sharp, PDF |
| **Platform (.NET/WPF)** | 7 | WPF UI, Kestrel, FTS5, Encryption, MediaElement, BiDi, DPI |
| **SaaS / Cloud** | 2 | Web Search API, GCS Backup |
| **Custom + Open-Source hybrid** | 2 | STT (Whisper API + Whisper.net), Wiki Index (Markdig + Custom) |

### Risk Concentration

- **High Risk (1):** Terminal Execution
- **Medium-High Risk (2):** HWND Capture & Text Injection, Three-Tier Interaction Architecture
- **Medium-High Risk (1):** Three-Tier Interaction Architecture  
- **Medium Risk (8):** Markdown Rendering, Global Hooks, Git Integration, Deep Research, Branching Model, BiDi Text, GCS Backup, PDF Export
- **Low-Medium Risk (4):** LLM Providers, File Watcher, Chat Import, Encryption UX
- **Low Risk (21):** All other components

---

*Technology sourcing completed 2026-06-17. This document should be reviewed and updated when new vision features are added or when selected libraries require version updates.*
