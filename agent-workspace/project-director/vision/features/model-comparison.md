# Model Comparison — Feature Spec

## What the User Accomplishes
The user chats with multiple Personas simultaneously, each in its own independent panel with full conversation history. The user can send follow-up messages to individual Personas or broadcast to all at once. At any point, the user accepts one conversation into the main chat; all other conversations are automatically saved as branches.

## Trigger
- "Compare" button in textbox toolbar (C26)
- Menu option: Chat → Compare Models

## Detailed Behavior

### M1. Model Comparison Mode
- Replaces the Studio's main content area. Sidebar and right panel remain visible.
- Transient view — comparison conversations are not permanent chats.
- Comparison results don't appear in sidebar.
- Accessible from any existing chat.

### M2. Comparison Setup
- **Persona Selection:** Checklist of all Personas with checkboxes. Selected Personas appear as chips above the list. Min 2, max 4. "Select All" / "Deselect All" links. Filter input.
- **Prompt:** Single text input pre-filled from the Studio textbox. Editable.
- **Layout:** User chooses horizontal (side-by-side columns, default) or vertical (stacked rows). Changeable during comparison.
- **Start:** "Start Comparison" button (enabled when 2-4 Personas selected). Sends prompt to all selected Personas simultaneously.

### M3. Comparison Results — Independent Mini-Chats
- **Each Panel is an Independent Chat:** Each Persona gets its own panel with scrollable conversation history, text input, Send button, Stop button, and Accept button.
- **Multi-Turn Conversations:** The user can send follow-up messages to individual Personas. Each panel maintains its own conversation history independently.
- **Broadcast Toggle:** A button in the header ("🔗 Broadcast") toggles between:
  - **Off (default):** Each panel has its own text input. User chats with each Persona individually.
  - **On:** Per-panel inputs are hidden. A single centered broadcast input appears. Messages are sent to ALL Personas simultaneously. Accept buttons remain visible per panel.
- **Panel Display:** Each panel shows: Persona name, Model Configuration name, metrics (response time, token count, estimated cost on completion), streaming response with Markdown rendering, thinking blocks (collapsible with second counter per E3).
- **Stop Controls:** Per-panel "Stop" button (visible during generation). Stops that panel's stream, preserves partial response.
- **Simultaneous Streaming:** All panels stream responses in parallel.
- **Panel Resizing:** Panels resize proportionally with the results area.

### M4. Accepting a Result
- **"Accept" Button:** On each panel. Always visible.
- **Effect on Click:**
  1. The accepted panel's ENTIRE multi-turn conversation is appended to the originating ChatThread as the assistant's response sequence.
  2. All OTHER panels' conversations are automatically saved as alternate branches (D3) on the accepted message node. No manual "Save as Branch" needed.
  3. The comparison view closes immediately.
  4. User returns to the normal Studio chat view. The chat now shows the accepted conversation.
  5. Toast: "Accepted [Persona] conversation. [N] other conversations saved as branches."
- **Close Without Accepting:** ✕ button closes comparison. If no Accept clicked, nothing saved to chat. Confirmation if generation is active.

## Data
- Comparison conversations are transient during the session.
- On Accept: accepted conversation becomes Messages in the originating ChatThread.
- Other conversations automatically become branch data (D3) on the accepted message node.
- Token counts and costs feed into Usage Dashboard (S).

## Success/Failure States
- **All Panels Complete:** Each panel shows final response with metrics. Accept buttons enabled.
- **One Panel Fails:** That panel shows error with "Retry" button. Others unaffected.
- **All Panels Fail:** "All comparisons failed. Check API keys and network connection."
- **Minimum Personas:** "Select at least 2 Personas to compare." Start button disabled.
- **Maximum Personas:** Additional checkboxes grayed out. "Maximum 4 Personas for comparison."
- **Close During Generation:** Confirmation: "Responses are still being generated. Stop and discard?"

## Permissions
- Single-user app.

## Interactions
- M2 selects from Personas (B3) and their Model Configurations (B2)
- M3 uses thinking blocks (E3) per panel
- M4 appends to current ChatThread; auto-creates branches (D3) for non-accepted conversations
- Token counts and costs feed into Usage Dashboard (S)
- Broadcast toggle unifies per-panel inputs into a single shared input
