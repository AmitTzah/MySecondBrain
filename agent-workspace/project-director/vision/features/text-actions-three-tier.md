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

A Text Action has three independent dimensions:

1. **Capture Scope** (flags, any combination): WHAT to grab from the active window — `selection`, `focusedElement`, `surroundingContext`, `fullDocument`, `screenshot`
2. **Transform** (systemPrompt + modelConfigId): HOW to process the captured content — the AI instruction and which model to use
3. **Apply Mode** (single choice): WHERE to put the result — `replaceSelection`, `insertAtCursor`, `replaceFocusedElement`, `appendToFocusedElement`, `prependToFocusedElement`, `clipboardOnly`, `showOnly`

The user has full freedom to create ANY combination across all three dimensions.

- **Definition:** Name + Capture Scope + System Prompt + Model Configuration + Apply Mode (see [`data/text-action.md`](../data/text-action.md) for full attribute table)
- **Built-in Defaults:**
  | Action | Capture Scope | Apply Mode | Hotkey |
  |--------|---------------|------------|--------|
  | Rewrite | `selection` | `replaceSelection` | Alt+Q |
  | Summarize | `selection` | `showOnly` | Alt+W |
  | Explain | `selection` | `showOnly` | Alt+E |
  | Translate | `selection` | `replaceSelection` | Alt+R |
  | Fix Grammar | `selection` | `replaceSelection` | — |
  | Enhance Prompt | `selection` | `replaceSelection` | — |
  | Continue Writing | `focusedElement` | `insertAtCursor` | Alt+C |
  | Improve Flow | `focusedElement` | `replaceFocusedElement` | — |
  | Summarize Page | `fullDocument` | `showOnly` | — |
  | Explain Screen | `fullDocument,screenshot` | `showOnly` | — |
- **Custom Actions:** User creates named actions with any combination of capture scope flags, any apply mode, custom system prompt, and optional hotkey.
- **Management:** Settings → Text Actions section. Create, edit, delete, reorder. Capture scope uses multi-select checkboxes; apply mode uses radio buttons.
- **Hotkey Assignment:** Each Text Action can be assigned a global hotkey (Settings → Hotkeys)

### K2. Textbox Toolbar
Row of controls above/beside chat message input in Studio (left to right):

- **Persona Selector:** Dropdown. Shows active Persona. Switch anytime. (B4)
- **Thinking Toggle (🧠):** On/Off for extended reasoning (E3)
- **Mute Toggle (🔇):** Per-chat sound notification mute (E4)
- **Tools Dropdown (🔧):** Checkboxes for all 10 tools (bash, text_editor, web_search, web_fetch, image_search, memory, wiki_search, skill_load, ask_user_input, present_files). "All on/off" at top. Auto-approval submenu per tool. Disabled tools removed from API call entirely. (H11)
- **Skills Dropdown (📚):** Checkboxes for each discovered skill + "All on/off." Disabled skills removed from system prompt catalog and skill_load enum. (W6)
- **Memory Toggle (🧠 Mem):** On/Off. Controls SQLite-backed memory tool availability for this chat. (W8)
- **Prompt Library Button:** Opens prompt library (J1)
- **Text Actions Dropdown:** Select a Text Action → transforms current textbox content → preview popup → accept/discard/edit. (K1)
  - **Preview Popup:** Shows original text (left) and AI-transformed text (right, editable). Buttons: [Accept] (replaces textbox content), [Discard], [Edit More] (type additional instructions and re-run).

### K3. Tier 1 — Global Hotkey Text Actions
Three-phase flow triggered by pressing assigned hotkey in any Windows application. The capture scope and apply mode are determined by the active Text Action's configuration — not all actions require highlighted text.

**Phase 1 — Capture (Graduated UIA Pipeline):**

The app captures content per the Text Action's [`captureScope`](../data/text-action.md) flags. The pipeline attempts capture methods in order of reliability, falling back progressively:

1. **`selection` flag:** If set, app captures highlighted text from active window via UIA TextPattern or clipboard. If no text is highlighted and `selection` is the ONLY capture scope flag, the action fails (see Empty State below).

2. **`focusedElement` flag:** If set, app locates the focused element via UIA and reads its entire content using ValuePattern. This captures the full text of the focused textbox/editor even if nothing is highlighted.

3. **`surroundingContext` flag:** If set, app navigates from the focused element via UIA TreeWalker to capture parent and sibling elements' text content. Provides context around the focused element (e.g., surrounding paragraphs, form labels, nearby code).

4. **`fullDocument` flag:** If set, app reads all accessible text in the active window via UIA DocumentRange (if supported) or by traversing the entire UIA tree. Captures the maximum available text context.

5. **`screenshot` flag:** If set, app captures a visual screenshot of the active window. This is a last-resort capture for non-text content (images, diagrams, UI layouts). Can be combined with any text scope flags (e.g., `fullDocument + screenshot` sends both text and an image to the AI).

6. After capture completes (all requested flags satisfied), app captures: HWND (P2), source application name, document/window title (if detectable), and clipboard format information (P4).

7. Minimal "Thinking…" overlay appears near cursor: small pill-shaped indicator (~200×40px), non-intrusive, shows animated dots or spinner. The overlay displays the Text Action name.

8. AI processes the Text Action with the captured content as input. If `screenshot` flag is set, the screenshot image is included as a vision attachment alongside the text.

**Phase 2 — Result Popup:**

When AI response completes, overlay expands into result popup near cursor:
- **Header:** Text Action name + source application name + capture scope summary (e.g., "Summarize Page — Chrome — full page text")
- **Editable Text Area:** AI-transformed text, user can modify before applying
- **Action Buttons:**
  - **[Accept]:** Applies result according to the Text Action's `applyMode` (see Phase 3)
  - **[Discard]:** Dismisses popup, no changes made
  - **[Open in Studio]:** Elevates to permanent ChatThread. Studio opens with result as first assistant message. IsTransient flips to false (O3). This is an orthogonal elevation action — it can combine with any apply mode.
  - **[Save to Wiki]:** Opens wiki save dialog. Saves result as a wiki page. Orthogonal elevation — combines with any apply mode.
  - **[Retry]:** Visible only if AI call failed. Re-attempts with same input and capture scope.
- **Additional Instructions Field:** Text input at bottom. User types extra guidance (e.g., "make it more formal"), presses Enter to re-run Text Action with appended instruction.

**Phase 3 — Apply (on [Accept]):**

The apply behavior is determined by the Text Action's [`applyMode`](../data/text-action.md):

1. **`replaceSelection`:** App attempts direct HWND text injection into source window, replacing the originally highlighted text. If HWND injection fails → fallback: result placed on clipboard (format preserved per P4), Ctrl+V simulated. If no text was highlighted (e.g., capture scope was `focusedElement` only), this mode behaves like `replaceFocusedElement`.

2. **`insertAtCursor`:** App attempts to insert the result at the current cursor position in the focused textbox/editor via UIA TextPattern. If UIA insertion fails → fallback: result placed on clipboard, user pastes manually. Toast: "Result copied to clipboard — paste at cursor (Ctrl+V)."

3. **`replaceFocusedElement`:** App attempts to replace the entire content of the focused textbox/editor via UIA ValuePattern. If UIA replacement fails → fallback: result placed on clipboard, Ctrl+A then Ctrl+V simulated.

4. **`appendToFocusedElement`:** App appends the result to the end of the focused textbox/editor content. If UIA append fails → fallback: result placed on clipboard.

5. **`prependToFocusedElement`:** App inserts the result at the beginning of the focused textbox/editor content. If UIA prepend fails → fallback: result placed on clipboard.

6. **`clipboardOnly`:** Result is copied to clipboard (format preserved per P4). No modification to source application. Toast: "Result copied to clipboard."

7. **`showOnly`:** No automatic application. The result popup remains open. User reads the result, may copy manually, edit, or use elevation actions (Open in Studio, Save to Wiki). User clicks [Discard] to dismiss. The [Accept] button is relabeled to **[Close]** — dismisses popup without modifying source.

For all modes except `showOnly`: after successful application, brief confirmation toast appears: "✓ Text applied — [Text Action name]" with **[Undo]** option (places original/captured text back on clipboard).

If **[Open in Studio]** was clicked: interaction becomes permanent ChatThread, Studio opens with result regardless of apply mode.

**Error State:**
- AI call fails → popup shows error message with [Retry] and [Discard]
- User can edit additional instructions and retry
- Network error → "Network error. Check your connection." with [Retry]
- Capture scope `screenshot` fails (e.g., no window to capture) → action proceeds without screenshot if text scopes provided content; fails only if screenshot was the sole capture flag
- All capture flags produce empty content → see Empty State below

**Empty/No-Capturable-Content State:**
- Hotkey pressed but NO capture scope flags yield any content (nothing highlighted and no focused element text and no document text and no screenshot) → "Thinking…" overlay appears briefly → "No capturable content found. Ensure text is highlighted, a text field is focused, or the active window contains readable text." → auto-dismisses after 4 seconds
- If `selection` is the only capture scope flag and no text is highlighted: "No text selected. Highlight text and try again, or edit this Text Action to use a broader capture scope." → auto-dismisses after 4 seconds
- If `screenshot` is the only capture scope flag and screenshot capture fails: "Could not capture screenshot. Ensure the target window is visible and not minimized." → auto-dismisses after 4 seconds

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
- **Tier 1 Success:** Toast: "Text applied — [Action name]" with Undo option (for all apply modes except `showOnly` and `clipboardOnly`). For `clipboardOnly`: "Result copied to clipboard." For `showOnly`: popup remains open until user dismisses.
- **Tier 1 No Capturable Content:** Overlay: "No capturable content found. Ensure text is highlighted, a text field is focused, or the active window contains readable text." (auto-dismiss 4s). If `selection`-only scope: "No text selected. Highlight text and try again, or edit this Text Action to use a broader capture scope."
- **Tier 1 Capture Partial:** If some capture scope flags succeed and others fail, action proceeds with available content. Warning icon shown in popup header: "⚠️ Screenshot unavailable — text-only result."
- **Tier 1 Error:** Popup with error message + Retry + Discard
- **Tier 2 Empty Input:** Bar shakes briefly
- **Tier 2 Error:** Inline error with Retry link

## Permissions
- Single-user app. All tiers available to the sole user.
- Hotkeys require Windows global keyboard hook permissions (admin may be needed on first run).
- UIA access requires the app to have sufficient UI Automation permissions (standard for desktop apps).

## Interactions
- K1/K3: Text Actions use Model Configurations (B2); capture scope uses UIA patterns (P9); apply mode uses spatial anchoring (P3)
- K3: Captures HWND and formats (P2, P4); graduated capture pipeline per capture scope flags (P9); applies back per apply mode (P3)
- K4: Creates transient ChatThreads (O2); elevation flips IsTransient (O3)
- K5: Full Studio features (Section C)
- Tier 1/2 threads appear in Timeline tab (L5)
- "Open in Studio" and "Save to Wiki" are orthogonal elevation actions — available regardless of apply mode
