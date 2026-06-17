# Windows OS Integration — Feature Spec

## What the User Accomplishes
The app deeply integrates with Windows to provide system-wide hotkeys, spatial anchoring (pushing AI-generated text back into the original application), clipboard format preservation, a local WebSocket server for external integrations, system tray access, and per-monitor DPI awareness.

## Trigger
- App startup (registers global hotkeys, starts WebSocket server, enables file watcher)
- User presses global hotkey (Tier 1)
- User presses Alt+Space (Tier 2)
- External application connects to WebSocket (P5)
- User minimizes app (system tray P6)

## Detailed Behavior

### P1. Global Keyboard Hooks
- System-wide keyboard hooks detect hotkey combinations regardless of focused application
- Hotkeys registered on app startup
- Hotkey assignments configurable in Settings → Hotkeys
- Default hotkeys: Alt+Q, Alt+W, Alt+E, Alt+R (configurable per Text Action K1); Alt+Space (Command Bar K4)
- Conflict detection: if hotkey conflicts with Windows or another app, warn user in Settings
- ⚠️ FLAGGED: Global keyboard hooks may trigger antivirus false positives. Code signing recommended.

### P2. HWND Capture
- Before drawing any overlay UI (Tier 1 or Tier 2), app captures the currently active window handle (HWND)
- Prevents focus-stealing race conditions: the overlay does not change which window is "active"
- Captured HWND stored with ChatThread for spatial anchoring (P3)

### P3. Spatial Anchoring
When a Tier 1 hotkey action captures highlighted text from a source application, the app saves:
- Captured HWND
- Source application name (e.g., "Word", "VS Code")
- Source document/window title (if detectable, e.g., "chapter3.docx")
- Original highlighted text

**Chat Header Source Indicator (in Studio):**
- Small non-intrusive banner: "[Source: {App Name} — '{Document Title}']"
- Next to it: **[Apply Latest]** button — pushes most recent assistant message back to source

**Per-Message [Apply] Button:**
- On each assistant message that was a direct transformation of captured text
- NOT on messages from follow-up questions (new queries, not transformations)
- Clicking pushes that specific message's content to source

**Apply Mechanism:**
1. First attempt: direct HWND text injection into source window (replaces originally highlighted text)
2. Fallback (if injection fails): result placed on clipboard (format preserved per P4), Ctrl+V simulated into source window
3. Confirmation toast: "Text applied to {App Name}" with [Undo] option

**Missing Source Window:**
- If source application window closed since Tier 1 action: [Apply] and [Apply Latest] grayed out
- Tooltip: "Source application is no longer available."

**No HWND Context:**
- Chats elevated from Tier 2 (Command Bar) or created directly in Studio: no source indicator or [Apply] buttons

### P4. Clipboard Format Preservation
- During hotkey capture: app checks clipboard DataFormats
- If source placed rich-text formats (HTML, RTF) on clipboard, AI returns response in corresponding format
- On [Apply]: result placed on clipboard in all supported formats; destination app selects best format on paste
- Supported formats: Plain Text, HTML, RTF

### P5. Local WebSocket Server
- Hosts WebSocket server on localhost (127.0.0.1)
- Port configurable in Settings (default: random available port, displayed in Settings)
- Enables direct integrations (e.g., Microsoft Word Add-in pipeline for creative writing)
- Authentication: simple token-based (generated on first run, displayed in Settings)
- ⚠️ FLAGGED: WebSocket server security model needs architectural review. Token-based auth is minimal.
- **Startup:** Starts with app, stops on app exit (or continues if minimize-to-tray)

### P6. System Tray
- App minimizes to Windows system tray (notification area)
- **Tray Icon:** App icon. Visual indicator when AI is generating (pulsing or colored dot).
- **Left-Click:** Restores Studio window (or opens if closed)
- **Right-Click Context Menu:**
  - **New Chat** — Opens Studio, creates new blank chat with default Persona
  - **Open Studio** — Restores/focuses main Studio window
  - **Command Bar** — Opens Tier 2 Command Bar overlay (Alt+Space equivalent)
  - **Recent Chats** — Submenu: 5 most recently active permanent chats by title. Click opens in Studio.
  - *Separator*
  - **Settings** — Opens Settings screen
  - *Separator*
  - **Exit** — Fully closes app (including background tasks: WebSocket, file watcher). Confirmation if AI generating (C20).

### P7. Session Restore
- On launch, if A6 (Restore Last Session) enabled:
  - Reopens all ChatThreads that were open in tabs
  - Restores tab order
  - Restores active tab
  - Restores window position and size
- If disabled: opens with single new chat tab

### P8. Per-Monitor DPI Awareness
- App is fully per-monitor DPI aware
- All UI renders crisply at any DPI scaling (100%, 125%, 150%, 200%, etc.)
- Adapts when window moved between monitors with different DPI settings
- No blurry text or misaligned elements

## Data
- Hotkey assignments stored in SQLite settings
- WebSocket token stored in SQLite
- Session state (open tabs, window position) stored in SQLite

## Success/Failure States
- **HWND Injection Success:** Text appears in source app. Toast confirms.
- **HWND Injection Failure:** Falls back to clipboard + Ctrl+V. Toast: "Applied via clipboard."
- **Source Window Missing:** [Apply] grayed out with tooltip.
- **WebSocket Startup Failure:** Error in Settings: "WebSocket server could not start on port [N]. Try a different port."
- **Hotkey Registration Conflict:** Warning in Settings: "Hotkey [combo] is already in use by [app/Windows]."

## Permissions
- Global keyboard hooks may require running as administrator on first launch (to register hooks)
- Clipboard access is standard Windows capability
- WebSocket server bound to localhost only (no external network access)

## Interactions
- P1 enables K3 (Tier 1 hotkeys) and K4 (Tier 2 Command Bar)
- P2/P3 enable C5a (conditional [Apply] in Studio)
- P4 preserves formatting for K3 Phase 3 (apply) and C6 (copy)
- P5 enables external integrations (Word Add-in for creative writing persona)
- P6 provides system-level access to all tiers
- P7 uses A6 (startup behavior setting)
