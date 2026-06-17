# Soft-Delete Trash — Feature Spec

## What the User Accomplishes
The user can safely delete chats knowing they can be recovered within 30 days. Deleted chats move to a "Trash" folder where they remain for 30 days before permanent deletion. The user can restore chats from Trash or permanently delete them immediately.

## Trigger
- Delete chat from sidebar context menu (L12)
- Delete chat from chat header three-dot menu (C16a)
- Bulk delete from sidebar (L11)
- "Empty Trash" from Trash view
- "Restore" from Trash view

## Detailed Behavior

### T1. Soft-Delete on Chat Deletion
- When user deletes a chat, it is NOT immediately hard-deleted.
- The ChatThread is marked as `IsDeleted=true` with `DeletedAt` timestamp.
- The chat disappears from the main chat list, pinned section, folders, and search results.
- The chat appears in a new "Trash" section accessible from the sidebar.
- Confirmation dialog on delete: "Move '[chat title]' to Trash? It will be permanently deleted after 30 days." Options: "Move to Trash" / "Cancel"
- Toast after deletion: "Moved to Trash. Restore within 30 days."

### T2. Trash View
- Accessible from sidebar: a "🗑️ Trash" item at the bottom of the navigation (below Settings, or as a tab in the chat list).
- Shows all soft-deleted chats sorted by deletion date (newest first).
- Each entry shows: chat title, deletion date ("Deleted 3 days ago"), "Restore" button, "Delete Permanently" button.
- Empty state: "Trash is empty. Deleted chats appear here and are automatically removed after 30 days."
- Count indicator: "Trash ([N])" showing number of items.

### T3. 30-Day Auto-Purge
- Background task runs daily.
- Permanently deletes (hard deletes) all ChatThreads where `IsDeleted=true` AND `DeletedAt` is more than 30 days ago.
- Hard delete follows O5 (Garbage Collection Policy): exclusively linked media/artifacts are also permanently deleted.
- No notification to user — this is a background cleanup.

### T4. Restore from Trash
- "Restore" button on any trashed chat.
- Removes `IsDeleted` flag, clears `DeletedAt`.
- Chat reappears in its original location (folder, pinned status, tags preserved).
- Toast: "'[title]' restored."
- No confirmation dialog (undoable via re-delete).

### T5. Permanent Delete from Trash
- "Delete Permanently" button on any trashed chat.
- Confirmation: "Permanently delete '[title]'? This cannot be undone. Linked media and artifacts used only by this chat will also be deleted."
- On confirm: hard deletes the ChatThread per O5.
- Toast: "'[title]' permanently deleted."

### T6. Empty Trash
- "Empty Trash" button at top of Trash view.
- Confirmation: "Permanently delete all [N] items in Trash? This cannot be undone."
- On confirm: hard deletes ALL soft-deleted chats per O5.
- Toast: "Trash emptied. [N] chats permanently deleted."

### T7. Deleted Chat Tab Handling
- If a soft-deleted chat is currently open in a tab: the tab closes automatically.
- Toast: "'[title]' moved to Trash. Tab closed."

## Data
- ChatThread gains two new attributes:
  - `IsDeleted` (boolean, default: false)
  - `DeletedAt` (timestamp, null when not deleted)
- See [`data/chat-thread.md`](../data/chat-thread.md) for the full entity spec.

## Success/Failure States
- **Delete Success:** "Moved to Trash. Restore within 30 days."
- **Restore Success:** "'[title]' restored."
- **Permanent Delete Success:** "'[title]' permanently deleted."
- **Empty Trash Success:** "Trash emptied. [N] chats permanently deleted."
- **Empty Trash (already empty):** "Trash is already empty."

## Permissions
- Single-user app. All trash operations available to the sole user.

## Interactions
- T1 modifies L4 (Delete Chat) in [`features/chat-organization-search.md`](../features/chat-organization-search.md)
- T3 follows O5 (Garbage Collection Policy) in [`features/data-model-lifecycle.md`](../features/data-model-lifecycle.md)
- Trash view appears in Studio sidebar (screens/studio-chat.md)
- Settings may include "Trash auto-purge days" configuration in a future update (deferred — 30 days is fixed for now)

## Edge Cases
- **Chat deleted while AI is generating:** Stop generation, move to Trash. Partial response preserved in Trash.
- **Restore chat whose folder was deleted:** Chat restored to "Unfiled."
- **Restore chat whose tags were deleted:** Tags restored as they were.
- **Locked chat moved to Trash:** Remains locked. Restoring requires password. Permanent deletion of locked chat does NOT require password (user explicitly confirmed).
- **Bulk delete with mixed locked/unlocked:** Each locked chat requires individual password. Non-locked chats move to Trash immediately.
