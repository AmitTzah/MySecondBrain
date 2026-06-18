# Diagnostics & Debug Logging — Feature Spec

## What the User Accomplishes
The user enables and configures structured diagnostic logging to troubleshoot LLM API calls, Tier 1 hotkey pipeline issues, database performance, wiki indexing, WebSocket connections, startup/shutdown sequencing, and system integration events. Logs are written as structured JSON via Serilog to rolling files in `%LOCALAPPDATA%\MySecondBrain\logs\`. The user can set the global log verbosity level, toggle individual logging categories on/off, open the logs folder in Windows Explorer, and clear all log files.

## Trigger
- Navigate to Settings → Diagnostics category
- Logging runs automatically in the background whenever the app is running — no manual start/stop

## Detailed Behavior

### A11a. Log Level Selector
- A dropdown at the top of the Diagnostics settings section:
  - **Information** (default) — Standard operational events. Normal app behavior.
  - **Debug** — Detailed diagnostic information useful for troubleshooting. Includes Information-level events plus detailed internal state.
  - **Verbose** — Maximum detail. Includes Debug-level events plus per-message internal tracing. May impact performance.
- Changes take effect immediately. No save button.
- The log level applies globally across ALL enabled categories. Categories cannot have individual log levels — the selector sets the minimum level for all enabled categories.

### A11b. Per-Category Toggles
Eight checkbox toggles, each controlling one logging category. Checked = category active, unchecked = category silent. Changes take effect immediately. Default states as specified below.

#### Category 1: LLM API Calls (Default: ON)
Logs the full lifecycle of every LLM API request.

**Request Logging:**
- System prompt (full text)
- Messages array (all messages in the request, including roles and content)
- Model identifier (e.g., `gpt-4o`, `claude-3-5-sonnet-20241022`)
- Temperature value
- Max tokens value
- Thinking toggle state (on/off)
- Provider name (OpenAI, Anthropic, Google, etc.)
- Timestamp of request dispatch

**API Key Handling:**
- API keys MUST be redacted. The log entry MUST show `"api_key": "[REDACTED]"` or equivalent.
- ⚠️ SECURITY REQUIREMENT: Under no circumstances may the raw API key appear in any log file. Serilog destructuring policies must enforce this.

**Response Logging:**
- Response content (full assistant message text)
- Finish reason (stop, length, tool_calls, content_filter, etc.)
- Token counts: prompt_tokens, completion_tokens, total_tokens
- Latency in milliseconds (time from request dispatch to first response byte, and total time to completion)
- Streaming: chunk count (number of streaming chunks received)
- HTTP status code (200, 401, 429, 500, etc.)
- HTTP error body (full response body on non-200 status codes)

#### Category 2: Tier 1 Hotkey Pipeline (Default: ON)
Logs the full Tier 1 text action pipeline from hotkey press to apply completion.

**Pipeline Stages Logged:**
1. **Hotkey Detected:** Which hotkey was pressed (e.g., `Alt+Q`), timestamp
2. **TextAction Loaded:** Name of the TextAction loaded (e.g., "Rewrite"), its capture scope flags, apply mode
3. **Capture Scope Execution:** Which capture scope flags were active, and for each:
   - `selection`: selection found (length in characters) or "no selection"
   - `focusedElement`: element type identified (e.g., "RichEdit20W"), captured text length
   - `surroundingContext`: context captured (character count before/after cursor)
   - `fullDocument`: document captured (total character count)
   - `screenshot`: screenshot captured (dimensions in pixels, file size in bytes)
4. **UIA Pattern Used:** Name of UIA pattern used for capture (e.g., `TextPattern`, `ValuePattern`, `HWND+SendMessage`)
5. **Content Captured:** First 200 characters of captured content (truncated with `"... [N more chars]"` if longer)
6. **AI Call Dispatched:** Model and provider used, token count of the prompt sent
7. **Response Received:** Token counts, latency
8. **Apply Mode Execution:**
   - `replaceSelection`: HWND injection attempted → success/failure. On failure: clipboard fallback used (yes/no)
   - `insertAtCursor`: injection method, success/failure
   - `replaceFocusedElement`: injection method, success/failure
   - `appendToFocusedElement`: injection method, success/failure
   - `prependToFocusedElement`: injection method, success/failure
   - `clipboardOnly`: clipboard set, format(s) used
   - `showOnly`: popup displayed, user action (Accept/Discard/Open in Studio/Save to Wiki/Retry)
9. **Final Outcome:** "success" or "failure" with reason if failed (e.g., "HWND injection failed — target window closed", "API error 429 — rate limited", "UIA pattern unavailable — element does not support TextPattern")

#### Category 3: Tier 2 Command Bar (Default: ON)
Logs the Tier 2 Command Bar lifecycle.

**Events Logged:**
- **Trigger:** `Alt+Space` detected, timestamp
- **Query Text:** The user's query text (full text)
- **AI Call Dispatched:** Model, provider, prompt tokens
- **Response Received:** Content length, token counts, latency
- **State Transitions:** Pop-out triggered (inline → popped-out), popped-out → inline, elevation to Studio triggered
- **Dismissal:** How dismissed (Escape, Close button, clicking away), whether conversation was saved as transient thread (yes/no, ChatThread ID if saved)

#### Category 4: Database (Default: OFF)
Logs database-related events for performance troubleshooting.

**Events Logged:**
- **Slow Queries:** Any query exceeding 100ms threshold. Logs: SQL statement text (first 500 chars, truncated if longer), duration in ms, operation type (read/write)
- **Migration Execution:** Each EF Core migration applied on startup. Logs: migration name, duration in ms
- **VACUUM Operations:** Logs: VACUUM start time, duration in ms, space before (bytes), space after (bytes), space reclaimed (bytes)
- **FTS5 Search:** Search terms, result count, search duration in ms

#### Category 5: Wiki & File System (Default: OFF)
Logs wiki file watcher and indexing events.

**Events Logged:**
- **File Watcher Events:** File create/modify/delete with full file path, event type, timestamp
- **Indexing Runs:** Trigger (startup, file change, manual "Re-index Now"), files scanned count, duration in ms, any errors encountered (file path + error message)
- **Git Auto-Commit:** Files changed count, commit hash (short), commit message, duration in ms. Push result (success/failure, error if failed)
- **Backup Operations (if applicable):** File count, total size (bytes), upload duration in ms

#### Category 6: WebSocket (Default: OFF)
Logs local WebSocket server events.

**Events Logged:**
- **Client Connections:** Client connected (client ID or IP), timestamp
- **Client Disconnections:** Client disconnected (client ID or IP), reason if known, timestamp
- **Message Counts:** Messages sent (count per interval or per connection), messages received
- **Auth Attempts:** Success or failure. Token value MUST be redacted — log shows `"token": "[REDACTED]"` only
- **Server Start/Stop:** WebSocket server started on port N, server stopped

#### Category 7: Startup & Shutdown (Default: OFF)
Logs application startup and shutdown sequencing for diagnosing slow starts or hang-on-exit issues.

**Events Logged:**
- **DI Container Build:** Duration in ms to build the dependency injection container
- **Migration Application:** Each migration applied: name, duration in ms
- **Service Initialization:** Key services initialized in order, each with per-service timing in ms. Services logged: Database context, Wiki file watcher, Global hotkey registration, WebSocket server, System tray, Auto-update check, Backup scheduler, Auto-cleanup scheduler
- **Startup Complete:** Total startup time in ms
- **Shutdown Sequence:** Graceful shutdown: services stopped in reverse initialization order, each with timing. Logs: "Shutdown initiated" → per-service stopped → "Shutdown complete"

#### Category 8: System Integration (Default: OFF)
Logs Windows OS integration events.

**Events Logged:**
- **Global Hotkey Registration:** Success or failure per hotkey. On failure: "Conflict detected — [hotkey] already registered by [process name if detectable]"
- **System Tray Interactions:** Left-click (restore window), right-click menu item selected (which item)
- **Clipboard Operations:** Capture — format detected (Text, HTML, RTF, Image, etc.), size (character count or bytes). Restoration — format written, success/failure
- **DPI Change Events:** DPI changed: from [old] to [new], which monitor, timestamp
- **Screenshot Operations:** Window title captured, screenshot dimensions (width×height), file size (bytes), success or failure with reason

### A11c. Open Logs Folder Button
- Button labeled "Open Logs Folder"
- Clicking opens `%LOCALAPPDATA%\MySecondBrain\logs\` in Windows Explorer
- If the logs folder does not exist yet (first launch, logs never written), the folder is created first, then opened

### A11d. Clear Logs Button
- Button labeled "Clear Logs"
- Clicking shows a confirmation dialog: "Delete all log files in the logs folder? This action cannot be undone."
- On confirm: all `.log` and `.json` files in `%LOCALAPPDATA%\MySecondBrain\logs\` are deleted
- The currently active log file (being written to by Serilog) is also deleted. Serilog will create a new log file on the next log event.
- On cancel: no action taken
- After clearing: the logs folder remains (empty). A new log file is created when the next loggable event occurs.
- If the logs folder is inaccessible or files are locked: error toast "Could not clear all log files. [N] files could not be deleted. Close other applications that may be accessing the logs and try again."

### Log File Format & Infrastructure
- **Logging Infrastructure:** Serilog (already built in W1.3 — rolling file sink, JSON structured output)
- **File Location:** `%LOCALAPPDATA%\MySecondBrain\logs\`
- **File Naming:** `mysecondbrain-{Date}.json` (or as configured by Serilog rolling file sink)
- **Format:** Structured JSON. Each log entry is a JSON object with: timestamp, level, category, message, and any structured properties
- **Rolling:** Serilog rolling file sink handles file rotation. Default: one file per day, retained for 30 days
- **API Key Redaction:** Enforced via Serilog destructuring policy. The `ApiKey` property type must be registered with a destructuring policy that replaces the value with `"[REDACTED]"`. This applies to ALL log categories, not just LLM API Calls.

## Data
- Log settings stored in SQLite settings database:
  - `LogLevel` (string: "Information" | "Debug" | "Verbose", default: "Information")
  - `LogCategory_LLMApiCalls` (bool, default: true)
  - `LogCategory_Tier1HotkeyPipeline` (bool, default: true)
  - `LogCategory_Tier2CommandBar` (bool, default: true)
  - `LogCategory_Database` (bool, default: false)
  - `LogCategory_WikiFileSystem` (bool, default: false)
  - `LogCategory_WebSocket` (bool, default: false)
  - `LogCategory_StartupShutdown` (bool, default: false)
  - `LogCategory_SystemIntegration` (bool, default: false)
- Log files on disk: structured JSON in `%LOCALAPPDATA%\MySecondBrain\logs\`

## Success/Failure States
- **Logging Active:** No user-visible indication. Background operation. Log files grow in the logs folder.
- **Log Level Changed:** New minimum level takes effect immediately. Log events below the threshold are silently dropped.
- **Category Toggled:** Enabled categories begin logging immediately. Disabled categories stop logging immediately. Already-written log entries are not affected.
- **Logs Folder Opened:** Windows Explorer opens at the logs directory.
- **Logs Cleared:** All log files deleted. Confirmation toast: "All log files cleared." New log file created on next event.
- **Logs Folder Inaccessible:** If `%LOCALAPPDATA%\MySecondBrain\logs\` cannot be created or accessed (permissions, disk error), logging falls back to a minimal in-memory buffer. Warning shown in Settings → Diagnostics: "⚠️ Log directory is not accessible. Logging to memory buffer only (last 1000 events)."

## Permissions
- Single-user app. No role-based permissions. Log settings are accessible to the sole user.
- Log files are stored in the user's `%LOCALAPPDATA%` — no admin privileges required.

## Interactions with Other Features
- **LLM API Calls (Category 1)** interacts with all AI calls across Studio Chat (C), Tier 1 (K3), Tier 2 (K4), Model Comparison (M), Deep Research (H6), and any other feature that calls a provider API
- **Tier 1 Hotkey Pipeline (Category 2)** interacts with Text Actions (K1, K3), Windows OS Integration (P1-P4), HWND Capture (P2)
- **Tier 2 Command Bar (Category 3)** interacts with Command Bar (K4)
- **Database (Category 4)** interacts with Data Model & Lifecycle (O), all SQLite operations
- **Wiki & File System (Category 5)** interacts with Personal Wiki (N1-N14), Backup & Recovery (R)
- **WebSocket (Category 6)** interacts with Local WebSocket Server (P5)
- **Startup & Shutdown (Category 7)** interacts with Startup Behavior (A6), DI container, all service initialization
- **System Integration (Category 8)** interacts with Global Hotkeys (P1), System Tray (P6), Clipboard (P4), DPI Awareness (P8), Screenshots (T4)
- **Settings:** Diagnostics settings are part of the Settings screen (A1, A11)
