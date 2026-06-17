# Model Comparison — Screen Specification

## Purpose

The Model Comparison view allows the user to chat with multiple Personas simultaneously, each in its own panel with independent conversation history. The user can send follow-up messages to individual Personas, compare multi-turn conversations, and at any point accept one conversation into the main chat. All other conversations are automatically saved as branches. The comparison view is transient — it replaces the main content area of the Studio (sidebar and right panel remain visible).

## Layout

Replaces the Studio's main content area (center column). Sidebar and right panel remain visible:
- **Setup Phase:** Centered single-column: Persona checklist with chips + Prompt input + Start button
- **Results Phase:** Side-by-side panels (horizontal default, toggleable to vertical). Each panel is an independent mini-chat with its own conversation history, text input, and Accept/Stop buttons.
- **Header bar:** "Model Comparison" title + layout toggle + ✕ Close

## Phases

### Phase 1: Setup

**Purpose:** Select Personas and write the initial prompt.

**Layout:** Single centered column in the main content area.

**Selected Persona Chips (top):**
- Horizontal row of chips showing selected Personas
- Each chip: Persona name + Model Config name + ✕ to deselect
- When none selected: "Select at least 2 Personas below"
- Count: "[N] Personas selected"

**Persona Checklist:**
- Scrollable list of all configured Personas with checkboxes
- Each row: ☐ checkbox + Persona name (bold) + Model Config name (muted) + "↺ Last used" badge if recent
- "Select All" / "Deselect All" links
- Filter input
- Min 2, max 4 Personas

**Prompt (below checklist):**
- Multi-line textarea pre-filled from Studio textbox
- Editable
- Token/character counter

**Layout Toggle:**
- "⬛⬛ Horizontal" (default) | "⬛ Vertical" — changeable during comparison

**Actions:**
- "Start Comparison" (enabled when 2-4 selected) — sends prompt to all Personas
- "Cancel" — returns to Studio chat

### Phase 2: Results

**Purpose:** Chat with each Persona in independent panels and pick the best conversation.

**Layout:**
- Horizontal (default): side-by-side columns. 2 = 2 columns, 3 = 3, 4 = 2×2 grid.
- Vertical: stacked rows.
- Toggle in header switches layout.

**Header Bar:**
- "⚖ Model Comparison" title
- Layout toggle: ⬛⬛ Horizontal / ⬛ Vertical
- "✕ Close" — closes comparison. If no Accept clicked, nothing saved to chat.

**Each Results Panel (independent mini-chat):**
- **Header:** Persona name (bold) + Model Config name (muted)
- **Metrics bar (visible after first response):** "⏱ [N]s" response time, "📊 [N] tokens", "💰 $[cost]"
- **Conversation area:** Scrollable history of all messages exchanged with this Persona in the comparison. User messages and assistant responses rendered with Markdown, code blocks, and thinking blocks per standard chat rendering (C2, C3, C4, E3).
- **Thinking block (when Thinking enabled):** Collapsible, with second counter during streaming. Same behavior as studio-chat thinking blocks.
- **Text input (bottom of each panel):** Single-line input + Send button. User types follow-up messages to THIS specific Persona only. Each panel has independent conversation history.
- **Stop button:** Per-panel. Visible during generation only. Stops this panel's stream, preserves partial response.
- **Accept button:** Prominent, always visible (below or beside the text input). Clicking Accept immediately:
  1. Appends this panel's ENTIRE conversation to the originating ChatThread as the assistant's response sequence
  2. Auto-saves all OTHER panels' conversations as alternate branches (D3) on the accepted message node
  3. Closes the comparison view
  4. Returns to the normal Studio chat view — the chat now shows the accepted conversation
  5. Toast: "Accepted [Persona] conversation. [N] other conversations saved as branches."

**Panel States:**
- **Streaming:** "Stop" button visible. "Accept" button disabled (must wait for generation to complete or stop it).
- **Idle (ready for input):** Text input enabled. "Accept" button enabled. "Stop" hidden.
- **Error:** Red error banner with "Retry" button. Text input still available for follow-up.

## Data Displayed

- Personas from [`data/persona.md`](../data/persona.md)
- Model Configurations from [`data/model-configuration.md`](../data/model-configuration.md)
- Comparison conversations are transient during the session
- On Accept: accepted conversation becomes Messages in the originating ChatThread
- Other conversations become branch data (D3) on the accepted message node
- Token counts and costs feed into [`data/usage-record.md`](../data/usage-record.md)

## Actions

| Action | Trigger | Behavior |
|--------|---------|----------|
| Check/Uncheck Persona | Click checkbox | Toggles selection. Updates chips. Enables Start when ≥2 selected. |
| Remove from chips | ✕ on chip | Unchecks that Persona. |
| Select All / Deselect All | Link | Selects max 4 or deselects all. |
| Start Comparison | Button (2-4 selected) | Sends initial prompt to all selected Personas simultaneously. Transitions to Results phase. |
| Cancel (Setup) | Button | Returns to chat. Nothing sent. |
| Toggle layout | ⬛⬛ / ⬛ buttons | Switches between horizontal and vertical panel layout. |
| Send (per panel) | Send button or Enter in panel's text input | Sends message to that specific Persona. Streams response in that panel only. |
| Stop (per panel) | Per-panel button | Stops that panel's generation. Preserves partial response. Enables Accept. |
| Accept | Per-panel button | Accepts this panel's entire conversation into the main chat. Others auto-saved as branches. Closes comparison. Returns to Studio. |
| ✕ Close | Header button | Closes comparison. If no Accept clicked, nothing saved to chat. |

## Empty States

| Context | State |
|---------|-------|
| Setup — No Personas selected | Chip row: "Select at least 2 Personas below." Start disabled. |
| Setup — No Personas configured | "No Personas configured. Create one in Settings → Profiles." |
| Setup — Filter no results | "No Personas match '[filter]'." |

## Loading States

| Context | State |
|---------|-------|
| Sending initial prompt | "Starting comparison..." overlay with spinner. |
| Streaming in a panel | Streaming tokens. Thinking block shows "🧠 Thinking... [N]s". Stop button visible. Accept disabled. |
| Panel waiting for first token | Spinner in that panel's conversation area. |

## Error States

| Context | State |
|---------|-------|
| One panel fails | Red error banner in that panel with "Retry" button. Other panels unaffected. |
| All panels fail | "All comparisons failed. Check API keys and network connection." |
| API key invalid | Red error: "API key for [provider] is invalid. Check Settings → Providers." |
| Network error | Yellow banner: "Network error." + Retry. |
| Close during active generation | Confirmation: "Responses are still being generated. Stop and discard?" |

## Navigation

**Entry Points:**
- Studio textbox toolbar → ⚖ Compare button (C26) → [`model-comparison.html`](model-comparison.html)

**Exit Points:**
- "Accept" on a panel → Returns to [`studio-chat.html`](studio-chat.html). Accepted conversation appended. Others saved as branches.
- "Cancel" (Setup) / ✕ Close (Results) → Returns to [`studio-chat.html`](studio-chat.html). Nothing saved.

## Cross-References

- Feature spec: [`features/model-comparison.md`](../features/model-comparison.md) — NOTE: simplified from M spec. Accept auto-saves branches. Per-panel text inputs for independent multi-turn conversations.
- Personas: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B3
- Thinking blocks: [`features/chat-modes-controls.md`](../features/chat-modes-controls.md) E3
- Message branching: [`features/message-manipulation-branching.md`](../features/message-manipulation-branching.md) D3
- Studio chat: [`screens/studio-chat.md`](studio-chat.md) C26

## Flagged Concerns

1. ⚠️ FLAGGED: Per-panel independent conversations mean each panel maintains its own message history. Accepting one panel appends its ENTIRE multi-turn conversation to the main chat — this could be surprising if the main chat had a different context. Architect should ensure the transition is clean.

2. ⚠️ FLAGGED: M spec originally described single-prompt comparison. This spec adds per-panel multi-turn chatting — a significant expansion. The feature spec M1-M4 should be updated to reflect this simplified Accept flow and per-panel text inputs.

3. ⚠️ FLAGGED: Simultaneous multi-turn conversations with N Personas = N independent API connection streams that may run for extended periods. Cost and connection management implications.
