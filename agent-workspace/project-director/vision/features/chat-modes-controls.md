# Chat Modes & Controls — Feature Spec

## What the User Accomplishes
The user controls how AI conversations are structured: Standard chat mode (user/assistant turns), Text Completion mode (raw prompt → raw completion), Thinking toggle (extended reasoning), per-chat mute, and dynamic system message editing.

## Trigger
- Chat creation: mode set by Persona default (B3)
- In-chat: controls in textbox toolbar (K2)
- System message: chat header or Chat Navigation Bar (D6)

## Detailed Behavior

### E1. Standard Chat Mode
- Default mode. Messages follow user/assistant conversational structure.
- Each user message paired with an assistant response.
- Full conversation history maintained and sent as context.
- This is the mode for 99% of chats.

### E2. Text Completion Mode
- Alternative mode. No user/assistant structure.
- User provides raw text prompt → AI returns raw text completion.
- No conversation history maintained (each prompt is independent).
- Suitable for models supporting text completion APIs (as opposed to chat APIs).
- **Mode Indicator:** Chat header shows "Text Completion Mode" label.
- **Input:** Single-line or multi-line text input without Send button styling.
- **Switching:** Set at chat creation via Persona, or switch mid-chat from textbox toolbar. Switching mid-chat warns: "Switching modes will not preserve conversation history."
- ⚠️ FLAGGED: Text completion APIs are being deprecated by some providers. The Architect should verify API availability for target providers.

### E3. Thinking Toggle
- Control in textbox toolbar: toggle switch or button with brain/lightbulb icon.
- **When Enabled:** AI may display its reasoning chain (thinking process) before the final answer. Thinking appears as expandable/collapsible section.
- **When Disabled:** AI responds directly without showing reasoning.
- Only functional for models that support extended thinking (e.g., Claude's extended thinking, OpenAI o1).
- For models without thinking support: toggle is grayed out with tooltip "This model does not support extended thinking."
- **Per-Chat:** Setting applies to current chat only.

### E4. Mute Notifications Toggle
- Control in textbox toolbar: speaker/mute icon toggle.
- **When Muted:** Sound notification (A4) suppressed for this specific chat.
- Other chats continue to play notifications normally.
- Visual indicator: muted speaker icon when active.

### E5. Dynamic System Message Editing
- **Access:** Chat header → click system message indicator, OR Chat Navigation Bar (D6) → system message entry at top.
- **View:** Opens system message in editable text area.
- **Edit:** Modify the system prompt text. Changes take effect for all SUBSEQUENT messages.
- **Reset:** "Reset to Persona Default" button reverts to the Persona's original system prompt.
- **Effect:** Past messages retain the system message that was active when they were generated.
- **Difference from D1:** D1 edits individual past messages. E5 edits the system message that governs future AI behavior.

## Data
- Chat mode (Standard/TextCompletion) stored with ChatThread
- Thinking toggle state stored with ChatThread
- Mute state stored with ChatThread
- Current system message stored with ChatThread (may differ from Persona default)

## Success/Failure States
- **Mode Switch Warning:** "Switching to Text Completion mode will not preserve conversation history. Continue?"
- **Thinking Unavailable:** Toggle grayed out: "This model does not support extended thinking."

## Permissions
- Single-user app. All controls available to the sole user.

## Interactions
- E1/E2 set by Persona default (B3), overridable per chat
- E3 depends on Model Configuration (B2) thinking support
- E4 overrides A4 (global notification settings) per chat
- E5 separate from D1 (message editing) — governs future behavior, not past content
