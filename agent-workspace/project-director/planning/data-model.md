# Data Model — MySecondBrain

## Overview

All 13 data entities are stored in a single SQLite database via Entity Framework Core. The model is relational with foreign keys, cascading rules, and soft-delete. Wiki files are **not** stored in SQLite — the `.md` files on disk are the source of truth; the database holds a read-optimized **index**.

---

## Entity Catalog

### 1. ApiKey
**Description:** Encrypted credential for accessing an AI provider's API. Keys are encrypted at rest via Windows DPAPI. The user brings their own keys — the app never creates provider accounts.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| displayName | string (≤100) | User-friendly label |
| provider | enum | OpenAI, Anthropic, Google, DeepSeek, MiMo, Moonshot, Mistral, OpenAICompatible |
| customProviderName | string | Required if provider=OpenAICompatible |
| customEndpointUrl | string | Required if provider=OpenAICompatible |
| keyValue | string | Encrypted via DPAPI at rest. Never displayed in full after save. |
| isValid | boolean | Set by Test Key validation |
| lastTestedAt | datetime? | Last successful validation |

**Relationships:**
- `has many` → ModelConfiguration (via apiKeyId FK)

**Consumed by feature groups:** A8, A10, B1, B2, B5, B6, B7, R1

---

### 2. Persona
**Description:** Defines AI behavior — system prompt, preferred Model Configuration, and default chat mode. Personas reference Model Configurations (the "engine"). Represents different "hats" the AI can wear.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| displayName | string (≤100, unique) | e.g., "Python Expert" |
| systemPrompt | string (≤~32K) | Supports `{{variables}}` resolved at send time |
| defaultModelConfigId | UUID (FK) | References ModelConfiguration |
| defaultChatMode | enum | Standard, TextCompletion |
| isBuiltIn | boolean | Shipped default personas |

**Relationships:**
- `belongs to` → ModelConfiguration (via defaultModelConfigId)
- `has many` → ChatThread (threads use this persona)
- `has many` → Message (messages generated with this persona)

**Consumed by feature groups:** A2, A8, B3, B4, C, D, E, K2, M

---

### 3. ModelConfiguration
**Description:** Defines the ENGINE — what model runs, on which provider, with what parameters. This is the "hardware" layer. Personas reference Model Configurations.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| displayName | string (≤100, unique) | e.g., "GPT-4o — Fast" |
| provider | string | Must match a configured ApiKey provider |
| apiKeyId | UUID (FK) | References ApiKey |
| modelIdentifier | string | e.g., "gpt-4o", "claude-sonnet-4-20250514" |
| temperature | number (0.0–2.0) | Default: 1.0 |
| maxOutputTokens | integer | Min: 1 |
| maxContextWindow | integer | Model-dependent |
| thinkingEnabled | boolean | Default: false |
| pricingInputPer1K | number? | USD per 1000 input tokens |
| pricingOutputPer1K | number? | USD per 1000 output tokens |
| contextOverflowStrategy | enum | SlidingWindow, HardStop, AutoSummarize |

**Relationships:**
- `belongs to` → ApiKey (via apiKeyId)
- `has many` → Persona (personas reference this as default)
- `has many` → Message (messages generated with this config)

**Consumed by feature groups:** B2, B3, B4, B5, B6, B7, B8, C11, E, K1, M, S

---

### 4. ChatThread
**Description:** The core container for all AI interactions regardless of origin tier. Every hotkey rewrite (Tier 1), Command Bar query (Tier 2), or Studio conversation (Tier 3) creates a ChatThread. "Everything is a Thread" architecture.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| title | string (≤200) | Auto-generated or user-edited |
| isTransient | boolean | true for Tier 1/2, false for Tier 3 |
| personaId | UUID (FK) | References Persona |
| systemMessage | string? | Per-chat system message override (E5) |
| chatMode | enum | Standard, TextCompletion |
| thinkingEnabled | boolean | Default: false |
| isMuted | boolean | Default: false |
| isFavorite | boolean | Default: false |
| isPinned | boolean | Default: false |
| isArchived | boolean | Default: false |
| colorLabel | string? | Hex or preset name |
| tags | string[] | User-defined tags |
| folderId | string? | Parent folder |
| isDeleted | boolean | Soft-delete flag (Trash) |
| deletedAt | datetime? | Set when isDeleted=true. 30-day auto-purge. |

**Source Context (Tier 1 elevation only, nullable):**

| Attribute | Type | Notes |
|-----------|------|-------|
| sourceHWND | integer? | Captured window handle |
| sourceAppName | string? | e.g., "Word", "VS Code" |
| sourceDocTitle | string? | e.g., "chapter3.docx" |
| originalHighlightedText | string? | Text that was originally highlighted |

**Relationships:**
- `belongs to` → Persona (via personaId)
- `has many` → Message (one thread contains many messages)
- `has many` → Artifact (generated during conversation)
- `has many` → MediaItem (uploaded/generated during conversation)
- `has many` → UsageRecord (denormalized via threadId)
- `references` → ModelConfiguration (indirectly via Persona)
- `optionally references` → source application (HWND context)

**Consumed by feature groups:** C, D, E, H, I, K, L, M, O, U

---

### 5. Message
**Description:** Single entry in a ChatThread conversation. Supports branching — editing creates a new version rather than overwriting.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| threadId | UUID (FK) | References ChatThread |
| role | enum | User, Assistant, System |
| content | string | Markdown text. No hard length limit. |
| rawContent | string? | Raw text before Markdown rendering |
| personaId | UUID? (FK) | References Persona (assistant msgs only) |
| modelConfigId | UUID? (FK) | References ModelConfiguration |
| tokenCount | object? | {prompt: int, completion: int} |
| estimatedCost | number? | USD, calculated from tokenCount × pricing |
| generationTimeMs | number? | Milliseconds request-to-completion |
| feedback | string? | thumbs_up, thumbs_down, null |

**Branching Attributes:**

| Attribute | Type | Notes |
|-----------|------|-------|
| parentMessageId | UUID? (FK→self) | Previous message in conversation chain |
| versionNumber | integer | Default: 1. Increments on edit. |
| branchId | UUID | Groups versions together |
| isActiveBranch | boolean | Whether this version is the active one |
| isDirectTransformation | boolean? | Enables [Apply] button (Tier 1) |

**Relationships:**
- `belongs to` → ChatThread (via threadId)
- `references` → Persona (via personaId, assistant msgs only)
- `references` → ModelConfiguration (via modelConfigId, assistant msgs only)
- `linked to` self → Message (via parentMessageId — conversation chain; via branchId — version chain)
- `may have` → MediaItem (images, audio, video rendered inline)
- `has one` → UsageRecord (one usage record per assistant message)

**Consumed by feature groups:** C, D, E, H, I, K, L, M, S

---

### 6. Artifact
**Description:** AI-generated, versioned, text-based file (code, document, config) created during a conversation. Editable and saveable to disk or wiki.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| name | string (≤255, filename-safe) | e.g., "app.py" |
| type | string | Inferred from extension or declared |
| threadId | UUID (FK) | References ChatThread |
| versionCount | integer | Default: 1 |

**Versions (embedded/sub-entity):**

| Attribute | Type | Notes |
|-----------|------|-------|
| versionNumber | integer | Sequential: 1, 2, 3... |
| content | string | Full file content |
| isActive | boolean | True for latest version |

**Relationships:**
- `belongs to` → ChatThread (via threadId)
- `can be saved to` → WikiFile (via N5 pipeline)
- `can be exported` → disk file

**Consumed by feature groups:** F, N5, O5

---

### 7. MediaItem
**Description:** Binary media file (image, audio, video) — uploaded by user, captured via webcam, or AI-generated. Stored on disk; tracked in database.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| filename | string | Original or generated |
| filePath | string | Absolute path on disk |
| mediaType | enum | Image, Audio, Video |
| mimeType | string | e.g., "image/png" |
| fileSize | integer | Bytes |
| source | enum | UserUpload, AIGenerated, WebcamCapture, Screenshot |
| threadId | UUID (FK) | References ChatThread |
| messageId | UUID? (FK) | References Message (containing message) |
| generatedPrompt | string? | Prompt used (AI-generated only) |
| isSavedToDisk | boolean | Default: false |
| isSavedToWiki | boolean | Default: false |

**Relationships:**
- `belongs to` → ChatThread (via threadId)
- `optionally belongs to` → Message (via messageId)
- `may be referenced by` → WikiFile (if saved to wiki)

**Consumed by feature groups:** C9, C9a, C21, C22, G, O5

---

### 8. PromptTemplate
**Description:** Saved, reusable prompt with dynamic variable placeholders (`{{clipboard}}`, `{{date}}`, etc.). Organized with tags and folders.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| name | string (≤200) | Display name |
| text | string (≤~16K) | Contains `{{variables}}` |
| tags | string[] | Organization tags |
| folderId | string? | Parent prompt folder |

**Supported Variables:** `{{clipboard}}`, `{{selected_text}}`, `{{date}}`, `{{current_wiki_file}}`

**Relationships:**
- Independent entity. Not linked to ChatThreads or Messages. Used transiently when inserted into textbox.

**Consumed by feature groups:** J

---

### 9. TextAction
**Description:** A Text Action is a named AI-powered text transformation defined across three independent dimensions: **what to capture** (captureScope — flags like `selection`, `focusedElement`, `fullDocument`, `screenshot`), **how to transform it** (systemPrompt + modelConfigId), and **where to put the result** (applyMode — `replaceSelection`, `insertAtCursor`, `replaceFocusedElement`, `appendToFocusedElement`, `prependToFocusedElement`, `clipboardOnly`, `showOnly`). Defined once and available everywhere: as global hotkeys (Tier 1) and toolbar dropdown options (Studio).

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| displayName | string (≤100, unique) | e.g., "Rewrite", "Summarize", "Continue Writing" |
| systemPrompt | string (≤~8K) | Instructs how to transform text |
| modelConfigId | UUID? (FK) | References ModelConfiguration. Nullable until ModelConfiguration seed data exists. |
| captureScope | string (flags) | Comma-separated combination: `selection`, `focusedElement`, `surroundingContext`, `fullDocument`, `screenshot`. Default: `selection`. |
| applyMode | string (enum) | One of: `replaceSelection`, `insertAtCursor`, `replaceFocusedElement`, `appendToFocusedElement`, `prependToFocusedElement`, `clipboardOnly`, `showOnly`. Default: `replaceSelection`. |
| hotkey | string? | e.g., "Alt+Q", "Alt+C" |
| isBuiltIn | boolean | Shipped defaults (10 built-in actions) |
| createdAt | datetime | Auto-set on creation |
| updatedAt | datetime | Auto-updated on modification |

**Built-in Defaults (10, shipped with isBuiltIn=true):**

| Display Name | Capture Scope | Apply Mode | Default Hotkey |
|-------------|---------------|------------|----------------|
| Rewrite | `selection` | `replaceSelection` | Alt+Q |
| Summarize | `selection` | `showOnly` | Alt+W |
| Explain | `selection` | `showOnly` | Alt+E |
| Translate | `selection` | `replaceSelection` | Alt+R |
| Fix Grammar | `selection` | `replaceSelection` | — |
| Enhance Prompt | `selection` | `replaceSelection` | — |
| Continue Writing | `focusedElement` | `insertAtCursor` | Alt+C |
| Improve Flow | `focusedElement` | `replaceFocusedElement` | — |
| Summarize Page | `fullDocument` | `showOnly` | — |
| Explain Screen | `fullDocument,screenshot` | `showOnly` | — |

**Capture Scope Flags (any combination valid):**

| Flag | What It Grabs | UIA Pattern |
|------|---------------|-------------|
| `selection` | Highlighted text in the active window | TextPattern or clipboard fallback |
| `focusedElement` | Entire content of the focused textbox/editor | ValuePattern |
| `surroundingContext` | Focused element + parent/sibling elements | TreeWalker |
| `fullDocument` | All accessible text in the active window | DocumentRange or full tree traversal |
| `screenshot` | Visual capture of the active window (last resort) | Win32 PrintWindow/BitBlt |

**Apply Modes (single choice per action):**

| Mode | What Happens on Accept | Injection Method |
|------|------------------------|-----------------|
| `replaceSelection` | Replace highlighted text in source application | HWND injection → clipboard + Ctrl+V fallback |
| `insertAtCursor` | Insert result at current cursor position | UIA TextPattern → clipboard fallback |
| `replaceFocusedElement` | Replace entire textbox/editor content | UIA ValuePattern → clipboard + Ctrl+A, Ctrl+V fallback |
| `appendToFocusedElement` | Append result to end of focused textbox | UIA ValuePattern append → clipboard fallback |
| `prependToFocusedElement` | Insert result at beginning of focused textbox | UIA ValuePattern prepend → clipboard fallback |
| `clipboardOnly` | Copy result to clipboard, do not modify source | Clipboard write |
| `showOnly` | Display in result popup only; user handles result manually | None (no injection) |

**Relationships:**
- `references` → ModelConfiguration (via modelConfigId, nullable)
- `used by` → ChatThread/Messages (when action is triggered, transient)
- Capture scope flags dictate which UIA patterns are invoked (see [`../vision/features/windows-os-integration.md`](../vision/features/windows-os-integration.md) P9)
- Apply mode dictates which text injection method is used (see [`integration-points.md`](integration-points.md) #16)

**Consumed by feature groups:** K1, K2, K3, P9

---

### 10. UsageRecord
**Description:** Token consumption and estimated cost for a single AI API call. Records are aggregated for the Usage & Pricing Dashboard.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| messageId | UUID (FK) | References Message |
| threadId | UUID (FK, denormalized) | References ChatThread |
| personaId | UUID? (FK) | References Persona |
| modelConfigId | UUID (FK) | References ModelConfiguration |
| provider | string | For provider-level aggregation |
| modelIdentifier | string | For model-level aggregation |
| promptTokens | integer | ≥ 0 |
| completionTokens | integer | ≥ 0 |
| totalTokens | integer | promptTokens + completionTokens |
| estimatedCost | number? | USD, from pricing config × tokens |

**Relationships:**
- `belongs to` → Message (via messageId)
- `belongs to` → ChatThread (via threadId, denormalized)
- `references` → Persona (via personaId)
- `references` → ModelConfiguration (via modelConfigId)

**Consumed by feature groups:** C11, S

---

### 11. WikiFile (Index)
**Description:** Represents a `.md` file in the user's wiki directory. The `.md` files on disk are the source of truth. This entity describes the INDEXED representation — metadata and content cached in SQLite for fast search and cross-referencing.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| filePath | string (PK) | Relative to wiki root directory |
| fileName | string | e.g., "git-cheatsheet.md" |
| h1Title | string? | First H1 heading |
| headings | object[] | [{level, text, anchor}] — all headings |
| content | string | Full file content (for FTS5) |
| wordCount | integer? | Approximate word count |
| lastModifiedAt | datetime? | From file system |
| crossLinksOut | string[] | Files this file links TO |
| crossLinksIn | string[] | Files linking TO this file (computed) |

**Relationships:**
- `has many` → WikiVersionSnapshot (version history)
- `linked to` → other WikiFiles (via crossLinksOut/In)
- `referenced by` → Messages (via @ mentions, N7)
- `created from` → ChatThreads (via Write to Wiki N5) — workflow relationship, not data

**Consumed by feature groups:** N

---

### 12. WikiVersionSnapshot
**Description:** Backup of a wiki `.md` file's previous state, saved automatically before modification via Write to Wiki pipeline. Enables instant undo of wiki changes.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| wikiFilePath | string (FK) | References WikiFile |
| content | string | Full file content at snapshot time |
| source | enum | WriteToWiki, ManualEdit, Restore |

**Retention:** Max 30 snapshots per file. 50MB total cap across all snapshots. Oldest auto-deleted when exceeded.

**Relationships:**
- `belongs to` → WikiFile (via wikiFilePath)

**Consumed by feature groups:** N6, N13

---

### 13. BackupSnapshot
**Description:** Represents a backup of all app data (SQLite DB, wiki files, artifacts) stored in Google Cloud Storage. Metadata tracked locally; actual data lives in GCS.

| Key Attribute | Type | Notes |
|---------------|------|-------|
| id | UUID (PK) | Primary identifier |
| sizeBytes | integer? | Total backup size |
| type | enum | Scheduled, Manual |
| status | enum | Complete, Failed, InProgress |
| errorMessage | string? | Only if status=Failed |
| gcsObjectPath | string | Path within GCS bucket |

**Relationships:**
- Standalone entity. Not linked to other data entities.

**Consumed by feature groups:** R

---

## Entity-Relationship Summary

```
┌──────────────┐       ┌──────────────────────┐
│   ApiKey     │ 1───* │  ModelConfiguration  │
│              │       │                      │
└──────────────┘       └────────┬─────────────┘
                                │
                         1      │
                      ┌─────────┘
                      │
                      ▼
              ┌──────────────┐       ┌──────────────────────┐
              │   Persona    │ 1───* │     ChatThread       │
              │              │       │                      │
              └──────────────┘       └────┬────────┬────────┘
                                          │        │
                                    1     │   1    │
                              ┌───────────┘        └────────────┐
                              ▼                                 ▼
                      ┌──────────────┐                 ┌──────────────┐
                      │   Message    │ *───────────────│   Artifact   │
                      │  (self-ref:  │   1             │              │
                      │  parentMsg,  │                 └──────┬───────┘
                      │  branchId)   │                        │ can be saved
                      └──┬───────┬───┘                        ▼
                         │       │                    ┌──────────────┐
                   1     │  1    │                    │  WikiFile    │
              ┌──────────┘       └───────────┐        │  (index)     │
              ▼                              ▼        └──┬───────┬───┘
      ┌──────────────┐              ┌──────────────┐      │       │
      │ UsageRecord  │              │  MediaItem   │      │ 1  *  │
      │              │              │              │      ▼       ▼
      └──────────────┘              └──────┬───────┘  ┌──────────────┐
                                          │           │WikiVersion   │
                                          │ can be    │Snapshot      │
                                          │ saved     └──────────────┘
                                          ▼
                                   ┌──────────────┐
                                   │  WikiFile    │
                                   │  (see above) │
                                   └──────────────┘

INDEPENDENT ENTITIES (no FK relationships):
  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
  │ PromptTemplate   │    │   TextAction     │    │ BackupSnapshot   │
  │                  │    │   refs ModelCfg  │    │                  │
  └──────────────────┘    └──────────────────┘    └──────────────────┘
```

### Relationship Summary Table

| Entity | FK Dependencies | Referenced By |
|--------|----------------|---------------|
| ApiKey | — | ModelConfiguration |
| Persona | → ModelConfiguration | ChatThread, Message |
| ModelConfiguration | → ApiKey | Persona, Message, TextAction, UsageRecord |
| ChatThread | → Persona | Message, Artifact, MediaItem, UsageRecord |
| Message | → ChatThread, → Persona?, → ModelConfiguration?, → self | UsageRecord, MediaItem |
| Artifact | → ChatThread | (can be saved to WikiFile) |
| MediaItem | → ChatThread, → Message? | — |
| PromptTemplate | — | (transient use only) |
| TextAction | → ModelConfiguration | (transient use only) |
| UsageRecord | → Message, → ChatThread, → Persona?, → ModelConfiguration | — |
| WikiFile | — | WikiVersionSnapshot |
| WikiVersionSnapshot | → WikiFile | — |
| BackupSnapshot | — | — |

---

## Special Data Modeling Notes

### Message Branching (Version-Chain Model)

The message branching model uses four columns to form a dual-graph structure:

1. **Conversation Chain:** `parentMessageId` links messages in temporal order (User → Assistant → User → Assistant...). This is the linear conversation flow.

2. **Version Chain:** `branchId` groups all versions of the "same" message. When a message is edited:
   - **Edit in Place (D1):** New version with same `branchId`, `versionNumber` incremented, old version's `isActiveBranch` = false.
   - **Edit as Branch (D1):** New version with new `branchId`, old version's `isActiveBranch` = false.

3. **Active Branch Resolution:** The active conversation path follows `parentMessageId` chain where `isActiveBranch` = true. SQL pattern using recursive CTE:

```sql
WITH RECURSIVE active_chain AS (
    SELECT * FROM Messages WHERE threadId = @threadId AND parentMessageId IS NULL AND isActiveBranch = true
    UNION ALL
    SELECT m.* FROM Messages m
    JOIN active_chain a ON m.parentMessageId = a.id
    WHERE m.isActiveBranch = true
)
SELECT * FROM active_chain ORDER BY createdAt;
```

4. **Branch Navigation (D3):** Change `isActiveBranch` flags along the chain. Re-render messages from that point forward.

5. **Chat Tree (D4):** Query all messages in thread, build tree from `parentMessageId` edges grouped by `branchId`. Visualize via custom WPF graph layout.

6. **Fork Chat (D7):** Copy messages up to fork point into new ChatThread. New thread references same Persona. Original thread unchanged.

### Transient vs. Permanent Chat Threads

| Aspect | Transient (Tier 1/2) | Permanent (Tier 3/Elevated) |
|--------|----------------------|---------------------------|
| `isTransient` | true | false |
| Created by | Hotkey (K3) or Command Bar (K4) | New Chat (K5), Import (I2), Elevation (O3) |
| Visibility | Timeline tab (L5) only | Sidebar chat list (L1) |
| Lifecycle | 7-day auto-cleanup (O4) | Indefinite (until soft-delete) |
| Elevation | Sending reply in Studio flips to false (O3) | N/A |
| Exceptions | Auto-elevated if: favorited, tagged, pinned, archived, has user replies or artifacts | N/A |
| Deletion | Direct hard-delete (no soft-delete for transient) | Soft-delete → Trash → 30-day purge |

### Soft-Delete (ChatThread)

Soft-delete uses two columns on ChatThread:
- `isDeleted` (boolean): true when chat moved to Trash
- `deletedAt` (datetime?): timestamp of deletion

**Workflow:**
1. User deletes chat (L4) → `isDeleted=true`, `deletedAt=now`. Chat hidden from all lists except Trash view.
2. Restore (U4) → clears both fields. Chat returns to original location (folder, tags, pinned preserved).
3. 30-day auto-purge (U3): Background task deletes threads where `isDeleted=true` AND `deletedAt > 30 days ago`.
4. Permanent delete from Trash (U5): immediate hard delete with O5 garbage collection cascade.
5. Empty Trash (U6): bulk permanent delete of all soft-deleted threads.

**Note:** Transient threads (`isTransient=true`) do NOT use soft-delete. They go directly to hard delete on cleanup (O4).

### Wiki Index Tables (Separate from Wiki File Storage)

Wiki `.md` files live on disk as the source of truth. The database holds two index tables:

1. **WikiFile index table:** Cached metadata (headings, cross-links, word count) + full content for FTS5 search. Updated by WikiIndexer on file change (debounced FileSystemWatcher).

2. **WikiVersionSnapshot table:** Pre-modification backups. Retention: max 30 per file, 50MB total cap. Oldest auto-pruned.

**Index invalidation:** If the watcher detects external deletion, the index row and its FTS5 entry are removed. If the watcher misses events (network drive, app suspended), a full re-index is triggered on next wiki interaction.

### Usage Records Aggregation

UsageRecord is append-only (immutable after creation). Aggregation queries power the Usage Dashboard:

- **By time range:** `WHERE createdAt BETWEEN @from AND @to`
- **By provider:** `GROUP BY provider`
- **By model:** `GROUP BY modelIdentifier`
- **By chat:** `GROUP BY threadId`
- **By persona:** `GROUP BY personaId`
- **Cost estimation:** `SUM(estimatedCost)` where pricing is configured; NULL if no pricing set

The `threadId` is denormalized onto UsageRecord to avoid JOIN through Message for dashboard queries. `personaId` is also denormalized for persona-level aggregation.

---

## Cascading Delete Rules

| Parent Deleted | Cascading Effect |
|---------------|-----------------|
| ChatThread (hard delete) | All Messages deleted. All Artifacts deleted UNLESS saved to disk/wiki. All MediaItems deleted UNLESS `isSavedToDisk=true` or `isSavedToWiki=true`. All UsageRecords deleted. |
| ChatThread (soft delete) | No cascading. Messages/Artifacts/Media preserved for restore. |
| Persona | ChatThreads retain persona name but FK set to null (or restrict — architect decision flagged). Messages retain original personaId. |
| ModelConfiguration | Personas referencing it must select new config (restrict). Messages retain original modelConfigId. |
| ApiKey | ModelConfigurations referencing it break (restrict with warning). |
| WikiFile (disk deletion) | Index entry removed. WikiVersionSnapshots for that file deleted. |
| Message (soft delete, D2) | Removed from active conversation history. Branch data preserved. |

---

## Architect Decision Flags

The following decisions from the entity specs require architect resolution:

1. **Persona FK on delete:** Nullify ChatThread.personaId or restrict deletion? Spec says "threads retain Persona name but lose the FK reference (or FK set to null)."

2. **ModelConfiguration FK on delete:** Nullify or cascade restrict? Spec says "show warning: this config is used by [N] Personas."

3. **MediaItem soft-delete:** Delete from Media Library (G3) — soft-delete or hard-delete? Spec says "(Architect decision)."

4. **MessageDrafts table:** Referenced in architecture and abstractions but not defined as a vision entity. Needs a lightweight schema: `threadId (PK)`, `content (text)`, `cursorPosition (int)`, `savedAt (datetime)`.

---

*Data model document — Batch 2 of planning/ directory. See also: [`architecture.md`](architecture.md), [`abstractions.md`](abstractions.md).*
