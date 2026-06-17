# PromptTemplate — Data Entity

## Description
A PromptTemplate is a saved, reusable prompt with dynamic variable placeholders. Prompts are organized with tags and folders. When inserted, variables like {{clipboard}} and {{date}} are resolved to their current values.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| name | string | Yes | Max 200 chars | Display name |
| text | string | Yes | Max ~16K chars. Contains {{variables}} | Prompt text with variable placeholders |
| tags | string[] | No | User-defined tags | Organization tags (e.g., "coding", "writing") |
| folderId | string | No | Nullable. References prompt folder | Parent folder |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| updatedAt | datetime | Yes | Auto-updated | Last modification timestamp |

## Supported Variables
| Variable | Resolution |
|----------|-----------|
| {{clipboard}} | Current clipboard text content |
| {{selected_text}} | Currently highlighted text in active application |
| {{date}} | Current date (YYYY-MM-DD) |
| {{current_wiki_file}} | Filename of currently open wiki file in Wiki Browser |

## Lifecycle

### Create
- Type prompt in textbox → "Save as Prompt" → name it → assign tags → save.

### Update
- Edit prompt text, name, or tags from Prompt Library panel.

### Delete
- Delete with confirmation: "Delete prompt '[name]'?"

## Relationships
- Independent entity. Not linked to ChatThreads or Messages.
- Used transiently when inserted into textbox.

## UI Visibility
- Prompt Library panel (J1) — Searchable list of saved prompts
- Textbox Toolbar (K2) — Prompt Library button opens the panel
