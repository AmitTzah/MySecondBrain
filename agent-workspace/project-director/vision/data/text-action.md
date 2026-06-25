# TextAction — Data Entity

## Description
A Text Action is a named AI-powered text transformation defined across three independent dimensions: **what to capture** (captureScope), **how to transform it** (systemPrompt + modelConfigId + chatMode), and **where to put the result** (applyMode). Text Actions are defined once and available everywhere: as global hotkeys (Tier 1) and as toolbar dropdown options (Studio).

The user has full freedom to create ANY combination of capture scope flags, apply mode, system prompt, model config, chat mode, and hotkey. Ship with sensible built-in defaults covering the most common use cases.

## Attributes

| Attribute | Type | Required | Constraints | Description |
|-----------|------|----------|-------------|-------------|
| id | string (UUID) | Yes | Unique | Primary identifier |
| displayName | string | Yes | Max 100 chars. Unique | Display name (e.g., "Rewrite", "Summarize", "Continue Writing") |
| systemPrompt | string | Yes | Max ~8K chars | System prompt instructing how to transform text |
| modelConfigId | string (FK) | Yes | References ModelConfiguration | Model to use for this action |
| chatMode | string (enum) | Yes | `Standard` (default) or `TextCompletion` | Which chat mode to use. Standard = chat API with system prompt. TextCompletion = raw prompt → raw completion, no conversation history. |
| captureScope | string (flags) | Yes | Comma-separated combination of: `selection`, `focusedElement`, `surroundingContext`, `fullDocument`, `screenshot`. Default: `selection` | WHAT to grab from the active window. Any combination valid. |
| applyMode | string (enum) | Yes | One of: `replaceSelection`, `insertAtCursor`, `replaceFocusedElement`, `appendToFocusedElement`, `prependToFocusedElement`, `clipboardOnly`, `showOnly`. Default: `replaceSelection` | WHERE to put the AI result. |
| hotkey | string | No | e.g., "Alt+Q", "Ctrl+Shift+R" | Assigned global hotkey |
| isBuiltIn | boolean | Yes | Default: false | Whether a shipped default |
| createdAt | datetime | Yes | Auto-set | Creation timestamp |
| updatedAt | datetime | Yes | Auto-updated | Last modification timestamp |

### Chat Mode

| Mode | Behavior | Use Case |
|------|----------|----------|
| `Standard` | Chat API with system prompt. User's captured content becomes the first user message. | Rewrite, Summarize, Explain, Translate, Fix Grammar, Enhance Prompt, Improve Flow |
| `TextCompletion` | Raw prompt → raw completion. No conversation history. Captured content becomes the raw prompt. | Continue Writing (raw continuation from where text left off) |

- The `chatMode` field determines which API endpoint is used and how the captured content is formatted.
- `TextCompletion` mode requires the ModelConfiguration to support text completion APIs.
- ⚠️ FLAGGED: Text completion APIs are being deprecated by some providers. The Architect should verify API availability for target providers.
- If the ModelConfiguration does not support `TextCompletion` mode, the Text Action falls back to `Standard` and a warning is shown in Settings.

### Capture Scope Flags (any combination valid)

| Flag | What It Grabs |
|------|---------------|
| `selection` | Highlighted text in the active window |
| `focusedElement` | Entire content of the focused textbox/editor via UIA ValuePattern |
| `surroundingContext` | Focused element + parent/sibling elements via UIA TreeWalker |
| `fullDocument` | All accessible text in the active window via UIA DocumentRange |
| `screenshot` | Visual capture of the active window (last resort, for non-text content). Can be combined with text scopes (e.g., `fullDocument + screenshot`). |

### Apply Modes (single choice per action)

| Mode | What Happens on Accept |
|------|------------------------|
| `replaceSelection` | Replace highlighted text in source application |
| `insertAtCursor` | Insert result at current cursor position |
| `replaceFocusedElement` | Replace entire textbox/editor content |
| `appendToFocusedElement` | Append result to end of focused textbox |
| `prependToFocusedElement` | Insert result at beginning of focused textbox |
| `clipboardOnly` | Copy result to clipboard, do not modify source |
| `showOnly` | Display in result popup only; user handles result manually |

### Built-in Defaults (shipped with app, isBuiltIn=true)

| Display Name | Chat Mode | Capture Scope | Apply Mode | Default Hotkey | Description |
|-------------|-----------|---------------|------------|----------------|-------------|
| Rewrite | Standard | `selection` | `replaceSelection` | Alt+Q | Improve clarity and flow of highlighted text |
| Summarize | Standard | `selection` | `showOnly` | Alt+W | Summarize highlighted text, show result in popup |
| Explain | Standard | `selection` | `showOnly` | Alt+E | Explain highlighted text in simple terms |
| Translate | Standard | `selection` | `replaceSelection` | Alt+R | Translate highlighted text to English |
| Fix Grammar | Standard | `selection` | `replaceSelection` | — | Correct grammar and spelling |
| Enhance Prompt | Standard | `selection` | `replaceSelection` | — | Expand a prompt draft into a well-structured prompt |
| Continue Writing | **TextCompletion** | `focusedElement` | `insertAtCursor` | Alt+C | Continue from where the text left off at cursor (raw completion) |
| Improve Flow | Standard | `focusedElement` | `replaceFocusedElement` | — | Rewrite entire textbox content for better flow |
| Summarize Page | Standard | `fullDocument` | `showOnly` | — | Summarize all accessible text in active window |
| Explain Screen | Standard | `fullDocument,screenshot` | `showOnly` | — | Explain what's on screen using text + visual capture |

## Lifecycle

### Create
- Settings → Text Actions → "New Text Action." Fill: name, system prompt, Model Configuration, chat mode (radio: Standard/TextCompletion), capture scope (checkboxes for each flag), apply mode (radio buttons), optional hotkey.
- The user can select ANY combination of capture scope flags. Multi-select checkboxes.
- The user MUST select exactly one apply mode AND exactly one chat mode. Radio button groups.
- Built-in defaults shipped with app. isBuiltIn=true. User can edit or delete them.

### Update
- Edit name, prompt, model config, chat mode, capture scope, apply mode, or hotkey.
- Hotkey changes take effect immediately (re-registers global hook).
- Changing chat mode may change which API endpoint is used.

### Delete
- If hotkey assigned, hotkey becomes unassigned.
- No cascading effects — Text Actions are independent.

## Relationships
- **references** ModelConfiguration (via modelConfigId)
- **used by** ChatThreads/Messages (when action is triggered)
- `chatMode` determines API endpoint: Standard (chat API) or TextCompletion (completion API)
- Capture scope flags dictate which UIA patterns are invoked (see [`features/windows-os-integration.md`](features/windows-os-integration.md) P9)
- Apply mode dictates which text injection method is used (see [`features/windows-os-integration.md`](features/windows-os-integration.md) P3)

## UI Visibility
- [`screens/settings.md`](screens/settings.md) — Text Actions section (create/edit with chat mode radio buttons, capture scope checkboxes + apply mode radio buttons)
- Textbox Toolbar (K2) — Text Actions dropdown
- Global hotkeys (K3) — Triggered via assigned hotkey
- [`features/text-actions-three-tier.md`](features/text-actions-three-tier.md) — Full behavioral spec for all three tiers
