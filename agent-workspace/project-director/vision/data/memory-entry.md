# Memory Entry — Data Entity

## Description

A single AI-extracted fact about the user, stored in SQLite using Anthropic's `memory_20250818` schema. Part of the Agent Skills feature (W8). Separate from the wiki — wiki is user-authored knowledge, memory entries are AI-extracted discrete facts.

## Attributes

| Attribute | Type | Required | Constraints |
|-----------|------|----------|-------------|
| `id` | string (UUID) | Required | Unique identifier |
| `key` | string | Required | Max 200 characters. No whitespace/slashes/quotes. Fact identifier (e.g., "user_primary_language") |
| `value` | string | Required | Max 10KB. Fact content (e.g., "TypeScript") |
| `sourceChatId` | string | Optional | FK to ChatThread. The chat where this memory was extracted. Null if manually created by user in Settings. |
| `createdAt` | datetime | Required | ISO 8601. When the memory was stored. |
| `updatedAt` | datetime | Required | ISO 8601. Last modification timestamp. |

## Lifecycle

### Create

- **By AI:** Model calls `memory` tool (H11) during conversation to store a fact. App creates MemoryEntry in SQLite.
- **By User:** Manually in Settings → Memory (A13) via "Add Memory" button.

### Update

- **By AI:** Model calls `memory` tool to update an existing fact (same key = overwrite). UpdatedAt refreshed.
- **By User:** Edit key or value inline in Settings → Memory. Click entry → inline edit → Save.

### Delete

- **By AI:** Not directly available. Model can only store/retrieve, not delete. (Model could store an empty value as a workaround, but this is not encouraged.)
- **By User:** X button per entry in Settings → Memory. Or "Clear All Memories" for bulk deletion.
- Hard delete (not soft-delete). No recovery after deletion.

## Relationships

| Relationship | Entity | Type |
|-------------|--------|------|
| `sourceChatId` → ChatThread.id | ChatThread | Optional FK. The chat conversation that produced this memory. |

## UI Visibility

| Screen | How It Appears |
|--------|---------------|
| **Settings → Memory** (A13) | Scrollable list of all memories. Each entry: key, value, source chat (clickable), timestamp. Edit/Delete actions. "Clear All Memories" button. Storage size display. |
| **Chat conversation** | Invisible to the user during normal chat. Memory tool calls appear as system messages: "🧠 Stored: user_primary_language = TypeScript" or "🧠 Retrieved: 3 memories about user preferences." |
| **Per-Chat Toolbar** | "🧠 Mem" toggle controls whether the memory tool is available in the current chat. Controls global default in Settings → Memory. |

## Cross-References

- Stored by: [`features/agent-skills.md`](../features/agent-skills.md) §W8 (Memory Tool)
- Tool spec: [`features/tool-use-agents.md`](../features/tool-use-agents.md) §H11 (memory tool)
- Managed in: [`features/settings-configuration.md`](../features/settings-configuration.md) §A13 (Memory Management)
- Not related to: [`features/personal-wiki.md`](../features/personal-wiki.md) — wiki is separate user-authored knowledge base
