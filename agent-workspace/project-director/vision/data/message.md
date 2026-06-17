# Message — Data Entity

## Description
A Message is a single entry in a ChatThread conversation. Messages can be from the user, from an AI assistant (via a specific Persona/Model), or system messages. Messages support branching — editing a message creates a new version rather than overwriting.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| threadId | string (FK) | Yes | References ChatThread | Parent chat thread |
| role | enum | Yes | User, Assistant, System | Message role |
| content | string | Yes | Markdown text. No hard length limit | Message content (Markdown) |
| rawContent | string | No | Raw text before Markdown rendering | For models that return non-Markdown |
| personaId | string (FK) | No | References Persona. Nullable for user messages | Persona used to generate (assistant msgs) |
| modelConfigId | string (FK) | No | References ModelConfiguration | Model config used to generate |
| createdAt | datetime | Yes | Auto-set | Message creation timestamp |
| tokenCount | object | No | {prompt: int, completion: int} | Token counts for this message |
| estimatedCost | number | No | In USD. Calculated from tokenCount × pricing | Estimated cost |
| generationTimeMs | number | No | Milliseconds from request to completion | Generation duration (C4) |
| feedback | string | No | Nullable. "thumbs_up", "thumbs_down", null | User feedback (D8) |

### Branching
| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| parentMessageId | string (FK) | No | References Message. Null for first message | Previous message in conversation |
| versionNumber | integer | Yes | Default: 1 | Version within branch chain |
| branchId | string | Yes | Groups versions together | All versions sharing a branchId form a branch chain |
| isActiveBranch | boolean | Yes | Default: true | Whether this version is the active one |

### Source Context (nullable — for Tier 1 apply functionality)
| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| isDirectTransformation | boolean | No | True if this message is a direct Text Action result | Enables [Apply] button (C5a) |

## Lifecycle

### Create
- **User sends message:** Content = textbox input. role = User. Created via Enter/Send button (C5).
- **AI responds:** Content = streaming tokens. role = Assistant. personaId + modelConfigId set.
- **System message:** role = System. Set at chat creation or via E5.
- **Tier 1 action:** User message = original highlighted text. Assistant message = AI transformation. isDirectTransformation=true.
- **Tier 2 query:** User message = Command Bar input. Assistant message = AI response.

### Update
- **Edit (D1):** "Edit in Place" updates content, increments versionNumber. "Edit as Branch" creates new version with new branchId; old version's isActiveBranch = false.
- **Regenerate (C5):** Creates new version of assistant message. Old version preserved as branch.
- **Continue (C8):** Appends to existing assistant message content.
- **Feedback (D8):** Toggles feedback field.

### Delete
- **Soft Delete (D2):** Removed from active conversation history. Branch data preserved. Can be restored via branch navigation or undo (D9).
- **Hard Delete:** When ChatThread deleted (L4/O4), all messages permanently deleted.

## Relationships
- **belongs to** ChatThread (via threadId)
- **references** Persona (via personaId, assistant messages only)
- **references** ModelConfiguration (via modelConfigId, assistant messages only)
- **linked to** other Messages (via parentMessageId — conversation chain; via branchId — version chain)
- **may have** MediaItems (images, audio, video rendered inline)

## UI Visibility
- [`screens/studio-chat.md`](screens/studio-chat.md) — Rendered in conversation view (C1, C2)
- Chat Navigation Bar (D6) — Listed as scrollable entries
- Chat Tree (D4) — Displayed as nodes in branch visualization
- Search Results (L3) — Snippets shown with highlighted terms
- Timeline Tab (L5) — Content preview for transient actions
