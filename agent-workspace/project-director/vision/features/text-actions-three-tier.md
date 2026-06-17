# Text Actions & Three-Tier Interaction — Feature Spec

## What the User Accomplishes
The user accesses AI-powered text transformations across three interaction tiers: instant hotkey rewrites from any Windows application (Tier 1), a Spotlight-style command bar for quick queries (Tier 2), and the full Studio chat workspace for deep conversations (Tier 3). Text Actions are defined once and available everywhere.

## Trigger
- Tier 1: Global hotkey assigned to a Text Action (e.g., Alt+Q = Rewrite)
- Tier 2: Alt+Space (Command Bar)
- Tier 3: Open Studio window, system tray, or elevation from Tier 1/2

## Detailed Behavior

### K1. Text Actions (Unified)
Text Actions are the unified mechanism for AI-powered text transformations. Define once, use via hotkey (Tier 1) or toolbar dropdown (Studio).

- **Definition:** Name + System Prompt + Model Configuration (B2)
- **Built-in Defaults:** Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt
- **Custom Actions:** User creates named actions with custom system prompts
- **Management:** Settings → Text Actions section. Create, edit, delete, reorder.
- **Hotkey Assignment:** Each Text Action can be assigned a global hotkey (Settings → Hotkeys)

### K2. Textbox Toolbar
Row of controls above/beside chat message input in Studio:

- **Persona Selector:** Dropdown. Shows active Persona. Switch anytime.
- **Thinking Toggle:** On/Off for extended reasoning (E3)
- **Mute Toggle:** Per-chat sound notification mute (E4)
- **Tools Toggle:** Enable/disable AI tool use for this chat (H)
- **Auto-Approval Override:** Per-chat override for tool auto-approval (H5)
- **Prompt Library Button:** Opens prompt library (J1)
- **Text Actions Dropdown:** Select a Text Action → transforms current textbox content → preview popup → accept/discard/edit
  - **Preview Popup:** Shows original text (left) and AI-transformed text (right, editable). Buttons: [Accept] (replaces textbox content), [Discard], [Edit More] (type additional instructions and re-run).

### K3. Tier 1 — Global Hotkey Text Actions
Three-phase flow triggered by pressing assigned hotkey while text is highlighted in any Windows application.

**Phase 1 — Capture:**
1. App captures highlighted text from active window via clipboard or UI Automation
2. App captures HWND (P2), source application name, document/window title (if detectable)
3. App captures clipboard format information (P4)
4. Minimal "Thinking…" overlay appears near cursor: small pill-shaped indicator (~200×40px), non-intrusive, shows animated dots or spinner
5. AI processes the Text Action with the captured text as input

**Phase 2 — Result Popup:**
When AI response completes, overlay expands into result popup near cursor:
- **Header:** Text Action name + source application name
- **Editable Text Area:** AI-transformed text, user can modify before applying
- **Action Buttons:**
  - **[Accept]:** Pushes result back to source application (see Phase 3)
  - **[Discard]:** Dismisses popup, no changes made
  - **[Open in Studio]:** Elevates to permanent ChatThread. Studio opens with result as first assistant message. IsTransient flips to false (O3).
  - **[Retry]:** Visible only if AI call failed. Re-attempts with same input.
- **Additional Instructions Field:** Text input at bottom. User types extra guidance (e.g., "make it more formal"), presses Enter to re-run Text Action with appended instruction.

**Phase 3 — Apply (on [Accept]):**
1. App attempts direct HWND text injection into source window (replacing originally highlighted text)
2. If HWND injection fails → fallback: result placed on clipboard (format preserved per P4), Ctrl+V simulated
3. Brief confirmation toast: "Text applied — [Text Action name]" with [Undo] option (places original text back on clipboard)
4. If [Open in Studio] was clicked: interaction becomes permanent ChatThread, Studio opens with result

**Error State:**
- AI call fails → popup shows error message with [Retry] and [Discard]
- User can edit additional instructions and retry
- Network error → "Network error. Check your connection." with [Retry]

**Empty/No-Selection State:**
- Hotkey pressed without highlighted text → "Thinking…" overlay appears briefly → "No text selected. Highlight text in any application and try again." → auto-dismisses after 3 seconds

### K4. Tier 2 — Command Bar (Alt+Space)
Spotlight-style overlay. Two states: Inline and Popped Out.

**Initial Inline State:**
- Appears at horizontal center, ~15% from top edge
- Rounded search-bar-style input field (~600px wide) with subtle drop shadow
- Placeholder: "Ask anything…"
- Default Persona name shown as small label left of input
- "?" icon (right) opens tooltip listing commands and shortcuts
- User types prompt, presses Enter
- While AI processing: thin progress bar animates along top edge; input dimmed but visible

**Inline Q&A Display:**
- AI response arrives → bar expands downward to reveal response in compact message area
- Response renders Markdown (headings, bold, italic, code blocks with syntax highlighting, lists, links)
- User query shown as minimal label: "You: [query text]"
- Multiple Q&A pairs stack vertically; bar grows to max 70% screen height, then older messages scroll internally
- Text input pinned at bottom at all times

**Inline Controls:**
- **[Pop-out] button (expand icon):** Top-right. Detaches into floating mini-window.
- **[Close] button (X):** Dismisses. Saves as transient ChatThread (IsTransient=true).
- **[Copy] button:** On each AI response. Copies to clipboard.
- **Persona indicator:** Shows active Persona. Click to open compact switcher dropdown.

**Popped-Out Floating Mini-Window:**
- Triggered by [Pop-out]. Detaches from anchored position into floating resizable window.
- Default size: 500×400px. Minimum: 350×250px. No hard max.
- Title bar: "Command Bar" + active Persona name
- Title bar controls: [Open in Studio] (elevates to permanent chat), [Pin Always on Top] toggle, [Minimize to system tray], [Close] (saves as transient)
- Message area: scrollable compact chat view with same rendering
- Text input at bottom with Send button
- Remembers last position and size across sessions
- Movable by title bar drag, resizable by edge/corner drag

**Elevation to Studio:**
- From inline [Pop-out] → [Open in Studio], OR mini-window [Open in Studio]
- Studio window opens/focuses
- New tab with full conversation as permanent ChatThread (IsTransient=false)
- Command Bar overlay/mini-window closes

**Dismissal:**
- Closing saves conversation as transient ChatThread (IsTransient=true)
- Transient threads visible in Timeline tab (L5), subject to 7-day auto-cleanup (O4)
- Escape key: if input has text, clears text first; second Escape dismisses. If input empty, dismisses immediately.

**Error State:** AI call fails → error message inline below input with [Retry] link. User can edit query and retry.

**Empty/No-Input State:** Enter with empty input → bar briefly shakes (visual nudge), does nothing.

### K5. Tier 3 — Studio Chat
The full chat workspace. See [`features/studio-chat-workspace.md`](features/studio-chat-workspace.md) for complete specification.

- Opened via: main application window, system tray → Open Studio, elevation from Tier 1/2
- All features from Section C available

## Data
- [`data/chat-thread.md`](data/chat-thread.md) — IsTransient flag, HWND context
- [`data/message.md`](data/message.md) — message content
- [`data/text-action.md`](data/text-action.md) — Text Action definitions

## Success/Failure States
- **Tier 1 Success:** Toast: "Text applied — [Action name]" with Undo option
- **Tier 1 No Selection:** Overlay: "No text selected. Highlight text and try again." (auto-dismiss 3s)
- **Tier 1 Error:** Popup with error message + Retry + Discard
- **Tier 2 Empty Input:** Bar shakes briefly
- **Tier 2 Error:** Inline error with Retry link

## Permissions
- Single-user app. All tiers available to the sole user.
- Hotkeys require Windows global keyboard hook permissions (admin may be needed on first run).

## Interactions
- K1/K3: Text Actions use Model Configurations (B2)
- K3: Captures HWND and formats (P2, P4); applies back via spatial anchoring (P3)
- K4: Creates transient ChatThreads (O2); elevation flips IsTransient (O3)
- K5: Full Studio features (Section C)
- Tier 1/2 threads appear in Timeline tab (L5)
