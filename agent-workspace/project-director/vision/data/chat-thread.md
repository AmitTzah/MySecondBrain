# ChatThread — Data Entity

## Description
A ChatThread is the core container for all AI interactions, regardless of origin tier. Every hotkey rewrite (Tier 1), Command Bar query (Tier 2), or Studio conversation (Tier 3) creates a ChatThread containing Messages. This is the "Everything is a Thread" architecture.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| title | string | Yes | Max 200 chars. Auto-generated or user-edited | Chat display title |
| isTransient | boolean | Yes | Default: true for Tier 1/2, false for Tier 3 | Whether subject to 7-day auto-cleanup |
| createdAt | datetime | Yes | Auto-set on creation | Thread creation timestamp |
| lastActivityAt | datetime | Yes | Updated on any message activity | Last activity timestamp |
| personaId | string (FK) | Yes | References Persona | Active Persona for this thread |
| systemMessage | string | No | Overrides Persona default | Per-chat custom system message (E5) |
| chatMode | enum | Yes | Standard, TextCompletion | Chat mode (E1, E2) |
| thinkingEnabled | boolean | Yes | Default: false | Thinking toggle state (E3) |
| isMuted | boolean | Yes | Default: false | Per-chat mute state (E4) |

### Source Context (nullable — only for Tier 1 elevation)
| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| sourceHWND | integer | No | Nullable. Valid Windows HWND | Captured window handle (P2) |
| sourceAppName | string | No | Nullable. e.g., "Word", "VS Code" | Source application name |
| sourceDocTitle | string | No | Nullable. e.g., "chapter3.docx" | Source document/window title |
| originalHighlightedText | string | No | Nullable | Text that was originally highlighted |

### Organization
| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| isFavorite | boolean | Yes | Default: false | Favorited state (L2) |
| isPinned | boolean | Yes | Default: false | Pinned to sidebar top (L8) |
| isArchived | boolean | Yes | Default: false | Archived state (L10) |
| colorLabel | string | No | Nullable. Hex color or preset name | Color label (L14) |
| tags | string[] | No | Array of tag strings | User-defined tags (L7) |
| folderId | string | No | Nullable. References Folder | Parent folder (L9) |

## Lifecycle

### Create
- **Tier 1:** Auto-created when hotkey Text Action is triggered (K3). isTransient=true. Source context populated.
- **Tier 2:** Auto-created when Command Bar is opened (K4). isTransient=true.
- **Tier 3:** Created when user presses Ctrl+N or "New Chat" (K5). isTransient=false.
- **Import:** Created when importing from ChatGPT/Claude (I2). isTransient=false.
- **Fork:** Created when duplicating/forking a chat (D7). Copies messages up to fork point. isTransient=false.

### Update
- **Title:** User edits inline (C7) or AI auto-generates on first message.
- **Persona Change:** User switches Persona from toolbar (B4). Updates personaId and systemMessage.
- **Elevation:** When user sends reply in transient thread in Studio → isTransient flips to false (O3).
- **Organization:** User sets favorite, pin, archive, color, tags, folder via sidebar actions.
- **System Message:** User edits per-chat system message (E5).

### Delete
- **Manual:** User deletes from sidebar (L4). Confirmation dialog. Cascading per O5.
- **Auto-Cleanup:** Background task deletes threads where isTransient=true AND age > 7 days AND no exceptions apply (O4).
- **Hard Delete:** Permanent. Media/artifacts exclusively linked are also deleted (O5).

## Relationships
- **has many** Messages (one ChatThread contains many Messages)
- **belongs to** Persona (via personaId)
- **has many** Artifacts (generated during conversation)
- **has many** MediaItems (uploaded or generated during conversation)
- **references** Model Configuration (indirectly via Persona)
- **optionally references** source application (HWND context)

## UI Visibility
- [`screens/studio-chat.md`](screens/studio-chat.md) — ChatThread displayed as tabbed conversation
- Sidebar Chat List (L1) — Title, preview, timestamp, star, tags
- Timeline Tab (L5) — Transient threads displayed chronologically
- Search Results (L3) — Thread title shown with matching messages
- [`screens/settings.md`](screens/settings.md) — Not directly displayed, but Persona/model configs that reference threads are managed here
