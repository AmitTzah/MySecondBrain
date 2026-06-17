# Persona — Data Entity

## Description
A Persona defines the AI's BEHAVIOR — its system prompt, preferred Model Configuration, and default chat mode. Personas represent different "hats" the AI can wear (Python Expert, Writing Coach, General Assistant). Personas reference Model Configurations (the engine).

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| displayName | string | Yes | Max 100 chars. Unique | Display name (e.g., "Python Expert") |
| systemPrompt | string | Yes | Max ~32K chars. Supports {{variables}} | System prompt defining AI behavior |
| defaultModelConfigId | string (FK) | Yes | References ModelConfiguration | Default model engine |
| defaultChatMode | enum | Yes | Standard, TextCompletion | Default chat mode for new chats using this Persona |
| isBuiltIn | boolean | Yes | Default: false | Whether this is a shipped default Persona |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| updatedAt | datetime | Yes | Auto-updated | Last modification timestamp |

## Lifecycle

### Create
- **User-created:** Settings → Personas → "New Persona." Fill form: name, system prompt, default Model Config, default chat mode.
- **Built-in defaults:** Shipped with app (e.g., "General Assistant", "Code Helper"). isBuiltIn=true.
- **System prompt variables:** `{{date}}`, `{{time}}`, `{{user_name}}` resolved at message send time.

### Update
- **Edit:** Settings → Personas → select → edit. Changes affect all NEW messages in chats using this Persona. Existing messages retain original Persona name.
- **Delete:** If Persona is the default (A2), prompt user to select new default. If Persona referenced by ChatThreads, those threads retain Persona name but lose the FK reference (or FK set to null — Architect decision).

### Delete
- **User-initiated:** Settings → Personas → Delete. Confirmation if referenced by chats.
- **Built-in defaults:** Can be deleted by user.

## Relationships
- **belongs to** ModelConfiguration (via defaultModelConfigId)
- **has many** ChatThreads (threads use this Persona)
- **has many** Messages (messages generated with this Persona)

## UI Visibility
- [`screens/settings.md`](screens/settings.md) — Personas section. Create, edit, delete.
- Persona Selector (B4) — Dropdown in textbox toolbar and new chat creation.
- Chat Header — Active Persona name displayed.
- Model Comparison (M2) — Personas selected for comparison.
