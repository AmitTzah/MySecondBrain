# Elevate Transient to Permanent — User Flow

## Persona
**The Hybrid Developer / Knowledge Worker / Creative Writer** — has used Tier 1 (hotkey rewrite) or Tier 2 (Command Bar) for a quick AI interaction, and now realizes the conversation is worth keeping. Wants to promote the transient chat to a permanent one that appears in the main chat list, is searchable, and won't be auto-deleted.

## Goal
Take a transient ChatThread (created by Tier 1 or Tier 2, flagged IsTransient=true) and elevate it to a permanent chat (IsTransient=false) so it persists indefinitely, appears in the main sidebar chat list, and supports full Studio features.

## Starting Point
The user has an existing transient ChatThread, created via one of these paths:
- **Tier 1 hotkey:** User pressed Alt+Q, got a rewrite result, clicked "Open in Studio" (from popup) — OR — clicked Discard but the chat was auto-saved as transient
- **Tier 2 Command Bar:** User pressed Alt+Space, had a Q&A, and either closed the bar (saved as transient) or popped out to mini-window

The transient chat is visible in the **Timeline tab** of the Studio sidebar ([`screens/studio-chat.html`](../screens/studio-chat.html) sidebar → Timeline).

---

## Happy Path (Open from Timeline → Send Reply)

### Step 1: Discover the Transient Chat
The user opens Studio and clicks the **Timeline** tab in the sidebar. The Timeline shows a chronological feed of all transient actions:

- Each entry shows: action type icon (⌨️ for Tier 1, 🔍 for Tier 2), text preview (first ~80 chars), relative timestamp, source app name
- Tier 1 entries show the Text Action name (e.g., "Rewrite — Code")
- Tier 2 entries show the first query text
- Entries are sorted newest-first

The user finds the relevant transient entry (e.g., "Rewrite — Code: 'The OAuth 2.0 flow begins when...'") from 2 hours ago.

### Step 2: Open the Transient Chat
The user clicks the entry in the Timeline. A new chat tab opens in the Studio with the full conversation:

- For Tier 1: shows captured text as user message + AI response as assistant message
- For Tier 2: shows all Q&A pairs from the Command Bar session
- The chat header shows the Persona used and the source context (e.g., "Source: Code — app.ts")

At this point, the chat is STILL transient (IsTransient=true). Simply opening it in Studio does NOT elevate it.

### Step 3: Send a Reply (The Elevation Trigger)
The user types a follow-up message in the textbox and clicks **Send** (or presses Enter).

**This is the elevation trigger per [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O3:** Sending a reply in a transient thread flips IsTransient from true to false.

**What happens:**
1. IsTransient flips to false
2. The chat now appears in the main **Chats** tab of the sidebar (moves from Timeline to Chats)
3. The chat is now permanent — excluded from 7-day auto-cleanup (O4)
4. The chat entry disappears from the Timeline tab (it's no longer transient)
5. The chat tab title is auto-generated from the conversation (C7) if not already titled
6. All Studio features are now available: artifacts, branching, Write to Wiki, model comparison, export, etc.

The user's follow-up message is sent to the AI and the conversation continues normally.

---

## Alternative Paths

### Path B: Elevate via Open in Studio (Tier 1)
From the Tier 1 result popup, user clicks **"Open in Studio"**:

1. Studio window opens/focuses
2. A new chat tab opens with the captured text + AI response
3. IsTransient is flipped to false **immediately** (no reply needed) — this is the "Open in Studio" elevation path, distinct from simply opening a transient chat
4. The chat appears in the Chats list
5. The popup remains open — user can still Accept or Discard independently
6. The chat is now permanent and ready for follow-up

### Path C: Elevate via Open in Studio (Tier 2)
From the Command Bar mini-window, user clicks **"Open in Studio"**:

1. Studio window opens/focuses
2. A new chat tab opens with all Q&A pairs from the Command Bar session
3. IsTransient flips to false immediately
4. The Command Bar mini-window closes
5. The chat appears in the Chats list

### Path D: Auto-Elevation via Exception
Per [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O4, a transient chat is automatically elevated (IsTransient flips to false) if ANY of these occur BEFORE the 7-day cleanup:

- User stars the chat (IsFavorite = true, L2)
- User assigns tags to the chat (L7)
- User pins the chat (IsPinned = true, L8)
- User archives the chat (IsArchived = true, L10)
- User creates a branch in the chat (D1, D3) — including editing a message as a branch
- The chat contains artifacts (F1) — AI generated code/docs during the transient interaction
- The chat contains user replies (covered by Path A above)

These actions trigger auto-elevation silently in the background. The chat moves from Timeline to Chats. No user notification is shown (the elevation is a natural consequence of treating the chat as important).

### Path E: Elevate via "Make Permanent" Explicit Action
From the chat header three-dot menu (⋯), the user can explicitly click **"Make Permanent"** (visible only for transient chats):

1. IsTransient flips to false immediately
2. Chat moves from Timeline to Chats
3. No reply needed
4. The "Make Permanent" option is replaced by "Make Temporary" (C30)

### Path F: Multiple Transient Chats Elevated at Once
The user can use bulk operations (L11) in the Timeline tab to select multiple transient entries and elevate them all at once via a "Make Permanent" bulk action. Each chat flips to IsTransient=false and moves to the Chats list.

---

## Failure Points

| Failure | Handling |
|---------|----------|
| User sends reply but API call fails | Elevation STILL happens (IsTransient flips on send action, not on successful API response). The chat moves to Chats. The failed message shows error with Retry. |
| User tries to elevate while offline | Elevation still works (it's a local data change — flipping a boolean). The chat moves to Chats. Sending a reply will fail with network error, but the chat is already permanent. |
| Transient chat was auto-cleaned before user could elevate | The chat no longer exists — hard deleted per O4. Timeline entry disappears. No recovery possible. |

---

## Edge Cases

1. **User opens a transient chat in Studio, reads it, closes the tab without sending a reply:** The chat remains transient. No elevation occurs. The chat stays in the Timeline tab.

2. **User opens a transient chat, edits a past message (D1), but doesn't send a new reply:** If the edit creates a branch ("Edit as Branch"), this triggers auto-elevation (Path D — branching exception). If the edit is "Edit in Place" (overwrite), elevation does NOT occur.

3. **Transient chat is 6 days old when elevated:** Elevation works normally. The chat becomes permanent. The 7-day timer is irrelevant once IsTransient=false.

4. **User elevates via "Open in Studio" from Tier 1 popup, then also clicks Accept:** Both actions happen independently. The Studio chat is permanent. The source application text is updated. No conflict.

5. **Chat elevated by reply, then user immediately clicks "Make Temporary" (C30):** The chat flips back to IsTransient=true. The 7-day timer resets (CreatedAt remains the original creation time, but cleanup logic re-evaluates based on the flag). Chat moves back to Timeline.

6. **Transient chat is part of a Chat Folder (L9):** Folders only contain permanent chats. If a transient chat is elevated, it retains its folder assignment (folders are assigned via right-click, which presumably only works on permanent chats). If somehow assigned, it appears in the folder after elevation.

7. **7-day auto-cleanup runs while user is viewing a transient chat in Studio:** The cleanup skips chats that are currently open in active tabs. The chat survives this cleanup cycle. If the user closes the tab without elevating, it will be cleaned on the next cycle.

---

## Completion
**Happy path ending:** The once-transient chat is now a permanent ChatThread in the main Chats list. It has a title, is searchable (L3), can be exported (I1), saved to wiki (N5), and will persist indefinitely. The user can continue the conversation with full Studio features. The Timeline tab reflects the change — the entry is gone from Timeline, now visible in Chats.

**The user's primary benefit:** No interaction is lost. A quick hotkey rewrite that turned out to be valuable, or a Command Bar Q&A that sparked a deeper investigation — both can be promoted to permanent, fully-featured conversations with a single action. The "transient by default, permanent on demand" model ensures the chat list doesn't bloat with one-off interactions while preserving the ability to keep anything important.

---

## Cross-References
- Feature spec: [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md) O2-O4 — IsTransient, elevation, auto-cleanup
- Feature spec: [`features/text-actions-three-tier.md`](../features/text-actions-three-tier.md) K3 — Tier 1 "Open in Studio"
- Feature spec: [`features/text-actions-three-tier.md`](../features/text-actions-three-tier.md) K4 — Tier 2 elevation
- Feature spec: [`features/chat-organization-search.md`](../features/chat-organization-search.md) L5 — Timeline tab
- Feature spec: [`features/chat-organization-search.md`](../features/chat-organization-search.md) L2 — Favoriting, L7 — Tags, L8 — Pinning, L10 — Archiving
- Feature spec: [`features/message-manipulation-branching.md`](../features/message-manipulation-branching.md) D1, D3 — Branching triggers auto-elevation
- Feature spec: [`features/artifacts-side-panel.md`](../features/artifacts-side-panel.md) F1 — Artifacts trigger auto-elevation
- Feature spec: [`features/studio-chat-workspace.md`](../features/studio-chat-workspace.md) C30 — Make Temporary toggle
- Screen: [`screens/studio-chat.md`](../screens/studio-chat.md) — Timeline tab, chat list, chat header
