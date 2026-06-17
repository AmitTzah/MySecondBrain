# TextAction — Data Entity

## Description
A Text Action is a named AI-powered text transformation — a system prompt paired with a Model Configuration. Text Actions are defined once and available everywhere: as global hotkeys (Tier 1) and as toolbar dropdown options (Studio).

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| displayName | string | Yes | Max 100 chars. Unique | Display name (e.g., "Rewrite", "Summarize") |
| systemPrompt | string | Yes | Max ~8K chars | System prompt instructing how to transform text |
| modelConfigId | string (FK) | Yes | References ModelConfiguration | Model to use for this action |
| hotkey | string | No | e.g., "Alt+Q", "Ctrl+Shift+R" | Assigned global hotkey |
| isBuiltIn | boolean | Yes | Default: false | Whether a shipped default |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| updatedAt | datetime | Yes | Auto-updated | Last modification timestamp |

## Lifecycle

### Create
- Settings → Text Actions → "New Text Action." Fill: name, system prompt, Model Configuration, optional hotkey.
- Built-in defaults (Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt) shipped with app. isBuiltIn=true. User can edit or delete them.

### Update
- Edit name, prompt, model config, or hotkey.
- Hotkey changes take effect immediately (re-registers global hook).
- Hotkey conflict detection: if assigned hotkey already in use, warn.

### Delete
- If hotkey assigned, hotkey becomes unassigned.
- No cascading effects — Text Actions are independent.

## Relationships
- **references** ModelConfiguration (via modelConfigId)
- **used by** ChatThreads/Messages (when action is triggered)

## UI Visibility
- [`screens/settings.md`](screens/settings.md) — Text Actions section
- Textbox Toolbar (K2) — Text Actions dropdown
- Global hotkeys (K3) — Triggered via assigned hotkey
