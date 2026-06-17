# Message Manipulation & Branching — Feature Spec

## What the User Accomplishes
The user edits, deletes, branches, and navigates conversation messages. Every message is versioned — editing creates branches rather than destroying history. The user can explore alternative conversation paths, fork chats, and undo changes.

## Trigger
- Edit button on any message
- Delete button on any message
- Branch navigation arrows on edited messages
- Chat Tree button in chat header
- Quote button on selected text
- Chat Navigation Bar toggle
- Duplicate/Fork from context menu
- Thumbs up/down on assistant messages
- Ctrl+Z / Ctrl+Y after edit/delete

## Detailed Behavior

### D1. Edit Any Past Message
- **Access:** Click edit icon on any message (user or assistant)
- **Edit Mode:** Message content opens in editable text area. For assistant messages, Markdown source is editable.
- **Two Modes (user chooses after editing):**
  - **Edit in Place:** Overwrites message content. No branch created. All subsequent messages remain unchanged in the conversation flow. The original content is preserved in version history but not as a navigable branch.
  - **Edit as Branch:** Creates a new version branch at that point. Original message preserved as v1, edit becomes v2. Branch indicator "2/2" appears. All subsequent AI calls use edited history.
- **Warning:** If the message being edited has subsequent messages that depend on it (especially assistant responses), editing in place may create logical inconsistency. A subtle warning appears: "Editing this message may affect context for subsequent messages."
- **Permission:** User can edit any message in any of their chats.

### D2. Delete Any Past Message
- **Access:** Click delete icon on any message
- **Confirmation:** "Delete this message? It will be removed from the current conversation history."
- **Effect:** Message removed from current conversation history. Subsequent LLM calls use updated history (without deleted message).
- **Branch Preservation:** If message had branches, branch data preserved and navigable.
- **Undo:** Ctrl+Z immediately after delete restores the message.

### D3. Branch Navigation
- **Indicator:** Edited messages show branch indicator: "v2/3" meaning "version 2 of 3"
- **Navigation:** Left/right arrow buttons on the indicator. Click to cycle through versions.
- **Effect:** Selecting different branch re-renders all subsequent messages according to that branch's history
- **Branch-Aware Generation:** When user sends new message, AI sees the currently selected branch's history
- **No Branches:** Messages without edits show no branch indicator

### D4. Chat Tree Visualization
- **Access:** "Tree" button in chat header or toolbar
- **View:** Full-width or side panel showing visual tree/graph of all message branches
- **Nodes:** Represent messages. Show truncated message preview + model/persona.
- **Edges:** Show conversation flow. Active branch path highlighted.
- **Interaction:** Click any node to navigate to that branch point. Chat view scrolls to that message and switches to that branch.
- **Empty State:** Linear conversation (no branches) shows single vertical line with message nodes.

### D5. Quote from Chat
- **Select:** Click and drag to select text in any past message
- **Quote Button:** Appears near selection: "Quote"
- **Effect:** Inserts selected text as Markdown blockquote (`> text`) in current message input at cursor position
- **Attribution:** Quote includes attribution: `> **You said:** text` or `> **[Persona] said:** text`

### D6. Chat Navigation Bar
- **Toggle:** Collapsible panel (button in chat header or sidebar)
- **Content:** Scrollable list of all messages in current branch
- **Each Entry:** Message number (#1, #2...), role icon (user/assistant), first line preview
- **Active Indicator:** Current position highlighted
- **Click:** Scrolls conversation view directly to that message
- **Use Case:** Quick jumping in long conversations

### D7. Duplicate / Fork Chat
- **Access:** Right-click any message → "Fork from here"
- **Effect:** Creates new ChatThread containing all messages up to and including the selected message
- **New Chat:** Opens in new tab. Original chat unchanged.
- **Use Case:** "I want to explore a different direction from this point without affecting the original conversation"

### D8. Message Feedback
- **Buttons:** Thumbs-up and thumbs-down on each assistant message
- **Toggle:** Click to give feedback, click again to remove
- **Storage:** Feedback stored with message. Visual indicator on messages with feedback.
- **Use Case:** Track which prompts/models produce best results for personal reference

### D9. Undo/Redo Message Edits
- **Scope:** Edit (D1), Delete (D2), Regenerate (C5)
- **Shortcut:** Ctrl+Z (undo), Ctrl+Y (redo)
- **Stack:** Per-chat. Persists until chat tab closed.
- **Granularity:** Each edit/delete/regenerate is one undo step
- **Visual:** No visual indicator of undo stack depth needed

## Data
- [`data/message.md`](data/message.md) — messages have version chains and branch metadata
- [`data/chat-thread.md`](data/chat-thread.md) — threads track active branch

## Success/Failure States
- **Success — Edit:** Message updates in-place. Branch indicator appears if branch mode used.
- **Success — Delete:** Message removed. Subsequent messages shift up.
- **Success — Branch Switch:** Subsequent messages re-render. Brief loading if regenerating AI responses.
- **Failure — Undo Stack Empty:** Ctrl+Z does nothing (no visual feedback beyond no change)

## Permissions
- Single-user app. All actions available to the sole user.

## Interactions
- D1/D2 affect message history seen by AI on next send
- D3/D4 work with C1 (conversation view) to re-render messages
- D5 feeds into textbox input
- D6 complements C1 for navigation
- D7 creates new ChatThread (O1)
- D9 works with C5 (regenerate undo)
