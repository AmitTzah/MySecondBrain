# Chat Organization & Search — Feature Spec

## What the User Accomplishes
The user organizes, searches, and navigates their chat history. Permanent chats appear in a sortable, filterable sidebar. Transient actions appear in a Timeline tab. Full-text search spans all chats. Chats can be favorited, tagged, pinned, archived, colored, and organized into folders.

## Trigger
- Studio sidebar (always visible)
- Ctrl+Shift+F for global search (L3)
- Right-click on chat for context menu (L12)

## Layout
Sidebar with two tabs: "Chats" (permanent) and "Timeline" (transient). See [`screens/studio-chat.md`](screens/studio-chat.md).

## Detailed Behavior

### L1. Sidebar Chat List
- **Content:** All permanent chats (IsTransient=false). Sorted per L13.
- **Pinned Chats:** Appear at top, above date groups.
- **Date Groups:** Today, Yesterday, This Week, This Month, Older.
- **Each Entry Shows:** Chat title, last message preview (1 line, truncated), relative timestamp, favorite star (if starred), tags (if any), color dot (L14).
- **Active Chat:** Highlighted entry.
- **Loading:** Skeleton entries while loading chat list.

### L2. Chat Favoriting
- **Toggle:** Star icon on each chat entry. Click to toggle.
- **Filter:** "Favorites" toggle at top of sidebar shows only favorited chats.
- **Persistence:** Favorite state persisted across sessions.

### L3. Full-Text Search
- **Trigger:** Ctrl+Shift+F or click search bar at top of sidebar.
- **Scope:** All chat messages — both permanent and transient (within retention window).
- **Results Display:** List of matching messages showing: snippet with highlighted search terms, parent chat name, timestamp. Grouped by chat.
- **Click Result:** Opens chat and scrolls to matching message.
- **Search Bar:** Search-as-you-type with debounce (300ms).
- **Empty State:** "No results for '[query]'"

### L4. Delete Chat (Soft-Delete → Trash)
- **Soft-Delete:** Deleting a chat moves it to Trash, NOT permanent deletion.
- **Confirmation Dialog:** "Move '[chat title]' to Trash? It will be permanently deleted after 30 days." Options: "Move to Trash" / "Cancel"
- **Result:** Chat marked `IsDeleted=true` with `DeletedAt` timestamp. Disappears from chat list, pinned, folders, and search. Appears in Trash view (🗑️ in sidebar).
- **If chat is open in a tab:** Tab closes automatically. Toast: "'[title]' moved to Trash. Tab closed."
- **Toast after delete:** "Moved to Trash. Restore within 30 days."
- **Trash View:** Accessible from sidebar navigation. Shows all soft-deleted chats sorted by deletion date. Each entry: title, deletion date, Restore button, Delete Permanently button.
- **Restore:** Removes `IsDeleted` flag. Chat returns to original location (folder, tags, pinned status preserved). Toast: "'[title]' restored."
- **Permanent Delete (from Trash):** Confirmation: "Permanently delete '[title]'? This cannot be undone." On confirm: hard deletes per O5.
- **30-Day Auto-Purge:** Background task permanently deletes chats where `IsDeleted=true` AND `DeletedAt` > 30 days ago. No user notification.
- **Empty Trash:** Button at top of Trash view. Confirmation: "Permanently delete all [N] items in Trash?" On confirm: hard deletes all.
- See [`features/soft-delete-trash.md`](../features/soft-delete-trash.md) for full spec.

### L5. Timeline Tab
- **Access:** Second tab in sidebar: "Timeline"
- **Content:** Chronological feed of ALL transient actions (Tier 1 hotkey rewrites + Tier 2 Command Bar queries)
- **Each Entry:** Action type icon, content preview (truncated), timestamp, source application (if HWND captured)
- **Date Groups:** Same as L1
- **Click Entry:** Opens the transient chat in Studio. If user sends reply, IsTransient flips to false (O3).
- **Empty State:** "No recent actions. Use Alt+Space for quick queries or assign hotkeys to Text Actions in Settings."

### L6. Sidebar Filtering
- **Default View:** "Chats" tab = permanent chats only
- **Toggle:** Click "Timeline" tab = transient actions
- **Favorites Filter:** Within Chats tab, toggle to show only favorited

### L7. Chat Tags/Labels
- **Add Tags:** Right-click chat → "Add Tags" → type tag name(s), comma-separated or Enter
- **Existing Tags:** Auto-complete from previously used tags
- **Display:** Tags appear as small labels on chat entry (max 3 visible, "+N more" overflow)
- **Filter:** Click tag to filter sidebar by that tag. Click again to clear filter.
- **Examples:** "coding", "writing", "research", "personal"

### L8. Pin Chats
- **Toggle:** Right-click → "Pin" / "Unpin", or pin icon on chat entry
- **Display:** Pinned chats appear at top of sidebar, above date groups and all other chats
- **Order:** Pinned chats maintain their own sort order (per L13)

### L9. Chat Folders/Collections
- **Create Folder:** "New Folder" button in sidebar header
- **Move Chat:** Drag chat onto folder, or right-click → "Move to Folder"
- **One Folder per Chat:** A chat can only be in one folder at a time
- **Folder Display:** Expandable/collapsible in sidebar
- **"Unfiled" Section:** Chats not in any folder

### L10. Chat Archiving
- **Archive:** Right-click → "Archive". Chat hidden from default view.
- **View Archived:** "Archived" filter in sidebar header
- **Unarchive:** Right-click archived chat → "Unarchive"
- **Exclusion:** Archived chats excluded from 7-day auto-cleanup (even if transient)
- **Visual:** Archived chats shown with archive icon

### L11. Bulk Operations
- **Enter Selection Mode:** "Select" button in sidebar header, or Ctrl+click entries
- **Select All:** Checkbox in header
- **Bulk Actions Bar:** Appears when items selected: Delete, Archive, Export, Tag, Move to Folder
- **Confirmation:** Destructive actions show confirmation: "Delete [N] chats?"

### L12. Context Menu (⋯ Button + Right-Click)
Click ⋯ (three-dot) button on any chat entry in sidebar (visible on hover), OR right-click the chat entry. Menu options: Rename, Delete, Archive/Unarchive, Duplicate (D7), Export (I1), Pin/Unpin, Add/Edit Tags, Move to Folder, Color Label (L14). Destructive actions show confirmation.

### L13. Chat Sorting Options
- **Sort Dropdown:** In sidebar header
- **Options:** Most Recent (default), Name (A-Z), Date Created, Last Activity
- **Persistence:** Sort order remembered across sessions

### L14. Chat Color Labels
- **Assign:** Right-click → "Color Label" → select color from preset palette (8-12 colors)
- **Display:** Colored dot on left edge of chat entry
- **Purpose:** Quick visual identification of important chats

## Data
- [`data/chat-thread.md`](data/chat-thread.md) — all organization metadata (tags, pin, folder, archive, color, favorite)

## Success/Failure States
- **Empty Sidebar:** "No chats yet. Press Ctrl+N to start a new conversation."
- **Empty Timeline:** "No recent actions. Use Alt+Space for quick queries."
- **Search No Results:** "No results for '[query]'"
- **Delete Confirmed:** Chat removed. Toast with Undo (5s).

## Permissions
- Single-user app.

## Interactions
- L3 searches across all chats (permanent + transient in window)
- L5 shows threads from K3 (Tier 1) and K4 (Tier 2)
- L4 triggers O5 (garbage collection)
- L10 prevents O4 auto-cleanup for archived threads
