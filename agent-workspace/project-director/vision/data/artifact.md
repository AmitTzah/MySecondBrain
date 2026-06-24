# Artifact — Data Entity

## Description

A file-based deliverable created by the AI and presented to the user via the `present_files` tool. Artifacts are files that the model creates in the workspace using `text_editor` or `bash`, then signals as "done" by calling `present_files`. The app copies presented files from the workspace to the artifacts directory and surfaces them in the WebView2-powered side panel. The model has no awareness of "artifacts" — it just writes files and presents them. The artifact concept is entirely client-side.

Each artifact is identified by its filename within a chat. Same filename within the same chat = new version (auto-tracked by the app). Different filename = new artifact. This matches Claude.ai's artifact identity model exactly.

## Attributes

| Attribute | Type | Required | Constraints |
|-----------|------|----------|-------------|
| `id` | string (UUID) | Required | Unique identifier |
| `chatThreadId` | string | Required | FK to ChatThread. The chat that produced this artifact. |
| `filename` | string | Required | Original filename (e.g., "budget.xlsx", "app.py", "todo-draft.md"). Determines artifact identity within a chat — same filename = new version. |
| `type` | enum | Required | Inferred from file extension: `code`, `markdown`, `spreadsheet`, `document`, `html`, `svg`, `pdf`, `config`, `other`. Determines rendering mode in WebView2 viewer. |
| `currentVersion` | int | Required | Current active version number. Starts at 1. Increments each time the same filename is presented again. |
| `createdAt` | datetime | Required | ISO 8601. When the artifact was first presented. |
| `updatedAt` | datetime | Required | ISO 8601. Last time a new version was created. |

## Version Data (Separate Table)

| Attribute | Type | Required | Constraints |
|-----------|------|----------|-------------|
| `id` | string (UUID) | Required | Unique identifier |
| `artifactId` | string | Required | FK to Artifact |
| `versionNumber` | int | Required | Sequential: 1, 2, 3... |
| `filePath` | string | Required | Path to the stored file content in the artifacts directory |
| `sizeBytes` | long | Required | File size |
| `createdAt` | datetime | Required | When this version was created |
| `source` | enum | Required | `text_editor_create`, `text_editor_str_replace`, `text_editor_insert`, `bash`, `present_files` — which tool created this version |

## Lifecycle

### Create (via present_files)

1. Model writes file in workspace using `text_editor` or `bash`
2. Model calls `present_files(["filename.ext"])`
3. App checks if an artifact with this filename already exists in this chat:
   - **New filename:** Creates new Artifact record (v1). Copies file from workspace to artifacts directory.
   - **Existing filename:** Increments `currentVersion`. Creates new version record. Copies file as new version.
4. Artifact appears in WebView2 side panel

### Update (via text_editor or bash + present_files)

- When model modifies an already-presented file and calls `present_files` again: new version created
- When model uses `text_editor.str_replace` on a file that was previously presented: app detects the change and can auto-version (even without an explicit second `present_files` call)
- Version history is entirely app-side — the model just writes files

### Delete

- **Soft delete with chat:** When chat is soft-deleted (U1), artifacts remain but are not visible until chat is restored
- **Permanent delete:** When chat is permanently deleted (U5, O5), exclusively-linked artifacts are deleted. Artifacts saved to disk or wiki are preserved.
- **User-initiated delete from side panel:** Not supported in initial release. Artifacts live with their parent chat.

## Relationships

| Relationship | Entity | Type |
|-------------|--------|------|
| `chatThreadId` → ChatThread.id | ChatThread | Required FK. Artifact belongs to one chat. |
| Version records → Artifact.id | Artifact | One-to-many. Each artifact has 1+ versions. |

## UI Visibility

| Screen | How It Appears |
|--------|---------------|
| **Studio Chat — Side Panel** (F2) | Listed by filename + type icon. Click to view in WebView2 viewer. Version dropdown. "Save to Disk" / "Save to Wiki" buttons. |
| **Global Artifacts Browser** (F7) | Table: filename, type, parent chat (clickable), created date, version count. Search, sort, filter. |
| **Chat conversation** | `present_files` tool call shown as system message: "📄 Presented: [filename]" |

## Cross-References

- Created by: [`features/artifacts-side-panel.md`](../features/artifacts-side-panel.md) §F1 (workspace-to-artifact pipeline)
- Triggered by: [`features/tool-use-agents.md`](../features/tool-use-agents.md) §H9 (present_files tool)
- Rendered in: WebView2 side panel (F2, F6)
- Version history: F3 (auto-tracked by filename within chat)
- Garbage collected with parent chat: O5
- Saved to wiki via: N5 (Write to Wiki pipeline)
