# Model Comparison — Feature Spec

## What the User Accomplishes
The user sends the same prompt to multiple Personas simultaneously and compares responses side-by-side. The best response can be accepted into the chat; others can be saved as branches or discarded.

## Trigger
- "Compare" button in textbox toolbar
- Menu option: Chat → Compare Models

## Detailed Behavior

### M1. Model Comparison Mode
- Transient view — responses displayed temporarily for comparison only
- Not a permanent chat. Comparison results don't appear in sidebar.
- Accessible from any existing chat

### M2. Comparison Setup
- **Persona Selection:** Multi-select list. Choose 2+ Personas (minimum 2).
- **Prompt:** Single text input shared across all selected Personas.
- **Layout:** User chooses horizontal (side-by-side columns) or vertical (stacked rows).
- **Start:** "Compare" button sends prompt to all selected Personas simultaneously.

### M3. Comparison Results
- **Each Panel Shows:** Persona name, Model Configuration name, streaming response, response time (updated on completion), token count (prompt + completion), estimated cost.
- **Simultaneous Streaming:** All responses stream in parallel.
- **Stop Controls:** "Stop All" button. Individual "Stop" button per panel.
- **Panel Resizing:** Panels can be resized if horizontal layout.

### M4. Accepting a Comparison Result
- **"Accept" Button:** On each panel. Accepts that response.
- **Effect:** Accepted response appended to the originating ChatThread as a normal assistant message. The chat continues from there.
- **Unaccepted Responses:** Discarded from chat by default. User option: "Save as branches" — saves unaccepted responses as alternate branches (D3) on the same message node for future reference.
- **Close Comparison:** X button closes comparison view. If no response accepted, nothing added to chat.

## Data
- Comparison results are transient — not stored as permanent data
- If "Save as branches" used: creates branch data per D3

## Success/Failure States
- **All Panels Complete:** Each panel shows final response with metrics.
- **One Panel Fails:** That panel shows error while others continue. "Retry" button on failed panel.
- **All Panels Fail:** "All comparisons failed. Check API keys and network connection."
- **Minimum Personas:** "Select at least 2 Personas to compare."

## Permissions
- Single-user app.

## Interactions
- M1/M2 selects from Personas (B3) and their Model Configurations (B2)
- M4 appends to current ChatThread; optional branch creation (D3)
- Token counts and costs feed into S (Usage Dashboard)
