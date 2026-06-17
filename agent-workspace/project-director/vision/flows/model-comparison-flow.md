# Model Comparison — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — wants to compare how different AI models/Personas respond to the same prompt. Uses this for: evaluating new models, deciding which Persona to use for a task, comparing reasoning quality, or testing system prompt variations.

## Goal
Send the same prompt to 2-4 different Personas simultaneously, engage in multi-turn follow-up conversations with each independently, and accept the best response into the main chat — with the other conversations automatically saved as branches for later reference.

## Starting Point
The user is in an active Studio chat ([`screens/studio-chat.html`](../screens/studio-chat.html)). They have a prompt in mind (or already typed in the textbox) that they want to test against multiple Personas.

---

## Happy Path (Two Personas, Accept One)

### Step 1: Trigger Comparison
**Trigger:** The user clicks the **⚖ Compare** button in the Studio textbox toolbar (C26).

The Studio's main content area transitions to the Model Comparison view. The sidebar and right panel remain visible.

### Step 2: Setup — Select Personas
The Setup phase displays:

**Selected Persona Chips (top):**
- Horizontal row showing selected Personas as chips
- Initially empty: "Select at least 2 Personas below"
- As Personas are selected, chips appear: Persona name + Model Config name + ✕ to deselect

**Persona Checklist:**
- Scrollable list of all configured Personas with checkboxes
- Each row: ☐ checkbox + Persona name (bold) + Model Config name (muted) + "↺ Last used" badge if recently used
- "Select All" / "Deselect All" links
- Filter input for searching Personas (useful when many Personas are configured)
- Constraints: minimum 2 Personas required, maximum 4 allowed. Additional checkboxes grayed out at 4.

The user checks two Personas:
- ☑ "Python Expert" (GPT-4o — Fast)
- ☑ "General Assistant" (Claude Sonnet 3.5)

Chips appear: `Python Expert — GPT-4o Fast ✕` `General Assistant — Claude Sonnet 3.5 ✕`

**Prompt:**
Multi-line textarea pre-filled with the current Studio textbox content. The user edits or replaces with their comparison prompt: "Explain the GIL in Python and why it still exists in 2025."

**Layout Toggle:**
"⬛⬛ Horizontal" (default, selected) | "⬛ Vertical"

### Step 3: Start Comparison
The user clicks **"Start Comparison"** (enabled because 2 Personas are selected).

The view transitions to the Results phase.

### Step 4: Results Phase — Initial Responses
**Layout:** Two side-by-side panels (horizontal layout). Each panel is an independent mini-chat.

**Left Panel — "Python Expert (GPT-4o — Fast)":**
- **Header:** Persona name (bold) + Model Config name (muted)
- **Conversation area:** Shows the initial prompt as the user message. AI response begins streaming.
- **During streaming:** "Stop" button visible. "Accept" button disabled.
- **Thinking block** (if Thinking enabled): Collapsible with "🧠 Thinking... [N]s" counter.
- Streaming tokens with Markdown rendering, code blocks with syntax highlighting.
- On completion:
  - **Metrics bar appears:** "⏱ 3.2s | 📊 450 tokens | 💰 $0.003"
  - "Stop" button hidden
  - "Accept" button enabled
  - Text input at bottom becomes active

**Right Panel — "General Assistant (Claude Sonnet 3.5)":**
- Identical layout, independent streaming
- AI response streams simultaneously with the left panel
- On completion: "⏱ 2.1s | 📊 520 tokens | 💰 $0.008"

Both panels stream in parallel. The user watches both responses build in real-time.

### Step 5: Multi-Turn Follow-Up (Optional)
The user decides to probe deeper. In the **left panel** (Python Expert), the user types a follow-up in that panel's text input:

**User (left panel only):** "Can you give a specific code example showing where the GIL causes a bottleneck?"

The left panel sends this message to the Python Expert Persona. The right panel is unaffected — it waits, showing its previous response.

The Python Expert responds with a code example. The left panel now has 2 exchanges (initial + follow-up).

The user then types in the **right panel** (General Assistant):

**User (right panel only):** "What are the alternatives to the GIL that have been proposed?"

The General Assistant responds with alternatives. The right panel now has 2 exchanges.

Each panel maintains its own independent conversation history. The user is effectively having two separate chats simultaneously.

### Step 6: Accept One Conversation
The user decides the Python Expert's responses (with the code example) are better for their needs. They click the **"Accept"** button on the left panel.

**What happens:**
1. The left panel's ENTIRE conversation (initial prompt + user follow-up + AI responses) is appended to the originating Studio ChatThread as a sequence of messages
2. The right panel's conversation is automatically saved as an alternate branch (D3) on the accepted message node — no manual "Save as Branch" needed
3. The comparison view closes immediately
4. The user returns to the normal Studio chat view — the chat now shows the accepted Python Expert conversation
5. The originating ChatThread's conversation history now includes:
   - [previous chat messages, if any]
   - User: "Explain the GIL in Python and why it still exists in 2025." (from comparison)
   - Python Expert: [AI response about GIL]
   - User: "Can you give a specific code example..." 
   - Python Expert: [code example response]
   - [branch indicator for General Assistant's version, D3]

6. Toast appears: "✓ Accepted Python Expert conversation. 1 other conversation saved as branch."

The user can now continue the chat with the accepted responses, or use the branch navigator (D3) to view the General Assistant's alternative responses.

---

## Alternative Paths

### Path B: Broadcast Mode
From the Results phase, the user toggles **🔗 Broadcast** ON:

1. Per-panel text inputs are hidden
2. A single centered broadcast input appears: "Send to all Personas"
3. User types a message and clicks Send → message is sent to ALL panels simultaneously
4. All Personas respond to the same follow-up
5. Useful for: comparing how different models handle the same follow-up question
6. Toggle back OFF → per-panel inputs reappear with their independent histories preserved

### Path C: Accept After Multiple Rounds
The user engages in 3-4 rounds of follow-up with each Persona before deciding. Each panel builds up its own conversation history. On Accept: the ENTIRE multi-turn conversation from the accepted panel is appended to the main chat. The other panel(s) conversations are saved as branches.

### Path D: Close Without Accepting
The user clicks ✕ **Close** (top-right of comparison header):

**If no generation is active:** Closes immediately. Nothing saved to chat. Comparison conversations are discarded.

**If generation is active:** Confirmation dialog: "Responses are still being generated. Stop and discard?" Options: "Stop & Discard" / "Keep Open"

On discard: nothing is saved to the originating chat. The chat remains exactly as it was before comparison was triggered.

### Path E: One Panel Fails, Others Succeed
During initial streaming, the right panel's API call fails:

1. Right panel shows red error banner: "Error: API key invalid for Anthropic." with **[Retry]** button
2. Left panel continues streaming normally
3. User can click Retry on the right panel to re-attempt
4. User can still Accept the left panel (the failed panel's conversation is empty — nothing to branch)
5. If user clicks Retry and it succeeds: both panels now have content, comparison continues

### Path F: All Panels Fail
All API calls fail (e.g., network down, all keys invalid):

"All comparisons failed. Check API keys and network connection."

User can:
- Retry all (click "Start Comparison" again)
- Close and return to Studio
- Fix API keys in Settings → Providers

### Path G: Vertical Layout
User toggles to vertical layout (⬛ Vertical) during comparison:

1. Panels stack vertically instead of side-by-side
2. Each panel gets full horizontal width
3. Useful for: comparing long responses (code, detailed explanations) where horizontal space is too cramped
4. Switchable at any time — conversation state preserved

### Path H: 3 or 4 Personas
User selects 3 Personas: three side-by-side panels. 4 Personas: 2×2 grid in horizontal mode, 4 stacked rows in vertical mode. All panels stream simultaneously. Per-panel text inputs and Accept buttons function identically.

---

## Failure Points

| Failure | Handling |
|---------|----------|
| One panel fails (API error) | Red error banner in that panel with **[Retry]**. Other panels unaffected. |
| All panels fail | "All comparisons failed. Check API keys and network connection." |
| Network error | Yellow banner: "Network error." + Retry per panel. |
| Close during active generation | Confirmation: "Responses are still being generated. Stop and discard?" |
| Minimum Personas not met | "Select at least 2 Personas to compare." Start button disabled. |
| Maximum Personas exceeded | Additional checkboxes grayed out. "Maximum 4 Personas for comparison." |
| Selected Persona has no valid API key | Warning on that Persona's row in the checklist: "⚠️ No valid API key." User can still select it, but the panel will fail on Start. |

---

## Edge Cases

1. **User accepts a panel, then immediately uses Undo (Ctrl+Z) in the main chat:** Undo reverts the message append. The branch data for other conversations is also reverted (since the branch node was based on the accepted message). The comparison conversations are lost — they were transient and not saved to disk except as branch data. User would need to re-run the comparison.

2. **Originating chat has existing content before comparison:** The accepted conversation is appended AFTER the existing messages. The comparison prompt becomes a new user message in the chat. If the chat was empty, the comparison prompt is the first message.

3. **User triggers comparison from a chat that already has branch data:** The accepted conversation creates a new branch point. Existing branches are preserved. The new branch contains the comparison follow-up path.

4. **User switches Persona mid-comparison in one panel:** The panel's Persona selector allows switching. The new Persona is used for all SUBSEQUENT messages in that panel. Previous messages retain their original Persona labels.

5. **Very long prompt (near context limit):** The token counter warns if the prompt is too large for any selected Persona's context window. User can reduce the prompt or remove Personas with smaller context windows.

6. **One Persona uses Thinking mode, another doesn't:** Panels render independently. One shows thinking blocks, the other doesn't. Both stream simultaneously.

7. **User opens a second comparison while one is active:** Not allowed. A message: "A comparison is already in progress. Close the current comparison before starting a new one."

8. **Comparison conversations are extremely long (50+ exchanges per panel):** Performance may degrade. The comparison view is designed for testing/evaluation, not marathon conversations. If the user needs extended multi-turn, they should Accept early and continue in the main Studio chat.

---

## Completion
**Happy path ending:** The user accepted the Python Expert's conversation. The main Studio chat now contains the multi-turn comparison conversation that produced the best result. The General Assistant's alternative responses are saved as a branch — reviewable later via the branch navigator (D3) if the user wants to compare or extract insights from both.

**Close-without-accept ending:** Nothing was saved. The user returns to the Studio chat exactly as it was. The comparison was exploratory.

**The user's primary benefit:** Side-by-side model comparison eliminates the "try one model, copy-paste, try another model" friction. The user can have independent multi-turn conversations with multiple Personas simultaneously and cherry-pick the best result. Nothing is lost — rejected conversations are saved as branches for later reference.

---

## Cross-References
- Feature spec: [`features/model-comparison.md`](../features/model-comparison.md) M1-M4 — Model Comparison Mode
- Feature spec: [`features/model-configurations-personas.md`](../features/model-configurations-personas.md) B3, B4 — Personas, Persona Selection
- Feature spec: [`features/chat-modes-controls.md`](../features/chat-modes-controls.md) E3 — Thinking Toggle
- Feature spec: [`features/message-manipulation-branching.md`](../features/message-manipulation-branching.md) D3 — Branch Navigation
- Feature spec: [`features/studio-chat-workspace.md`](../features/studio-chat-workspace.md) C26 — Compare button
- Feature spec: [`features/studio-chat-workspace.md`](../features/studio-chat-workspace.md) C2-C4 — Markdown rendering, streaming
- Screen: [`screens/model-comparison.md`](../screens/model-comparison.md) — Full screen spec
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Originating chat and return destination
