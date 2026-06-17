# Data Model & Lifecycle — Feature Spec

## What the User Accomplishes
This feature group defines how all application data is organized, persisted, and managed throughout its lifecycle. It's not a user-facing feature but a foundational specification that governs data integrity, cleanup, and retrieval.

## Data Storage
All application data is stored in a local SQLite database on the user's machine in the app's local data directory. Wiki .md files, exported files, and media saved to disk are stored as regular files on the file system — not inside the database. The SQLite database is the single source of truth for app-internal data; the wiki directory is the single source of truth for wiki content.

## Detailed Behavior

### O1. Unified ChatThread Model
- Every interaction creates a ChatThread containing one or more Messages
- Same data model regardless of origin: Tier 1 hotkey action, Tier 2 Command Bar query, Tier 3 Studio conversation
- ChatThread fields: ID, Title, IsTransient, IsPermanent (derived from !IsTransient), CreatedAt, LastActivityAt, SourceHWND (nullable), SourceAppName (nullable), SourceDocTitle (nullable), OriginalHighlightedText (nullable), PersonaID, tags, folder, isPinned, isArchived, colorLabel, isFavorite

### O2. IsTransient Flagging
- Tier 1 (K3) and Tier 2 (K4) interactions: IsTransient = true
- Tier 3 Studio-originated (K5) or elevated chats: IsTransient = false
- Flag set at ChatThread creation

### O3. Chat Elevation
- When user opens a transient thread in Studio AND sends a reply: IsTransient flips to false
- Chat becomes permanent and appears in default sidebar Chat list (L1)
- Removed from Timeline tab (L5)

### O4. 7-Day Auto-Cleanup
- **Background Task:** Runs periodically (e.g., on app startup, every 24 hours)
- **Deletes:** All ChatThreads where IsTransient = true AND CreatedAt > 7 days ago
- **Exceptions (auto-elevated to permanent):**
  - IsFavorite = true
  - Has tags assigned (L7)
  - IsPinned = true (L8)
  - IsArchived = true (L10)
  - Contains user-created branches (D1, D3)
  - Contains user replies (not just Tier 1/2 auto-messages)
  - Contains artifacts (F1)
- **Auto-Elevation:** When an exception is detected, IsTransient flips to false automatically
- **Deletion:** Hard delete — permanent and automatic. No undo after cleanup runs.

### O5. Garbage Collection Policy
When a ChatThread is hard-deleted (manually L4 or via O4 auto-cleanup):
- Media files exclusively linked to that chat → deleted from storage
- Artifact files exclusively linked to that chat → deleted from storage
- Media/artifacts shared across multiple chats → preserved
- Media/artifacts saved to disk (exported) → preserved
- Media/artifacts saved to wiki (F6/N5) → preserved
- Wiki .md files → NEVER deleted (even if created from the deleted chat)

### O6. Database Compaction
- Accessible via A9 (Database Maintenance)
- Runs SQLite VACUUM to reclaim disk space after large deletions
- Displays database size before and after compaction
- ⚠️ FLAGGED: VACUUM requires temporary free disk space equal to database size. Low-disk scenarios need handling.

## Data Entities
See individual data entity files:
- [`data/chat-thread.md`](data/chat-thread.md)
- [`data/message.md`](data/message.md)
- [`data/persona.md`](data/persona.md)
- [`data/model-configuration.md`](data/model-configuration.md)
- [`data/api-key.md`](data/api-key.md)
- [`data/text-action.md`](data/text-action.md)
- [`data/artifact.md`](data/artifact.md)
- [`data/media-item.md`](data/media-item.md)
- [`data/prompt-template.md`](data/prompt-template.md)
- [`data/wiki-version-snapshot.md`](data/wiki-version-snapshot.md)
- [`data/usage-record.md`](data/usage-record.md)

## Success/Failure States
- **Cleanup Complete:** Silent (background task). Next sidebar refresh shows cleaned chats removed.
- **Cleanup Blocked (DB Locked):** Retry on next cycle. Log error.
- **VACUUM Failure:** "Database compaction failed. [error]. Ensure sufficient free disk space."

## Interactions
- O1/O2 referenced by K3, K4, K5 (all interaction tiers)
- O3 triggered by user sending reply in transient thread
- O4 references L2, L7, L8, L10, D1, D3, F1 for exception checks
- O5 governs L4 (manual delete) and O4 (auto-cleanup)
- O6 triggered by A9
