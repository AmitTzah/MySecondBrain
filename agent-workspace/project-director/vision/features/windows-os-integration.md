# Windows OS Integration — Feature Spec

## What the User Accomplishes

The app deeply integrates with Windows to provide system-wide hotkeys, spatial anchoring (pushing AI-generated text back into the original application), clipboard format preservation, a local WebSocket server for external integrations, system tray access, per-monitor DPI awareness, bash tool adaptation for Windows, and workspace isolation for safe AI command execution.

## Trigger

- App startup (registers global hotkeys, starts WebSocket server, enables file watcher)
- User presses global hotkey (Tier 1)
- User presses Alt+Space (Tier 2)
- External application connects to WebSocket (P5)
- User minimizes app (system tray P6)
- AI calls bash tool (P9)
- AI writes files in workspace (P10)

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

When a Tier 1 hotkey action captures content from a source application, the app saves:
- Captured HWND
- Source application name (e.g., "Word", "VS Code")
- Source document/window title (if detectable, e.g., "chapter3.docx")
- Original captured content (text and/or screenshot, depending on capture scope)

**Chat Header Source Indicator (in Studio):**
- Small non-intrusive banner: "[Source: {App Name} — '{Document Title}']"
- Next to it: **[Apply Latest]** button — pushes most recent assistant message back to source

**Per-Message [Apply] Button:**
- On each assistant message that was a direct transformation of captured content
- NOT on messages from follow-up questions (new queries, not transformations)
- Clicking pushes that specific message's content to source

**Apply Mechanism:**
1. First attempt: direct HWND text injection into source window (replaces captured content per the Text Action's applyMode)
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
- WebView2 artifacts panel respects DPI awareness

### P9. UIA-Based Context Capture

The UIA (UI Automation) context capture system powers Tier 1 Text Action capture scope. It provides a graduated pipeline that attempts capture methods in order of reliability, falling back progressively based on which capture scope flags are set on the active Text Action.

**Capture Pipeline (in priority order):**

1. **TextPattern — Selection Capture (`selection` flag):**
   - App uses UIA TextPattern to read currently highlighted/selected text in the focused element.
   - Works with any UIA-compatible application (most Windows apps: Word, VS Code, browsers, Notepad, etc.).
   - Fallback: if UIA TextPattern is unavailable, app reads clipboard after simulating Ctrl+C (clipboard fallback restores original clipboard content afterward).

2. **ValuePattern — Focused Element Capture (`focusedElement` flag):**
   - App identifies the currently focused element via UIA FocusedElement.
   - Reads the element's entire text content via UIA ValuePattern.
   - Captures full textbox/editor content even when nothing is highlighted.

3. **TreeWalker — Surrounding Context Capture (`surroundingContext` flag):**
   - Starting from the focused element, app navigates the UIA tree using TreeWalker.
   - Captures: the focused element's content + its parent element's content + its immediate sibling elements' content.
   - Navigation depth: up to 2 levels up (parent + grandparent) and all immediate siblings at the focused element's level.

4. **DocumentRange — Full Document Capture (`fullDocument` flag):**
   - App requests the full document text range via UIA DocumentRange.
   - If DocumentRange is not supported, app traverses the entire UIA tree from the root element, collecting all accessible text content.

5. **Screenshot Capture (`screenshot` flag):**
   - App captures a visual screenshot of the active window (client area) using Win32 PrintWindow or BitBlt.
   - The screenshot is included as a vision attachment alongside any captured text when sent to the AI.

**Capture Fallback Logic:**
For each Text Action, the capture pipeline runs all flags that are set. The pipeline is additive — flags are processed in order, and each successful capture adds to the total captured content.

**Clipboard Restoration:**
When the clipboard fallback is used (simulating Ctrl+C), the app MUST restore the original clipboard content after capture.

### P10. bash Tool — Windows Adaptation

The `bash` tool is named to match Anthropic's `bash_20250124` trained-in schema but executes via Windows shells:

```
bash tool receives command
    │
    ├── Is it a .sh script?
    │   ├── Try: "C:\Program Files\Git\bin\bash.exe" script.sh
    │   ├── Try: wsl bash -c "script.sh"
    │   └── Neither? → Error: "bash or WSL required for .sh scripts"
    │
    ├── Contains heredoc (cat > file << 'EOF')?
    │   └── Redirect: use write_to_file tool instead
    │
    └── Everything else → cmd.exe /c "command"
         (python, pip, npm, pandoc — cross-platform, no translation needed)
```

**Bash detection at startup:**
- Check `C:\Program Files\Git\bin\bash.exe` (Git for Windows)
- Check `wsl --status` (Windows Subsystem for Linux)
- Store availability in tool description for model awareness
- If neither available: model adapts — uses write_to_file for file writes, skips `.sh` scripts

**System prompt context:**
```
You are running on Windows. Shell commands use Command Prompt (cmd.exe).
- python, pip, npm work as expected
- .sh scripts require Git Bash or WSL
- File paths use backslashes: C:\Users\...
- The workspace is at %WORKSPACE%
- For multi-line file writing, prefer the write_to_file tool over heredocs
```

### P11. Workspace Isolation

All `bash` commands execute inside `%LOCALAPPDATA%/MySecondBrain/workspace/`:

- **Working directory:** Set to workspace path via `Process.StartInfo.WorkingDirectory` before each command
- **Path blocking:** Absolute paths outside workspace detected (scan for `C:\`, `%`, `~`) and blocked pre-execution
- **Wiki access:** Wiki directory is read-only from bash. Writes to wiki blocked — must go through Write-to-Wiki pipeline (N5)
- **Cleanup:** Workspace files older than 24h removed on app startup
- **Two-zone model:**
  - **Workspace** (`%LOCALAPPDATA%/MySecondBrain/workspace/`) — bash execution, temp files, intermediate work. 24h auto-cleanup. Equivalent to Claude's `/home/claude/`.
  - **Artifacts directory** — final deliverables. Model calls `present_files` to copy from workspace to artifacts. Persisted with chat. Equivalent to Claude's `/mnt/user-data/outputs/`.
- **`present_files` bridge:** Model creates in workspace, calls `present_files(["file"])` to surface as artifact. Without `present_files`, workspace files remain invisible to user.
- **Skills integration:** Skills' bundled scripts are copied to workspace so both bash and skills can reference them. Shared scripts (e.g., `scripts/office/` used by both docx and xlsx) are accessible to both skills.

## Data

- Hotkey assignments stored in SQLite settings
- WebSocket token stored in SQLite
- Session state (open tabs, window position) stored in SQLite
- TextAction captureScope and applyMode stored in SQLite (see [`data/text-action.md`](../data/text-action.md))
- Workspace files are temporary — not backed up, not persisted across app restarts

## Success/Failure States

- **HWND Injection Success:** Text appears in source app. Toast confirms.
- **HWND Injection Failure:** Falls back to clipboard + Ctrl+V. Toast: "Applied via clipboard."
- **Source Window Missing:** [Apply] grayed out with tooltip.
- **WebSocket Startup Failure:** Error in Settings: "WebSocket server could not start on port [N]. Try a different port."
- **Hotkey Registration Conflict:** Warning in Settings: "Hotkey [combo] is already in use by [app/Windows]."
- **UIA Capture Partial:** Some capture scope flags fail but others succeed → action proceeds. Warning in popup.
- **UIA Capture Total Failure:** All capture flags produce no content → "No capturable content found" (K3 Empty State).
- **Screenshot Capture Failure:** Window minimized or occluded → warning in popup header.
- **bash blocked (outside workspace):** "Cannot access path outside workspace: [path]."
- **bash requires shell not available:** "This command requires Git Bash or WSL. Install Git for Windows or enable WSL."
- **Workspace not writable:** "Cannot write to workspace directory. Check disk space and permissions."

## Permissions

- Global keyboard hooks may require running as administrator on first launch (to register hooks)
- Clipboard access is standard Windows capability
- WebSocket server bound to localhost only (no external network access)
- UIA access is standard Windows desktop application capability — no elevation required
- Workspace isolation is app-enforced, not OS-level sandboxing

## Interactions

- P1 enables K3 (Tier 1 hotkeys) and K4 (Tier 2 Command Bar)
- P2/P3 enable C5a (conditional [Apply] in Studio)
- P4 preserves formatting for K3 Phase 3 (apply) and C6 (copy)
- P5 enables external integrations (Word Add-in for creative writing persona)
- P6 provides system-level access to all tiers
- P7 uses A6 (startup behavior setting)
- P9 powers K3 Phase 1 capture scope (graduated UIA pipeline per TextAction.captureScope flags)
- P10 (bash on Windows) serves H1 (bash tool)
- P11 (workspace isolation) contains H1 execution, bridges to F1 (present_files → artifacts)
- P11 wiki read-only restriction enforces N8 (wiki access restrictions)
