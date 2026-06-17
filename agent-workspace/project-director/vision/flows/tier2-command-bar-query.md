# Tier 2 — Command Bar Query — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — needs quick AI answers without leaving the current application or opening the full Studio. Uses the Command Bar like Spotlight (macOS) or PowerToys Run — type a query, get an answer, decide whether to keep it or dive deeper.

## Goal
Press Alt+Space from any Windows application, ask a question, get an AI response in a compact overlay, and either dismiss (saving as transient) or pop out into a floating mini-window for multi-turn conversation, or elevate to a full Studio chat.

## Starting Point
The user is in any Windows application. MySecondBrain is running (Studio window may be open or minimized to tray). Global hotkeys are active. The user presses **Alt+Space**.

---

## Happy Path (Quick Q&A → Dismiss)

### Step 1: Open Command Bar
**Trigger:** User presses **Alt+Space** from any application.

**Overlay:** A Spotlight-style input bar appears at the horizontal center of the screen, approximately 15% from the top edge. The bar is ~600px wide, rounded, with subtle styling. It does not steal focus — the user can continue typing in their original application if they click away, but the bar captures keyboard input while visible.

**Initial elements:**
- Text input field with placeholder: "Ask anything…"
- Default Persona name shown as a small label to the left of the input (e.g., "General Assistant")
- "?" icon (right side) — click opens tooltip listing available shortcuts and commands
- The bar has subtle drop shadow and a thin border

### Step 2: Type and Submit Query
The user types a question (e.g., "What's the difference between OAuth 2.0 and OIDC?") and presses **Enter**.

**During AI processing:**
- A thin progress bar animates along the top edge of the bar
- The input field dims but remains visible (user cannot edit while processing, but can see their query)
- The user can continue working in their original application while the AI processes (the overlay is non-blocking)

### Step 3: View Response
The AI response arrives. The bar expands downward to reveal:
- **User query label:** "You: What's the difference between OAuth 2.0 and OIDC?" (minimal, collapsed style)
- **AI response:** Full Markdown rendering (headings, bold, italic, code blocks with syntax highlighting, lists, links) in a compact message area below the input

**Controls on the response:**
- **[Copy] button** on the AI response — copies the raw Markdown to clipboard
- **[Pop-out] button** (expand icon, top-right) — detaches into a floating mini-window (see Alternative Path B)
- **[Close] button** (X, top-right) — dismisses the bar, saves as transient ChatThread
- **Persona indicator** — shows active Persona; click opens compact Persona switcher dropdown

The user reads the response, finds it sufficient, and clicks **X (Close)**.

### Step 4: Dismissal
- The Command Bar overlay fades out
- The conversation is saved as a transient ChatThread (IsTransient=true) per [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O2
- The ChatThread appears in the Timeline tab (sidebar → Timeline in [`screens/studio-chat.html`](../screens/studio-chat.html))
- Subject to 7-day auto-cleanup (O4) unless elevated or excepted
- The user returns to their original application seamlessly

---

## Alternative Paths

### Path B: Multi-Turn Q&A in Inline Mode
The user asks a follow-up question without leaving the Command Bar:

1. After the first response, the input field is active again at the bottom
2. User types a follow-up: "And which one should I use for a mobile app?"
3. Presses Enter → second AI response appears below the first
4. Multiple Q&A pairs stack vertically
5. The bar grows to a maximum of 70% of the screen height, then older messages scroll internally
6. Text input remains pinned at the bottom at all times
7. User can continue this multi-turn conversation indefinitely
8. On Close: all Q&A pairs are saved as a single transient ChatThread

### Path C: Pop-Out into Floating Mini-Window
The user clicks **[Pop-out]** (expand icon):

1. The Command Bar detaches from its anchored position and becomes a floating, resizable mini-window
2. Default size: 500×400px. Minimum: 350×250px. No hard maximum.
3. Title bar shows: "Command Bar" + active Persona name
4. Title bar controls:
   - **[Open in Studio]:** Elevates to permanent ChatThread. Studio opens with full conversation. Mini-window closes.
   - **[📌 Pin Always on Top]:** Toggle. Keeps mini-window above all other windows.
   - **[Minimize to system tray]:** Hides mini-window to tray. Conversation preserved.
   - **[Close] (X):** Saves as transient ChatThread and closes mini-window.
5. Message area: scrollable compact chat view with the same Markdown rendering
6. Text input at bottom with Send button
7. Window is movable by title bar drag, resizable by edge/corner drag
8. Remembers last position and size across sessions

**From mini-window, user can:**
- Continue multi-turn conversation
- Click "Open in Studio" → elevates to permanent Studio chat
- Pin window to stay on top while referencing other apps
- Close → saves as transient

### Path D: Elevate to Studio (from Inline or Mini-Window)
At any point, the user clicks **[Open in Studio]** (available in both inline via Pop-out → Open in Studio, and directly in the mini-window title bar):

1. Studio window opens/focuses (or launches if not running)
2. A new tab opens with the full Command Bar conversation as a permanent ChatThread
3. IsTransient flips to false (O3)
4. The Command Bar overlay or mini-window closes
5. The chat appears in the main Chat list (sidebar)
6. User can continue the conversation with full Studio features: artifacts, file uploads, model comparison, Write to Wiki, tool use, etc.

### Path E: Switch Persona Mid-Conversation
1. User clicks the Persona indicator (name label left of input)
2. Compact dropdown opens listing all configured Personas
3. User selects a different Persona (e.g., switches from "General Assistant" to "Code Helper")
4. All SUBSEQUENT messages use the new Persona's system prompt and Model Configuration
5. Previous messages retain their original Persona labels
6. The active Persona indicator updates

### Path F: Copy Response Without Dismissing
1. User clicks **[Copy]** on an AI response
2. Response text (raw Markdown) copied to clipboard
3. Brief visual feedback: "Copied!" appears momentarily on the Copy button
4. Command Bar remains open — user can ask follow-up or close

### Path G: Escape Key Dismissal
- **If input has text:** First Escape clears the input field. Second Escape dismisses the bar.
- **If input is empty:** First Escape dismisses the bar immediately.
- On dismiss: conversation saved as transient ChatThread.

---

## Failure Points

| Failure | Handling |
|---------|----------|
| AI call fails (API error) | Error message appears inline below the input: "Error: [details]." **[Retry]** link next to the error. User can edit query and retry. |
| Network error | "Network error. Check your connection." with **[Retry]** link. |
| No API keys configured | "No API keys configured. Add one in Settings → Providers." with link to Settings. |
| Enter pressed with empty input | Bar briefly shakes (visual nudge). Nothing happens. |
| Context window fills up (multi-turn) | Warning appears: "⚠️ Context nearly full. Older messages will be summarized." per the active Model Config's overflow strategy (B8). |
| Hotkey conflict (Alt+Space used by another app) | Detected during hotkey configuration in Settings → Hotkeys. User must reassign. |

---

## Edge Cases

1. **Command Bar open while a Tier 1 hotkey is pressed:** Tier 1 hotkey is ignored. Command Bar takes priority. Both overlays cannot coexist.

2. **Command Bar open while Studio is focused:** Works identically — the overlay appears above the Studio window. Useful for quick queries even while in Studio.

3. **User switches virtual desktops (Windows Task View) while Command Bar is open:** The Command Bar overlay follows to the new desktop (it's a top-level window). The conversation state is preserved.

4. **User locks Windows (Win+L) while Command Bar is open:** The Command Bar is hidden behind the lock screen. On unlock, the Command Bar is still open with its conversation intact (window state preserved).

5. **Extremely long conversation in mini-window:** No hard message limit. The mini-window is scrollable. Performance may degrade with hundreds of messages — the conversation remains efficient since it's a single ChatThread.

6. **User opens Command Bar, types nothing, dismisses:** No ChatThread created. No transient data saved.

7. **User opens Command Bar immediately after dismissing a previous one:** A new transient ChatThread is created. The previous one is already saved. Both appear separately in the Timeline tab.

8. **Persona has no API key / invalid key:** The Persona's Model Configuration references a key. If that key is invalid, the AI call fails with the same error handling as above. User can switch to a Persona with a valid key via the Persona dropdown.

9. **User resizes mini-window very small (< 350×250px):** Minimum size enforced. Window snaps to minimum dimensions.

10. **User pops out, pins, then opens Studio from mini-window:** Mini-window closes. Studio opens with the elevated chat. Pin state is irrelevant — the Studio window follows its own pin settings (C23).

---

## Completion
**Happy path ending:** The user got a quick AI answer via the Command Bar, read it, clicked X to dismiss. The interaction is saved as a transient ChatThread in the Timeline. The user continues working in their original application. Zero context switch cost.

**Elevation ending:** The user opened the conversation in Studio as a permanent chat. Full features are now available: continue the conversation, save to wiki, compare models, use tools.

**Mini-window ending:** The Command Bar is popped out as a floating mini-window, pinned on top. The user references AI responses while working in another application. The mini-window stays until the user closes it or elevates to Studio.

---

## Cross-References
- Feature spec: [`features/text-actions-three-tier.md`](../features/text-actions-three-tier.md) K4 — Command Bar two-state behavior
- Feature spec: [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O1-O4 — ChatThread, IsTransient, elevation, auto-cleanup
- Feature spec: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B4 — Persona selection, B8 — Context overflow
- Feature spec: [`features/studio-chat-workspace.md`](../features/studio-chat-workspace.md) C2-C4 — Markdown rendering, streaming
- Feature spec: [`features/windows-os-integration.md`](../features/windows-os-integration.md) P1 — Global hotkeys
- Data entity: [`data/chat-thread.md`](../data/chat-thread.md)
- Data entity: [`data/message.md`](../data/message.md)
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Destination for elevation, Timeline tab
