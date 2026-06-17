# WikiVersionSnapshot — Data Entity

## Description
A WikiVersionSnapshot is a backup of a wiki .md file's previous state, saved automatically before any modification via the Write to Wiki pipeline (N5). Snapshots enable instant undo of wiki changes.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| wikiFilePath | string | Yes | Relative to wiki root | Path to the wiki .md file |
| content | string | Yes | Full file content at snapshot time | Snapshot content |
| createdAt | datetime | Yes | Auto-set | When snapshot was taken |
| source | enum | Yes | WriteToWiki, ManualEdit, Restore | What triggered the snapshot |

## Retention Rules
- **Per-File Limit:** Maximum 30 snapshots per wiki file. Oldest auto-deleted when exceeded.
- **Total Storage Cap:** 50MB total across all snapshots. Oldest deleted across all files when exceeded.
- **Restore:** Restoring creates a new snapshot of current state first (so restore is undoable).

## Lifecycle

### Create
- Auto-created before Write to Wiki (N5) modifies a file.
- Auto-created before a restore operation (so restore can be undone).

### Delete
- Auto-deleted when per-file limit exceeded (oldest first).
- Auto-deleted when total storage cap exceeded (oldest across all files first).
- Not user-deletable individually (user deletes all snapshots for a file via Version History).

### Restore
- Wiki Browser → right-click file → "Version History" → select snapshot → "Restore."
- Current file state is snapshotted before restore executes.

## Relationships
- **belongs to** WikiFile (via wikiFilePath)

## UI Visibility
- Wiki Browser (N4) — Right-click file → "Version History"
- Version History panel — List of snapshots with dates and sources. Preview any snapshot. Restore button.
