# Artifact — Data Entity

## Description
An Artifact is an AI-generated, versioned, text-based file (code, document, config) created during a conversation. Artifacts are editable and can be saved to disk or to the wiki.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| name | string | Yes | Max 255 chars. Filename-safe | Artifact name (e.g., "app.py") |
| type | string | Yes | Inferred from extension or declared | File type (e.g., "python", "markdown", "json") |
| threadId | string (FK) | Yes | References ChatThread | Parent chat thread |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| updatedAt | datetime | Yes | Auto-updated | Last modification timestamp |
| versionCount | integer | Yes | Default: 1 | Number of versions |

### Versions (sub-entity or embedded)
| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| versionNumber | integer | Yes | Sequential: 1, 2, 3... | Version number |
| content | string | Yes | Full file content | Version content |
| createdAt | datetime | Yes | Auto-set | Version creation timestamp |
| isActive | boolean | Yes | Default: true for latest | Whether this is the active version |

## Lifecycle

### Create
- AI generates artifact during conversation (F1). Version 1 created with content.
- Appears in side panel (F2).

### Update
- User requests changes → AI produces new version. versionCount increments.
- Version switching (F5): user selects which version is active. New changes branch from active version.

### Delete
- Manual: delete from Global Artifacts Browser (F7). Permanently removed.
- Cascading: when parent ChatThread deleted AND artifact not saved to disk/wiki (O5).

## Relationships
- **belongs to** ChatThread (via threadId)
- **can be saved to** Wiki File (via N5 pipeline)
- **can be exported** to disk file

## UI Visibility
- Side Panel (F2) — Listed in current chat's artifact list
- Artifact Viewer (F6) — Content with syntax highlighting
- Diff View (F4) — Version comparison
- Global Artifacts Browser (F7) — All artifacts across all chats
- Wiki Browser (N4) — If saved to wiki
