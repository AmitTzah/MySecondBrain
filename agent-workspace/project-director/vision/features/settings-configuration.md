# Settings & Configuration — Feature Spec

## What the User Accomplishes

The user configures all global application settings from a single, organized settings screen. This includes AI providers, model profiles, appearance, wiki directory, backup, hotkeys, tools, skills, memory, language, notifications, startup behavior, updates, diagnostics, and maintenance.

## Trigger

User clicks "Settings" from the Studio sidebar, system tray context menu, or the onboarding wizard on first launch.

## Detailed Behavior

### A1. Global Settings Screen

A dedicated screen containing all global configuration options, organized into 19 collapsible or tabbed sections:
- **Providers:** API key management (B1)
- **Profiles:** Personas and Model Configurations (B)
- **Appearance:** Visual themes, fonts (A3, A5)
- **Wiki:** Directory selection, indexing status (N1)
- **Backup:** Google Cloud Storage config, schedule (R)
- **Text Actions:** Create, edit, delete, reorder Text Actions with capture scope and apply mode (K1). Hotkey assignment table (K, P1).
- **Hotkeys:** Global hotkey assignments for Text Actions and Command Bar (K, P1)
- **Tools:** Tool enable/disable defaults and auto-approval for 14 tools: read_file, list_files, search_files, apply_diff, write_to_file, bash, web_search, web_fetch, image_search, wiki_search, memory, skill_load, ask_user_input, present_files (H). Out-of-workspace read access configurable per read-tool (Auto-Approve / Ask / Disabled).
- **Skills:** Built-in skills list with individual enable/disable toggles + community skills discovered at `%LOCALAPPDATA%/MySecondBrain/skills/` (A12, W)
- **Memory:** View, edit, delete AI-stored memories. "Clear All Memories." Memory storage size displayed (A13, W8)
- **Language:** UI language preferences (Q)
- **Notifications:** Sound, streaming, per-chat mute defaults (A4)
- **Startup:** Launch on boot, session restore (A6)
- **Updates:** Auto-update configuration (A7)
- **Pricing:** Budget alerts, cost tracking (S5)
- **Security:** API key encryption status
- **Diagnostics:** Debug logging configuration — log level, 8 per-category toggles, open logs folder, clear logs (A11)
- **System Info:** Comprehensive reference of all app data locations — paths, purposes, sizes, editability. Also accessible from "?" icon in app header (Z, new)
- **Maintenance:** Database compaction (A9)

### A2. Default Profile Selection

- User selects which Persona is auto-assigned to new chats
- Dropdown of all configured Personas
- "Last Used" option: new chats default to the most recently used Persona
- Change takes effect immediately for all subsequent new chats

### A3. Appearance Settings

- **Chat Visual Theme:** Dropdown with Classic (user right, assistant left), Compact (minimal spacing), Bubble (chat-bubble style)
- **Font Family:** Text input or dropdown of system fonts
- **Font Size:** Numeric input with +/- stepper (range: 10-24px, default: 14px)
- **Font Weight:** Normal or Bold toggle
- Live preview area shows sample message rendering with current settings

### A4. Notification Settings

- **Sound on Completion:** Toggle. Plays system notification sound when assistant message completes streaming
- **Disable Streaming:** Toggle. When enabled, responses appear all at once when complete instead of token-by-token
- Both settings are global defaults; per-chat overrides exist in textbox toolbar (E4)

### A5. Dark Mode / Light Mode

- Toggle or dropdown: Dark Mode (default), Light Mode
- Affects entire application UI chrome (sidebar, headers, settings, etc.)
- Independent of chat visual themes (A3) which control message layout only
- WebView2 artifacts panel receives theme via JavaScript bridge

### A6. Startup Behavior

- **Launch on Windows Startup:** Toggle. Adds/removes registry entry or startup folder shortcut
- **Restore Last Session:** Toggle. On launch, reopens all chats and tabs from previous session. If disabled, opens with a blank new chat

### A7. Auto-Update

- **Check Frequency:** Radio: On Startup, Daily, Weekly, Manual Only
- **Notification:** When update available, non-intrusive banner in Studio with "Install Now" and "Later" options
- **Install:** Downloads and applies update. App restarts after installation

### A8. Onboarding Wizard

- Triggered on first launch (no existing settings detected)
- Steps: (1) Add first API key → (2) Create first Persona → (3) Select wiki directory → (4) Configure hotkeys
- Each step has a "Skip" button
- "Finish" button completes wizard and opens Studio with a new chat
- Re-launchable from Settings at any time

### A9. Database Maintenance

- **"Compact Database" Button:** Runs SQLite VACUUM operation
- **Before:** Displays current database file size
- **During:** Progress indicator (operation may take seconds to minutes depending on size)
- **After:** Displays new size and amount reclaimed
- **Error State:** If VACUUM fails, displays error message with suggestion to check disk space

### A10. Speech-to-Text (STT) Provider

- Separate from text-generation Model Configurations (B2)
- **Provider Selection:** Dropdown: OpenAI Whisper API, Local Whisper, OpenAI-Compatible STT
- **Model:** Model identifier for the selected provider
- **API Key:** Can reuse a text-generation key or specify a dedicated key
- **Test Microphone:** Button to verify STT is working with a short test recording

### A11. Diagnostics & Debug Logging

Configures structured diagnostic logging written via Serilog to rolling JSON files in `%LOCALAPPDATA%\MySecondBrain\logs\`. Full behavioral spec: [`features/diagnostics-debug-logging.md`](diagnostics-debug-logging.md).

- **Log Level Selector:** Dropdown: Information (default), Debug, Verbose. Applies globally across all enabled categories.
- **Per-Category Toggles:** Eight checkbox toggles controlling which subsystems are logged:
  1. **LLM API Calls** (default ON)
  2. **Tier 1 Hotkey Pipeline** (default ON)
  3. **Tier 2 Command Bar** (default ON)
  4. **Database** (default OFF)
  5. **Wiki & File System** (default OFF)
  6. **WebSocket** (default OFF)
  7. **Startup & Shutdown** (default OFF)
  8. **System Integration** (default OFF)
- **"Open Logs Folder" Button:** Opens `%LOCALAPPDATA%\MySecondBrain\logs\` in Windows Explorer.
- **"Clear Logs" Button:** Deletes all log files with confirmation dialog.

### A12. Skills Defaults

Configures global defaults for which skills are enabled in new chats. Full spec: [`features/agent-skills.md`](agent-skills.md).

- **Built-in Skills List:** 11 Anthropic skills with individual enable/disable toggles. Each shows: skill name, description, source (built-in), dependency status (Python/Node.js detected or not).
- **Community Skills:** Skills discovered at `%LOCALAPPDATA%/MySecondBrain/skills/`. Listed with `source: community` annotation.
- **"Enable All" / "Disable All":** Quick actions at top of list.
- **Skills Count:** "11 built-in, [N] community — [M] enabled"
- **Inheritance:** New chats inherit these defaults. Per-chat toolbar overrides are temporary.

### A13. Memory Management

Manages AI-stored memories (SQLite-backed, Anthropic `memory_20250818` schema). Full spec: [`features/agent-skills.md`](agent-skills.md) §W8.

- **Memory List:** Scrollable list of all stored memories. Each entry shows: key (fact identifier), value (fact content), source chat (clickable to open), timestamp.
- **Edit Memory:** Click entry → inline edit key + value. Save/Cancel.
- **Delete Memory:** X button per entry. Confirmation: "Delete this memory?"
- **"Clear All Memories" Button:** Deletes all memories with confirmation: "Delete all [N] memories? This cannot be undone."
- **Storage Size:** "Memory storage: [size] across [N] entries."
- **Per-Chat Default:** Toggle: "Enable memory for new chats by default." (Default: OFF). New chats inherit this as the initial state of the textbox toolbar "🧠 Mem" toggle.

### Z. System Info (NEW)

A comprehensive reference of every file and folder the app writes to or reads from on the user's computer. Full spec: [`features/app-data-locations.md`](app-data-locations.md).

- **App Data Locations Table:** Each entry shows: Location (file system path), Purpose (what it's for), Size on Disk (real-time), "User Can Edit?" badge (✅ Yes / ⚠️ Caution / ❌ No), "Open in Explorer" button.
- **Entries include:** Database file, logs directory, workspace directory, artifacts directory, skills directories (user + cross-client), wiki directory (user-configured), backup directory (user-configured), settings file.
- **Clear distinction:** App-managed (don't touch — cleaned on chat deletion), User-editable (wiki, skills).
- Also accessible from: "?" (Help) icon in the app header bar → "App Data Locations".

## Data

- All settings stored in local SQLite database
- API keys encrypted via Windows DPAPI
- Settings changes take effect immediately (no "Save" button needed)
- Memory entries stored in SQLite `MemoryEntries` table

## Validations & Constraints

- Font size range: 10-24px
- Wiki directory must exist and be readable
- API keys validated via "Test Key" button (not just format check)
- Hotkey assignments must not conflict with existing system or app hotkeys
- Memory edits validate key length (max 200 chars) and value size (max 10KB)

## Success/Failure States

- **Success:** Settings persist across app restarts
- **Failure — Invalid API Key:** Red error text: "Key validation failed. Check the key and try again."
- **Failure — Directory Not Found:** Red error text: "The selected directory does not exist or is not accessible."
- **Failure — Hotkey Conflict:** Warning: "This hotkey is already assigned to [action]. Reassign?"
- **Failure — Memory Edit Invalid:** "Key must be under 200 characters. Value must be under 10KB."

## Permissions

- Single-user app — no role-based permissions. All settings accessible to the sole user.

## Interactions with Other Features

- A2 feeds into B4 (Persona selection for new chats)
- A3 feeds into C1 (conversation view rendering)
- A4 feeds into C4 (streaming), E4 (per-chat mute)
- A5 affects entire UI including WebView2 artifacts panel (via JS bridge)
- A6 affects P7 (session restore)
- A8 feeds into B1 (API keys), B3 (Personas), N1 (wiki directory), K3 (hotkeys)
- A10 feeds into C21 (voice dictation)
- A12 feeds into W6 (per-chat skills toggle), W7 (system prompt construction)
- A13 manages W8 (memory tool storage)
- Tools section controls H (tool enable/disable + auto-approval for 14 tools, out-of-workspace read access per read-tool)
- A14 (System Info) references app data locations from [`features/app-data-locations.md`](app-data-locations.md)
