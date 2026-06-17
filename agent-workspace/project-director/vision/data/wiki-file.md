# WikiFile — Data Entity (Conceptual)

## Description
A WikiFile represents a .md file in the user's wiki directory. Unlike other entities, WikiFiles are NOT stored in the SQLite database — the .md files on disk are the source of truth. However, the app maintains an INDEX of wiki file metadata and content in SQLite for fast search and cross-referencing. This entity describes the INDEXED representation, not the files themselves.

## Attributes (Indexed)

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| filePath | string | Yes | Relative to wiki root directory | Path to .md file |
| fileName | string | Yes | e.g., "git-cheatsheet.md" | File name |
| h1Title | string | No | First H1 heading in file | Primary title |
| headings | object[] | No | [{level: 1-6, text: string, anchor: string}] | All headings with levels |
| content | string | Yes | Full file content (for full-text search) | Indexed content |
| wordCount | integer | No | Approximate word count | File size indicator |
| createdAt | datetime | No | From file system | File creation date |
| lastModifiedAt | datetime | No | From file system | Last modified date |
| crossLinksOut | string[] | No | Files this file links TO | Extracted from Markdown links |
| crossLinksIn | string[] | No | Files linking TO this file | Calculated from other files' links |

## Lifecycle

### Create
- User creates .md file in wiki directory (externally or via Write to Wiki N5).
- File system watcher detects new file → indexer adds to SQLite index.

### Update
- File modified externally (VS Code, etc.) → watcher detects → indexer updates index.
- File modified via Write to Wiki (N5) → file written to disk → indexer updates.

### Delete
- User deletes file externally → watcher detects → index entry removed.
- User deletes via Wiki Browser (N4) → file deleted from disk → index entry removed.
- AI CANNOT delete wiki files (N8 restriction).

## Relationships
- **has many** WikiVersionSnapshots (version history for this file, N6)
- **linked to** other WikiFiles (via crossLinksOut and crossLinksIn)
- **referenced by** Messages (via @ mentions, N7)
- **created from** ChatThreads (via Write to Wiki N5) — but this is a workflow relationship, not a data relationship

## UI Visibility
- Wiki Browser (N4) — File tree + Markdown viewer
- Wiki Search (N3) — Search results
- @ Mentions dropdown (N7) — Quick file selection
- index.md (N11) — Auto-generated directory
