# Integration Points — MySecondBrain

## Overview

Every external integration point in MySecondBrain is wrapped behind a C# interface (defined in [`abstractions.md`](abstractions.md)). This document enumerates each integration — SaaS service, platform API, or Windows subsystem — and documents its abstraction, fallback strategy, and configuration requirements.

Integration points are organized into two categories:
- **SaaS / Cloud Integrations:** Remote APIs the app calls over the network
- **Platform Integrations:** Windows-specific subsystems accessed via .NET or P/Invoke

---

## SaaS / Cloud Integrations

### 1. OpenAI API (Chat, STT, Models)

| Aspect | Detail |
|--------|--------|
| **Service** | OpenAI API (`api.openai.com`) — Chat Completions, Whisper, Model list |
| **Abstraction** | [`ILLMProvider`](abstractions.md#illmprovider) (Chat), [`ISTTProvider`](abstractions.md#isttprovider) (Whisper) |
| **Implementation** | `OpenAIProvider` — wraps official `OpenAI` NuGet SDK |
| **Protocol** | HTTPS REST + SSE streaming |
| **Fallback** | Retry with exponential backoff (3 attempts). If persistent failure: show specific error message + Retry button (C14). User can switch to another provider for the same chat. |
| **Configuration** | `apiKey` (encrypted via DPAPI), no endpoint override needed for standard OpenAI. |
| **Vision Features** | B1, B2, B4, B7, C4, C5, C14, E1, E3, H, A10 |
| **Tech-Sourcing** | [#3](tech-sourcing.md#3-llm-provider-http-client), [#21](tech-sourcing.md#21-speech-to-text-stt) |

### 2. Anthropic API

| Aspect | Detail |
|--------|--------|
| **Service** | Anthropic Messages API (`api.anthropic.com`) |
| **Abstraction** | [`ILLMProvider`](abstractions.md#illmprovider) |
| **Implementation** | `AnthropicProvider` — wraps community `Anthropic.SDK` NuGet |
| **Protocol** | HTTPS REST + SSE streaming |
| **Fallback** | Same retry pattern as OpenAI. If community SDK becomes unmaintained, fallback to direct `HttpClient` + `System.Text.Json` against Anthropic REST API. |
| **Configuration** | `apiKey` (encrypted via DPAPI). |
| **Vision Features** | B1, B2, B4, E3 (thinking tokens) |
| **Tech-Sourcing** | [#3](tech-sourcing.md#3-llm-provider-http-client) |

### 3. Google Gemini API

| Aspect | Detail |
|--------|--------|
| **Service** | Google Cloud Vertex AI / Gemini API |
| **Abstraction** | [`ILLMProvider`](abstractions.md#illmprovider) |
| **Implementation** | `GoogleProvider` — wraps `Google.Cloud.AIPlatform.V1` SDK |
| **Protocol** | HTTPS REST + streaming (gRPC or REST) |
| **Fallback** | Same retry pattern. Google SDK is large (~50MB) but official and well-maintained. |
| **Configuration** | `apiKey` or service account credentials (encrypted via DPAPI). |
| **Vision Features** | B1, B2 |
| **Tech-Sourcing** | [#3](tech-sourcing.md#3-llm-provider-http-client) |

### 4. OpenAI-Compatible Endpoints (DeepSeek, Mistral, MiMo, Moonshot, Local Models)

| Aspect | Detail |
|--------|--------|
| **Service** | Any API implementing OpenAI's Chat Completions protocol (DeepSeek, Mistral, local Ollama, LM Studio, etc.) |
| **Abstraction** | [`ILLMProvider`](abstractions.md#illmprovider) |
| **Implementation** | `OpenAICompatibleProvider` — uses `HttpClient` + `System.Text.Json` to emulate the OpenAI protocol against a user-configured endpoint. |
| **Protocol** | HTTPS REST (or HTTP for localhost) + SSE streaming |
| **Fallback** | Same retry pattern. For local endpoints (localhost), different error handling: "Is the local model server running?" message. |
| **Configuration** | `customProviderName` (display), `customEndpointUrl` (full URL), optional `apiKey`. |
| **Vision Features** | B5, B6, B7 |
| **Tech-Sourcing** | [#3](tech-sourcing.md#3-llm-provider-http-client) |

### 5. Google Cloud Storage (Backup)

| Aspect | Detail |
|--------|--------|
| **Service** | Google Cloud Storage — off-site backup of SQLite DB + wiki `.md` files + artifacts |
| **Abstraction** | [`IBackupProvider`](abstractions.md#ibackupprovider) |
| **Implementation** | `GcsBackupProvider` — wraps `Google.Cloud.Storage.V1` SDK |
| **Protocol** | HTTPS REST (resumable uploads for large files) |
| **Fallback** | [`LocalFolderBackupProvider`](abstractions.md#ibackupprovider) — copies backup archive to user-specified local folder. Zero external dependency. Also serves as the backup target if GCS is not configured. |
| **Configuration** | GCS bucket name, service account JSON key file (encrypted via DPAPI). Backup schedule: daily/weekly/manual. |
| **Vision Features** | R1, R2, R3, R4 |
| **Tech-Sourcing** | [#26](tech-sourcing.md#26-backup-to-google-cloud-storage) |

### 6. Web Search API (Google Custom Search / Bing)

| Aspect | Detail |
|--------|--------|
| **Service** | Google Custom Search API or Bing Web Search API |
| **Abstraction** | [`ISearchProvider`](abstractions.md#isearchprovider) |
| **Implementation** | `GoogleCustomSearchProvider` or `BingSearchProvider` |
| **Protocol** | HTTPS REST (GET with query params) |
| **Fallback** | If one provider fails, try the other (if both configured). If neither is configured, web search tool is disabled in the UI (grayed out). The AI is informed that web search is unavailable so it doesn't attempt tool calls. |
| **Configuration** | Google: API key + Search Engine ID. Bing: API key (Azure marketplace). Both encrypted via DPAPI. |
| **Vision Features** | H1, H6 |
| **Tech-Sourcing** | [#16](tech-sourcing.md#16-web-search-integration-tool-use-h1) |

### 7. GitHub (Wiki Git Push)

| Aspect | Detail |
|--------|--------|
| **Service** | GitHub.com — remote push for wiki git repository |
| **Abstraction** | `IWikiGitService` (referenced in `IWikiService`, defined in [`abstractions.md`](abstractions.md)) |
| **Implementation** | LibGit2Sharp for all git operations (`git init`, `add`, `commit`, `push`, `remote`) |
| **Protocol** | HTTPS (git smart protocol) |
| **Fallback** | Git push is optional. If GitHub PAT is not configured, only local git version control is available (still valuable). If push fails (network, auth), show non-blocking error notification. Auto-commit continues locally. |
| **Configuration** | GitHub Personal Access Token (encrypted via DPAPI), repo name, branch. PAT scope: repo-only (minimal). |
| **Vision Features** | Onboarding Step 3, N wiki VC |
| **Tech-Sourcing** | [#28](tech-sourcing.md#28-git-integration-wiki-version-control) |

### 8. Auto-Update Feed

| Aspect | Detail |
|--------|--------|
| **Service** | Remote HTTPS server hosting update feed (JSON/XML) + MSIX installer packages |
| **Abstraction** | [`IUpdateChecker`](abstractions.md#iupdatechecker) |
| **Implementation** | `AutoUpdaterDotNet` or `MsixAppInstallerUpdater` |
| **Protocol** | HTTPS (GET for feed check, GET for download) |
| **Fallback** | If update server unreachable: silent skip (no error to user). Next check per schedule. If download fails mid-way: resume support via HTTP Range requests. Manual check always available in Settings. |
| **Configuration** | `UpdateFeedUrl` (hardcoded or configurable). Check frequency: startup/daily/weekly/manual. |
| **Vision Features** | A7 |
| **Tech-Sourcing** | [#27](tech-sourcing.md#27-auto-update-mechanism) |

---

## Platform Integrations

### 9. Windows DPAPI (Encryption)

| Aspect | Detail |
|--------|--------|
| **API** | `System.Security.Cryptography.ProtectedData` (DPAPI) |
| **Scope** | `DataProtectionScope.CurrentUser` — tied to Windows user account |
| **Used For** | API keys (ApiKey.keyValue), GCS service account key, GitHub PAT, locked chat AES key wrapping |
| **Abstraction** | [`IEncryptionService`](abstractions.md#iencryptionservice) — thin wrapper around `ProtectedData.Protect()`/`Unprotect()`. Full interface contract in abstractions.md §13. |
| **Fallback** | If DPAPI fails (corrupted user profile): keys are irrecoverable. This is by design — DPAPI is tied to the Windows account. User must re-enter API keys. Clear error message: "Unable to access encrypted credentials. Your Windows user profile may be corrupted. Please re-enter your API keys." |
| **Configuration** | None. Zero-config. |
| **Vision Features** | B1, C31, R1, Flag #11 |
| **Tech-Sourcing** | [#30](tech-sourcing.md#30-encryption--api-keys--chat-locking) |

### 10. AES-256-GCM (Locked Chat Encryption)

| Aspect | Detail |
|--------|--------|
| **API** | `System.Security.Cryptography.AesGcm` (.NET 8+) |
| **Key Derivation** | `Rfc2898DeriveKey` (PBKDF2) with configurable iterations |
| **Used For** | Per-chat message encryption (C31). Salt stored alongside ciphertext. |
| **Abstraction** | `IChatEncryptionService` (service-level) |
| **Fallback** | Password lost → permanent lockout. No recovery mechanism. UI must clearly warn: "This chat is encrypted. If you lose the password, the content cannot be recovered." |
| **Configuration** | Global default password (optional) + per-chat password override. Iteration count configurable. Salt: 128-bit random per chat. |
| **Vision Features** | C31, Flag #10 |
| **Tech-Sourcing** | [#30](tech-sourcing.md#30-encryption--api-keys--chat-locking) |

### 11. Windows Clipboard

| Aspect | Detail |
|--------|--------|
| **API** | `System.Windows.Clipboard` (WPF) |
| **Used For** | Reading clipboard during Tier 1 capture (text + format detection), writing AI results back (multi-format: HTML, RTF, plain text), Copy MD/Copy Rich per message (C6), Tier 1 Apply fallback (Ctrl+V simulation) |
| **Abstraction** | `IClipboardService` (service-level) |
| **Fallback** | `GetDataObject()` may throw if clipboard is locked by another process. Retry up to 3 times with 100ms delay. If clipboard is empty/unavailable, Tier 1 capture falls back to reading selected text via UI Automation (if available). Apply fallback chain: UIA ValuePattern → WM_SETTEXT → clipboard Ctrl+V. |
| **Configuration** | None. |
| **Vision Features** | K3, C6, P4 |
| **Tech-Sourcing** | [#7](tech-sourcing.md#7-clipboard-format-preservation) |

### 12. FileSystemWatcher (Wiki Monitoring)

| Aspect | Detail |
|--------|--------|
| **API** | `System.IO.FileSystemWatcher` (.NET) |
| **Used For** | Monitor wiki directory for external `.md` file changes (added, edited, deleted). Triggers wiki re-indexing. |
| **Abstraction** | `IWikiFileWatcher` (service-level, consumed by `IWikiService`) |
| **Fallback** | FileSystemWatcher has known reliability issues on network drives and some edge cases (buffer overflow on rapid changes). Fallback: polling-based directory scanner (`Directory.EnumerateFiles` + last-modified check) that runs every 30 seconds as a safety net. Debounce: accumulate changes over 500ms windows before triggering re-index. |
| **Configuration** | Wiki directory path (set during onboarding, changeable in Settings). |
| **Vision Features** | N1, N2, Flag #14 |
| **Tech-Sourcing** | [#12](tech-sourcing.md#12-file-system-watcher-wiki-monitoring) |

### 13. Embedded WebSocket Server (Kestrel)

| Aspect | Detail |
|--------|--------|
| **API** | ASP.NET Core Kestrel (embedded, in-process) |
| **Used For** | Local WebSocket server on `127.0.0.1` for external integrations (Word Add-in). Token-based authentication. |
| **Abstraction** | `ILocalWebSocketServer` (service-level). Protocol: JSON messages over WebSocket. |
| **Fallback** | If port is occupied: try next port (configurable range). Display actual port in Settings. If Kestrel fails to start entirely: external integrations unavailable. Non-blocking — app continues without WebSocket. Notification in status bar. |
| **Configuration** | Configurable port (default: auto-select from range). Auth token (auto-generated on first run, display in Settings, regeneratable). |
| **Vision Features** | P5, Flag #5 |
| **Tech-Sourcing** | [#8](tech-sourcing.md#8-local-websocket-server) |

### 14. System Tray Icon

| Aspect | Detail |
|--------|--------|
| **API** | `System.Windows.Forms.NotifyIcon` (WinForms interop from WPF) |
| **Used For** | Minimize to system tray. Left-click restores. Right-click context menu. Visual indicator when AI is generating. |
| **Abstraction** | `ISystemTrayService` (service-level) |
| **Fallback** | NotifyIcon requires a message pump. WinForms interop provides this. If interop fails (rare): app minimizes to taskbar instead of tray. System tray features unavailable but app remains fully functional. |
| **Configuration** | "Minimize to tray on close" toggle (A6). "Show generation indicator" toggle. |
| **Vision Features** | P6 |
| **Tech-Sourcing** | [#9](tech-sourcing.md#9-system-tray-integration) |

### 15. Global Keyboard Hooks

| Aspect | Detail |
|--------|--------|
| **API** | `RegisterHotKey` (primary) + `SetWindowsHookEx` with `WH_KEYBOARD_LL` (fallback) — P/Invoke |
| **Used For** | System-wide hotkey detection: Alt+Q/W/E/R for Tier 1 Text Actions, Alt+Space for Tier 2 Command Bar |
| **Abstraction** | `IGlobalHotkeyService` (service-level) |
| **Fallback** | Primary: `RegisterHotKey` (kernel-level, reliable, less AV suspicion). Fallback: `WH_KEYBOARD_LL` (low-level hook) for key combos that `RegisterHotKey` cannot register (e.g., some multi-modifier combos). If hook is blocked by AV: show notification with guidance to whitelist the app. Code signing mitigates AV false positives. |
| **Configuration** | Hotkey assignments per TextAction (K1). Configurable in Settings → Hotkeys. Conflict detection on assignment. |
| **Vision Features** | K3, P1, P3, Flag #6 |
| **Tech-Sourcing** | [#5](tech-sourcing.md#5-global-keyboard-hooks) |

### 16. HWND Capture & UI Automation (Text Capture & Injection)

| Aspect | Detail |
|--------|--------|
| **API** | `GetForegroundWindow`, `GetWindowText`, `GetWindowThreadProcessId` (P/Invoke Win32). `System.Windows.Automation` (UIA): `TextPattern`, `ValuePattern`, `TreeWalker`, `DocumentRange`. `SendMessage` with `WM_SETTEXT`. `SendInput` for Ctrl+V. Win32 `PrintWindow`/`BitBlt` for screenshot capture. |
| **Used For** | Tier 1: multi-scope capture per TextAction's `captureScope` flags (selection, focused element, surrounding context, full document, screenshot) + HWND/source app name/document title. Tier 1 Apply: push AI-transformed text back into source window per TextAction's `applyMode`. |
| **Abstraction** | `IHwndCaptureService` + `ITextInjectionService` (service-level) |
| **Fallback** | **Capture Pipeline (graduated, per captureScope flags):** (1) TextPattern for `selection` → clipboard fallback. (2) ValuePattern for `focusedElement`. (3) TreeWalker for `surroundingContext` (parent/sibling elements). (4) DocumentRange/full tree traversal for `fullDocument`. (5) Win32 PrintWindow/BitBlt for `screenshot`. Clipboard restored after clipboard-based capture. **Apply:** per applyMode — `replaceSelection`: HWND injection → clipboard+Ctrl+V. `insertAtCursor`: UIA TextPattern → clipboard. `replaceFocusedElement`: UIA ValuePattern → clipboard+Ctrl+A, Ctrl+V. `appendToFocusedElement`/`prependToFocusedElement`: UIA ValuePattern → clipboard. `clipboardOnly`: clipboard only. `showOnly`: no injection. |
| **Configuration** | None. Capture scope and apply mode are per-TextAction configuration. Injection method auto-detected per target window. |
| **Vision Features** | K3, P2, P3, P9 |
| **Tech-Sourcing** | [#6](tech-sourcing.md#6-hwnd-capture--text-injection-spatial-anchoring) |

### 17. Local Tokenizer (SharpToken)

| Aspect | Detail |
|--------|--------|
| **API** | `SharpToken` NuGet (C# port of tiktoken) |
| **Used For** | Real-time token counting as user types (C11), pre-send context window validation, usage record token counts |
| **Abstraction** | [`ITokenizer`](abstractions.md#itokenizer) + [`ITokenizerFactory`](abstractions.md#itokenizerfactory) |
| **Fallback** | For providers without a SharpToken-compatible tokenizer (Anthropic, Google): use `FallbackTokenizer` — character count / 4 approximation (off by 30-40%). Usage records capture API-reported token counts from response headers when available, which are authoritative. The fallback is only used for real-time pre-send estimation. |
| **Configuration** | None. Tokenizer selected automatically per model via `ITokenizerFactory`. |
| **Vision Features** | C11, S |
| **Tech-Sourcing** | [#10](tech-sourcing.md#10-local-tokenization-real-time-token-counting) |

### 18. Speech-to-Text (STT)

| Aspect | Detail |
|--------|--------|
| **API** | OpenAI Whisper API (cloud) or Whisper.net / Whisper.cpp (local) or Windows `System.Speech` (built-in) |
| **Used For** | Voice dictation in Studio textbox (C21) |
| **Abstraction** | [`ISTTProvider`](abstractions.md#isttprovider) |
| **Fallback** | Provider selection in Settings (A10). Three-tier fallback: OpenAI Whisper (highest accuracy) → Local Whisper (privacy, offline) → Windows Speech (zero-cost, built-in). If selected provider fails, show error with option to switch to another. |
| **Configuration** | Provider selection. For local Whisper: model size (tiny/base/small/medium/large). Model auto-downloaded on first use (~1-4GB). |
| **Vision Features** | C21, A10 |
| **Tech-Sourcing** | [#21](tech-sourcing.md#21-speech-to-text-stt) |

### 19. Audio Recording & Playback

| Aspect | Detail |
|--------|--------|
| **API** | `NAudio` NuGet |
| **Used For** | Microphone recording for STT dictation. Playback of AI-generated audio (G5) and user-uploaded audio (G6). |
| **Abstraction** | `IAudioService` (service-level — wraps NAudio for simpler consumption) |
| **Fallback** | If no microphone detected: voice dictation button grayed out with tooltip "No microphone found." If audio device changes mid-recording: stop recording, save captured data to that point. If playback device unavailable: show error toast. |
| **Configuration** | Microphone device selection (default: system default). |
| **Vision Features** | C21, G5, G6 |
| **Tech-Sourcing** | [#22](tech-sourcing.md#22-audio-recording--playback) |

### 20. Webcam Capture

| Aspect | Detail |
|--------|--------|
| **API** | `AForge.NET` (`AForge.Video.DirectShow`) |
| **Used For** | Webcam photo capture (C22). Live preview + "Capture" button. |
| **Abstraction** | `ICameraService` (service-level) |
| **Fallback** | If no webcam detected: camera button grayed out with tooltip. If webcam in use by another app: show "Webcam is in use by another application." If AForge.NET fails: fallback to `Emgu.CV` (OpenCV wrapper) if available. |
| **Configuration** | Camera device selection (default: system default). |
| **Vision Features** | C22 |
| **Tech-Sourcing** | [#23](tech-sourcing.md#23-webcam-capture) |

### 21. Video Playback

| Aspect | Detail |
|--------|--------|
| **API** | WPF `MediaElement` (primary). `LibVLCSharp` (fallback for codec support). |
| **Used For** | Inline video player in chat messages (G6) |
| **Abstraction** | `IVideoPlayerService` (service-level) |
| **Fallback** | MediaElement handles common formats (MP4, WMV, AVI). If a format fails: try LibVLCSharp. If both fail: show "Unable to play this video format" with "Open in external player" button. |
| **Configuration** | None. |
| **Vision Features** | G6, T5 (future) |
| **Tech-Sourcing** | [#24](tech-sourcing.md#24-video-playback) |

### 22. Spell Check Engine

| Aspect | Detail |
|--------|--------|
| **API** | `WeCantSpell.Hunspell` NuGet |
| **Used For** | Red squiggly underline for misspelled words in textbox (C34). Right-click suggestions. "Add to Dictionary." |
| **Abstraction** | `ISpellCheckService` (service-level) |
| **Fallback** | If dictionary file missing/corrupted: disable spell check, show "Spell check unavailable — dictionary not found" in Settings. User can re-download. |
| **Configuration** | English dictionary (default, ~500KB). Toggle on/off per Settings. Custom dictionary (user-added words) stored in SQLite. |
| **Vision Features** | C34 |
| **Tech-Sourcing** | [#25](tech-sourcing.md#25-spell-checking) |

### 23. Git (Wiki Version Control)

| Aspect | Detail |
|--------|--------|
| **API** | `LibGit2Sharp` NuGet (in-process git) |
| **Used For** | Git init in wiki directory, auto-commit on file change (debounced 5s), optional GitHub remote push |
| **Abstraction** | `IWikiGitService` (consumed by `IWikiService` in [`abstractions.md`](abstractions.md)) |
| **Fallback** | If git init fails (permissions, disk space): git version control disabled for this wiki. Wiki snapshots (N6) remain available as undo mechanism. Auto-commit debouncing (Flag #14): buffer changes for 5s after last file modification. Do not commit while FileSystemWatcher shows file still being written (check `lastWriteTime` stability). |
| **Configuration** | GitHub PAT (encrypted via DPAPI), repo URL, branch name. Auto-commit: enabled/disabled. |
| **Vision Features** | Onboarding Step 3, Flag #11, #13, #14 |
| **Tech-Sourcing** | [#28](tech-sourcing.md#28-git-integration-wiki-version-control) |

---

## Integration Dependency Map

```
External World                            MySecondBrain
─────────────                            ─────────────
                                        
OpenAI API ───────────► ILLMProvider (OpenAIProvider)
Anthropic API ────────► ILLMProvider (AnthropicProvider)
Google Gemini ────────► ILLMProvider (GoogleProvider)
OpenAI-Compatible ────► ILLMProvider (OpenAICompatibleProvider)
Google GCS ───────────► IBackupProvider (GcsBackupProvider)
Google/Bing Search ───► ISearchProvider (GoogleCustomSearch / BingSearchProvider)
GitHub ───────────────► IWikiGitService (LibGit2Sharp)
Update Feed ──────────► IUpdateChecker (AutoUpdaterDotNet)
                                        
Windows DPAPI ─────────► IEncryptionService (ProtectedData)
AES-256-GCM (.NET) ───► IChatEncryptionService
Clipboard (.NET) ─────► IClipboardService
FileSystemWatcher ────► IWikiFileWatcher
Kestrel ──────────────► ILocalWebSocketServer
NotifyIcon ───────────► ISystemTrayService
RegisterHotKey ───────► IGlobalHotkeyService
UIA/Win32 (TextPattern, ValuePattern, TreeWalker, DocumentRange, PrintWindow/BitBlt) ──► IHwndCaptureService + ITextInjectionService
SharpToken ───────────► ITokenizer / ITokenizerFactory
Whisper API/.net ─────► ISTTProvider
NAudio ───────────────► IAudioService
AForge.NET ───────────► ICameraService
MediaElement/VLC ─────► IVideoPlayerService
Hunspell ─────────────► ISpellCheckService
LibGit2Sharp ─────────► IWikiGitService
```

---

## Failure Mode Summary

| Integration | Failure Impact | User Experience |
|-------------|---------------|-----------------|
| LLM Provider (any) | Cannot send/receive AI messages | Specific error + Retry button (C14). User can switch provider. |
| GCS Backup | Backup cannot upload | Local folder backup available. Non-blocking error toast. |
| Web Search | Tool-use web search unavailable | Tool disabled in UI. AI informed so it won't attempt. |
| GitHub Push | Remote push unavailable | Local git commits continue. Non-blocking notification. |
| Auto-Update Feed | Cannot check for updates | Silent skip. Manual check always available. |
| DPAPI | Cannot encrypt/decrypt keys | Keys irrecoverable. User must re-enter. |
| Clipboard | Tier 1 capture/apply degraded | Fallback chain: UIA → WM_SETTEXT → Ctrl+V. |
| FileSystemWatcher | Wiki index stale | Polling fallback every 30s. |
| Kestrel | External integrations unavailable | App continues. Status bar notification. |
| Global Hotkeys | Tier 1/2 hotkeys non-functional | AV whitelist guidance. App usable via Studio. |
| SharpToken | Pre-send token counts inaccurate | Fallback to chars/4 estimation. API counts still authoritative. |
| STT Provider | Voice dictation unavailable | Button grayed out. Switch provider option. |

---

*Integration points document — Batch 2 of planning/ directory. See also: [`abstractions.md`](abstractions.md), [`tech-stack.md`](tech-stack.md).*
