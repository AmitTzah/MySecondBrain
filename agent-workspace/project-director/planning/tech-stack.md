# Technology Stack — MySecondBrain

## Stack Summary

| Layer | Technology | Version / Notes |
|-------|-----------|-----------------|
| **Runtime** | .NET 8.0+ | LTS, Windows 10 (22H2+) / Windows 11 |
| **UI Framework** | WPF | Native Windows presentation framework |
| **MVVM Toolkit** | CommunityToolkit.Mvvm | Latest stable (NuGet) |
| **DI Container** | Microsoft.Extensions.DependencyInjection | Built into .NET |
| **Local Database** | SQLite + EF Core + FTS5 | Microsoft.Data.Sqlite + EF Core 8.x |
| **Markdown Parsing** | Markdig | Latest stable (NuGet) |
| **Syntax Highlighting** | AvalonEdit highlighting engine | Code block coloring |
| **LLM SDK — OpenAI** | OpenAI (official NuGet) | Covers OpenAI, DeepSeek, Mistral, OpenAI-compatible |
| **LLM SDK — Anthropic** | Anthropic.SDK (community) | Latest stable (NuGet) |
| **LLM SDK — Google** | Google.Cloud.AIPlatform.V1 | Official Google Cloud SDK |
| **Token Counting** | SharpToken | C# port of tiktoken |
| **Audio Recording/Playback** | NAudio | MIT license, mature .NET audio |
| **Diff Engine** | DiffPlex | MIT license, line/word-level diff |
| **Charts** | LiveCharts2 | WPF-native, MVVM binding, dark/light theming |
| **Spell Check** | WeCantSpell.Hunspell | Hunspell .NET port |
| **PDF Export** | QuestPDF | Native .NET, MIT license |
| **Auto-Update** | AutoUpdater.NET | Purpose-built for .NET desktop |
| **Git (Wiki VCS)** | LibGit2Sharp | MIT license, in-process git |
| **Webcam Capture** | AForge.NET | Still-image capture |
| **Local STT** | Whisper.net | .NET binding for Whisper.cpp |
| **Embedded WebSocket** | ASP.NET Core Kestrel | Built into .NET, localhost-only |
| **Encryption** | System.Security.Cryptography | DPAPI + AES-256-GCM (.NET 8+) |
| **Global Hotkeys** | P/Invoke (RegisterHotKey) | Custom thin wrapper |
| **Text Injection** | UI Automation + P/Invoke | UIA ValuePattern → WM_SETTEXT → clipboard |
| **Video Playback** | WPF MediaElement | Built-in; LibVLCSharp as fallback |
| **Backup Cloud SDK** | Google.Cloud.Storage.V1 | Official Google Cloud Storage SDK |

---

## Per-Component Stack Detail

Each component below references the approved decision from [`tech-sourcing.md`](../tech-sourcing.md). Sourcing categories: 🟢 Use (OSS), 🔵 Build (Custom), ⚪ Platform (Built-in).

---

### 🟢 Open-Source Libraries (15 components)

#### SQLite + EF Core
- **Packages:** `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`
- **Why:** Zero-config, in-process, single-file database. EF Core provides ORM with migrations for schema evolution across auto-updates. SQLite FTS5 built-in for full-text search on chat messages and wiki content.
- **Vision requirements met:** All 13 data entities (A9 VACUUM, O6 compaction), branching message trees (recursive CTE), full-text search (L3, N3), single-user local storage.
- **Constraints:** No built-in encryption — addressed via DPAPI at field level for API keys, AES-256-GCM for locked chats.
- **Ref:** [tech-sourcing #2](../tech-sourcing.md#2-local-database--sqlite)

#### Markdig
- **Package:** `Markdig`
- **Why:** The .NET Markdown standard — fast, extensible, handles all syntax (headings, bold, italic, code blocks, lists, links, tables, blockquotes, task lists, custom extensions). Used for chat message rendering, wiki file rendering, and Markdown export.
- **Vision requirements met:** Full Markdown rendering in chat (C2), wiki rendering (N4), Markdown export (I1).
- **Constraints:** Progressive streaming requires custom FlowDocument update logic — Markdig provides the AST, but the streaming renderer is custom-built.
- **Ref:** [tech-sourcing #4](../tech-sourcing.md#4-markdown--code-rendering-engine)

#### OpenAI SDK
- **Package:** `OpenAI` (official NuGet)
- **Why:** Official, vendor-maintained .NET SDK. Handles authentication, streaming (SSE), retries, error handling. Covers OpenAI, DeepSeek, Mistral, and any OpenAI-compatible endpoint. One SDK for ~70% of providers.
- **Vision requirements met:** Chat completions with streaming (C4), vision/image input (C9), tool-use/function calling (H), model list fetching (B7), API key validation. Also used for OpenAI Whisper API (STT — A10).
- **Constraints:** Vendor SDK version may lag API changes. The `ILLMProvider` abstraction layer isolates the rest of the app from SDK version churn.
- **Ref:** [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client)

#### Anthropic SDK
- **Package:** `Anthropic.SDK` (community NuGet)
- **Why:** Separate API structure from OpenAI (Messages API vs. Chat Completions). Community .NET SDK is well-maintained and wraps Anthropic's REST API.
- **Vision requirements met:** Claude chat with streaming, thinking/reasoning tokens (E3), tool-use, vision input.
- **Constraints:** Community SDK dependency — risk of maintenance gaps. Mitigated by the `ILLMProvider` abstraction; a fallback to direct HTTP is possible if the SDK becomes unmaintained.
- **Ref:** [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client)

#### Google Cloud SDK
- **Packages:** `Google.Cloud.AIPlatform.V1` (Gemini), `Google.Cloud.Storage.V1` (GCS backup)
- **Why:** Official Google-maintained SDKs for Gemini chat API and Google Cloud Storage backup. Handles auth, streaming, resumable uploads.
- **Vision requirements met:** Gemini chat (B1), GCS backup (R1-R4).
- **Constraints:** Google Cloud SDK is large (~50MB+). Acceptable for a desktop app. GCS backup dependency is flagged (Vision Flag #9) — mitigated by local folder backup alternative.
- **Ref:** [tech-sourcing #3](../tech-sourcing.md#3-llm-provider-http-client), [tech-sourcing #26](../tech-sourcing.md#26-backup-to-google-cloud-storage)

#### SharpToken
- **Package:** `SharpToken`
- **Why:** C# port of OpenAI's tiktoken — accurate, offline, no API call needed. Matches API-reported token counts for OpenAI models.
- **Vision requirements met:** Real-time token count as user types (C11), context window display, pre-send validation against model's max context.
- **Constraints:** Covers OpenAI tokenizers. For Anthropic/Google, the `ITokenizer` abstraction allows per-model tokenizer selection with character-count fallback (chars/4) for unknown providers.
- **Ref:** [tech-sourcing #10](../tech-sourcing.md#10-local-tokenization-real-time-token-counting)

#### NAudio
- **Package:** `NAudio`
- **Why:** The .NET audio standard — mature (15+ years), MIT license, handles WAV/MP3 recording and playback with position tracking.
- **Vision requirements met:** Microphone recording for STT (C21), audio playback (G5, G6) with mini player (play/pause/seek).
- **Constraints:** API is verbose (low-level audio concepts). Wrapper service recommended for app-level consumption.
- **Ref:** [tech-sourcing #22](../tech-sourcing.md#22-audio-recording--playback)

#### DiffPlex
- **Package:** `DiffPlex`
- **Why:** Standard .NET diff library — fast, MIT license, provides both line-level and word-level diffs.
- **Vision requirements met:** Artifact version comparison (F4), wiki file update review before commit (N5 Step 5), find-and-replace preview (N13).
- **Constraints:** Rendering of diff results (side-by-side/unified view with color highlighting) is custom WPF work.
- **Ref:** [tech-sourcing #14](../tech-sourcing.md#14-diff-engine)

#### LiveCharts2
- **Package:** `LiveCharts2`
- **Why:** Modern WPF-native charts with animations, dark/light theming, MVVM binding. Supports line, bar, and pie charts with interactive tooltips.
- **Vision requirements met:** Usage dashboard charts (S3): line chart (tokens over time), bar chart (cost over time), pie charts (by provider, by model).
- **Constraints:** Limited to the library's chart types — sufficient for the vision's 4 chart types. OxyPlot available as fallback if needed.
- **Ref:** [tech-sourcing #15](../tech-sourcing.md#15-charting--data-visualization-usage-dashboard)

#### WeCantSpell.Hunspell
- **Package:** `WeCantSpell.Hunspell`
- **Why:** Hunspell is the industry standard (Chrome, Firefox, LibreOffice). Newer, more maintainable C# port than NHunspell. English dictionaries ~500KB.
- **Vision requirements met:** Red squiggly underline for misspelled words (C34), right-click suggestions, "Add to Dictionary", toggle in Settings.
- **Constraints:** Must implement the squiggly-underline adorner and suggestion UI manually in WPF.
- **Ref:** [tech-sourcing #25](../tech-sourcing.md#25-spell-checking)

#### QuestPDF
- **Package:** `QuestPDF`
- **Why:** Native .NET PDF generation, MIT license, no external binary dependency. Generates PDF from C# layout code.
- **Vision requirements met:** Chat export to PDF with preserved formatting, code highlighting, images (I1).
- **Constraints:** Requires programmatic layout construction rather than HTML→PDF conversion. wkhtmltopdf available as fallback if layout quality is insufficient.
- **Ref:** [tech-sourcing #20](../tech-sourcing.md#20-pdf-export-chat-export-i1)

#### AutoUpdater.NET
- **Package:** `Autoupdater.NET.Official` (or `NetSparkle`)
- **Why:** Purpose-built for .NET desktop apps. Handles version checking against remote feed (JSON/XML), downloading MSIX/MSI installer, triggering installation, and app restart.
- **Vision requirements met:** Check for updates on startup/daily/weekly/manual (A7), notify when available, download and install.
- **Constraints:** Requires hosting an update feed (JSON/XML) at a known URL. MSIX App Installer auto-update is a Windows-native alternative if distributing via MSIX.
- **Ref:** [tech-sourcing #27](../tech-sourcing.md#27-auto-update-mechanism)

#### LibGit2Sharp
- **Package:** `LibGit2Sharp`
- **Why:** In-process git — no git.exe needed. Used by GitHub Desktop and GitKraken. Provides full git functionality: init, add, commit, push, remote.
- **Vision requirements met:** Git init in wiki directory, auto-commit on file change (debounced), optional GitHub push with PAT (encrypted via DPAPI). (Onboarding Step 3, Flag #11, #13, #14)
- **Constraints:** Adds ~15MB native binaries. Auto-commit debouncing must handle rapid successive changes and must not commit while file is being written (Flag #14).
- **Ref:** [tech-sourcing #28](../tech-sourcing.md#28-git-integration-wiki-version-control)

#### AForge.NET
- **Package:** `AForge.Video.DirectShow`
- **Why:** Simple API for webcam enumeration and frame capture. Sufficient for still-image capture.
- **Vision requirements met:** Webcam photo capture (C22), live preview + "Capture" button.
- **Constraints:** Library is aging (last major update 2017) but functional for basic capture. Emgu.CV available as fallback.
- **Ref:** [tech-sourcing #23](../tech-sourcing.md#23-webcam-capture)

#### Whisper.net
- **Package:** `Whisper.net`
- **Why:** .NET binding for Whisper.cpp. Privacy-preserving local STT alternative to OpenAI Whisper API. Runs entirely on-device.
- **Vision requirements met:** Local STT option (A10), voice dictation (C21).
- **Constraints:** Model download on first use (~1-4GB depending on model size). GPU recommended for reasonable speed.
- **Ref:** [tech-sourcing #21](../tech-sourcing.md#21-speech-to-text-stt)

---

### 🔵 Custom Build Components (12 components)

| Component | Technology Used | Why Built | Ref |
|-----------|----------------|-----------|-----|
| **Three-Tier Architecture** | WPF (three window types) + shared `ChatThreadService` singleton | Unique interaction model — no library exists | [#32](../tech-sourcing.md#32-three-tier-interaction-architecture) |
| **Message Branching Model** | SQLite (recursive CTE) + custom C# queries | Unique data model — no off-the-shelf library | [#31](../tech-sourcing.md#31-message-branching-data-model) |
| **Wiki Indexing Engine** | Markdig (AST walker) + SQLite index tables | Custom indexing logic is the product's secret sauce | [#13](../tech-sourcing.md#13-wiki-indexing--cross-linking-engine) |
| **HWND Capture & Text Injection** | P/Invoke (`GetForegroundWindow`, `GetWindowText`) + UI Automation (`ValuePattern`, `TextPattern`) + `SendMessage` (`WM_SETTEXT`) + `SendInput` (Ctrl+V) | Inherently Windows-specific | [#6](../tech-sourcing.md#6-hwnd-capture--text-injection-spatial-anchoring) |
| **Global Keyboard Hooks** | P/Invoke (`RegisterHotKey`, `SetWindowsHookEx` with `WH_KEYBOARD_LL`) | Thin wrapper (~5 functions) — too simple for a library | [#5](../tech-sourcing.md#5-global-keyboard-hooks) |
| **Tool Use Orchestration** | `System.Diagnostics.Process` (terminal), `HttpClient` (web search), `System.IO` (file ops) | Function-calling loop + confirmation workflow is custom | [#16-19](../tech-sourcing.md#16-web-search-integration-tool-use-h1) |
| **LLM Provider Abstraction** | `ILLMProvider` interface + per-provider adapters wrapping vendor SDKs | Thin glue code — SDKs handle heavy lifting | [#3](../tech-sourcing.md#3-llm-provider-http-client) |
| **Chat Import Parsers** | `System.Text.Json` | Format-specific parsing (~200 lines each) — too niche for a library | [#29](../tech-sourcing.md#29-chat-import-parsing-chatgpt--claude) |
| **Deep Research Pipeline** | Custom state machine (Plan→Search→Read→Synthesize→Report) driven by tool-use loop | Bounded workflow — no framework needed | [#19](../tech-sourcing.md#19-deep-research-orchestration-h6) |
| **Theming System** | WPF `ResourceDictionary` with `DynamicResource` references | Native WPF capability — no library needed | [#33](../tech-sourcing.md#33-theming--darklight-mode) |
| **Markdown → WPF Renderer** | Markdig (parse) + AvalonEdit highlighting engine (syntax colors) → WPF `FlowDocument` | Conversion logic is custom; progressive rendering during streaming is the hard part | [#4](../tech-sourcing.md#4-markdown--code-rendering-engine) |
| **Auto-Save Drafts** | `PeriodicTimer` + SQLite `MessageDrafts` table | Trivial — too simple for a library | [#35](../tech-sourcing.md#35-auto-save-drafts) |

---

### ⚪ Platform-Provided (10 capabilities)

| Capability | .NET/WPF API | What It Provides | Ref |
|-----------|-------------|-----------------|-----|
| **UI Framework** | WPF (Window, UserControl, DataTemplate, Style) | Complete desktop UI framework with data binding, styling, layout | [#1](../tech-sourcing.md#1-ui-framework--wpf-net-application-shell) |
| **API Key Encryption** | `System.Security.Cryptography.ProtectedData` (DPAPI) | Encrypt API keys at rest, tied to Windows user account | [#30](../tech-sourcing.md#30-encryption--api-keys--chat-locking) |
| **Full-Text Search** | SQLite FTS5 (`CREATE VIRTUAL TABLE`) | Ranked search, snippet extraction, highlight markup across chats and wiki | [#11](../tech-sourcing.md#11-full-text-search) |
| **Clipboard** | `System.Windows.Clipboard` | Read/write with multi-format support (text, HTML, RTF) | [#7](../tech-sourcing.md#7-clipboard-format-preservation) |
| **File System Watcher** | `System.IO.FileSystemWatcher` | Monitor wiki directory for external `.md` file changes | [#12](../tech-sourcing.md#12-file-system-watcher-wiki-monitoring) |
| **Video Playback** | WPF `MediaElement` | Inline video player with standard controls (play/pause/seek/volume) | [#24](../tech-sourcing.md#24-video-playback) |
| **Local WebSocket** | ASP.NET Core Kestrel (embedded) | Local WebSocket server on 127.0.0.1 for external integrations | [#8](../tech-sourcing.md#8-local-websocket-server) |
| **BiDi Text** | WPF `FlowDocument` / `FlowDirection` | Bidirectional text rendering — Hebrew RTL detection, mixed LTR/RTL | [#34](../tech-sourcing.md#34-bidirectional-text-rendering-hebrew-rtl) |
| **DPI Awareness** | `PerMonitorV2` (app.manifest) | Crisp rendering at any monitor scaling (100%-200%+) | [#36](../tech-sourcing.md#36-per-monitor-dpi-awareness) |
| **Process Execution** | `System.Diagnostics.Process` | Execute terminal commands, capture stdout/stderr | [#17](../tech-sourcing.md#17-terminal--shell-execution-tool-use-h2) |

---

### 🌐 SaaS / Cloud Services (2 components)

| Service | API / SDK | Purpose | Ref |
|---------|----------|---------|-----|
| **Web Search** | Google Custom Search API or Bing Web Search API | Execute web searches for AI tool-use (H1, H6). User brings own API key. | [#16](../tech-sourcing.md#16-web-search-integration-tool-use-h1) |
| **Google Cloud Storage** | `Google.Cloud.Storage.V1` SDK | Off-site backup of SQLite DB + wiki + artifacts. Scheduled or manual. | [#26](../tech-sourcing.md#26-backup-to-google-cloud-storage) |

---

## Runtime Dependencies Summary

| Dependency | Type | Approx. Size | Required |
|-----------|------|-------------|----------|
| .NET 8.0 Runtime | Platform | ~200MB (shared) | Yes |
| SQLite (via Microsoft.Data.Sqlite) | OSS NuGet | <5MB | Yes |
| EF Core | OSS NuGet | ~5MB | Yes |
| Markdig | OSS NuGet | <1MB | Yes |
| CommunityToolkit.Mvvm | OSS NuGet | <1MB | Yes |
| OpenAI SDK | OSS NuGet | ~2MB | Conditional (if OpenAI used) |
| Anthropic SDK | OSS NuGet | ~1MB | Conditional (if Anthropic used) |
| Google Cloud SDK | OSS NuGet | ~50MB | Conditional (if Gemini/GCS used) |
| SharpToken | OSS NuGet | ~5MB (with tokenizer data) | Yes |
| NAudio | OSS NuGet | ~2MB | Yes |
| DiffPlex | OSS NuGet | <1MB | Yes |
| LiveCharts2 | OSS NuGet | ~2MB | Yes |
| WeCantSpell.Hunspell | OSS NuGet | ~2MB (with dictionary) | Yes |
| QuestPDF | OSS NuGet | ~2MB | Yes |
| AutoUpdater.NET | OSS NuGet | <1MB | Yes |
| LibGit2Sharp | OSS NuGet | ~15MB (native binaries) | Yes |
| AForge.NET | OSS NuGet | ~1MB | Yes |
| Whisper.net | OSS NuGet | ~5MB runtime + 1-4GB model | Conditional (if local STT used) |
| ASP.NET Core Kestrel | Platform | ~10MB | Yes |
| **Total (all conditional)** | | **~90MB NuGet + ~200MB .NET runtime** | |

---

*Technology stack document — Batch 1 of planning/ directory. See also: [`architecture.md`](architecture.md), [`abstractions.md`](abstractions.md).*
