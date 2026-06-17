# Settings & Configuration — Feature Spec

## What the User Accomplishes
The user configures all global application settings from a single, organized settings screen. This includes AI providers, model profiles, appearance, wiki directory, backup, hotkeys, tools, language, notifications, startup behavior, updates, and maintenance.

## Trigger
User clicks "Settings" from the Studio sidebar, system tray context menu, or the onboarding wizard on first launch.

## Detailed Behavior

### A1. Global Settings Screen
A dedicated screen containing all global configuration options, organized into collapsible or tabbed sections:
- **Providers:** API key management (B1)
- **Profiles:** Personas and Model Configurations (B)
- **Appearance:** Visual themes, fonts (A3, A5)
- **Wiki:** Directory selection, indexing status (N1)
- **Backup:** Google Cloud Storage config, schedule (R)
- **Hotkeys:** Global hotkey assignments for Text Actions and Command Bar (K, P1)
- **Tools:** Tool auto-approval defaults (H5), STT provider (A10)
- **Language:** UI language preferences (Q)
- **Notifications:** Sound, streaming, per-chat mute defaults (A4)
- **Startup:** Launch on boot, session restore (A6)
- **Updates:** Auto-update configuration (A7)
- **Pricing:** Budget alerts, cost tracking (S5)
- **Security:** API key encryption status
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
- ⚠️ FLAGGED: Exact implementation of theme system (CSS variables vs WPF resource dictionaries) is an architectural decision.

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

## Data
- All settings stored in local SQLite database
- API keys encrypted via Windows DPAPI
- Settings changes take effect immediately (no "Save" button needed)

## Validations & Constraints
- Font size range: 10-24px
- Wiki directory must exist and be readable
- API keys validated via "Test Key" button (not just format check)
- Hotkey assignments must not conflict with existing system or app hotkeys

## Success/Failure States
- **Success:** Settings persist across app restarts
- **Failure — Invalid API Key:** Red error text: "Key validation failed. Check the key and try again."
- **Failure — Directory Not Found:** Red error text: "The selected directory does not exist or is not accessible."
- **Failure — Hotkey Conflict:** Warning: "This hotkey is already assigned to [action]. Reassign?"

## Permissions
- Single-user app — no role-based permissions. All settings accessible to the sole user.

## Interactions with Other Features
- A2 feeds into B4 (Persona selection for new chats)
- A3 feeds into C1 (conversation view rendering)
- A4 feeds into C4 (streaming), E4 (per-chat mute)
- A5 affects entire UI
- A6 affects P7 (session restore)
- A8 feeds into B1 (API keys), B3 (Personas), N1 (wiki directory), K3 (hotkeys)
- A10 feeds into C21 (voice dictation)
