# Prompt Library — Feature Spec

## What the User Accomplishes
The user saves, organizes, and reuses prompts with dynamic variable resolution. Saved prompts can be inserted into any chat with variables auto-resolved.

## Trigger
- Prompt Library button in textbox toolbar (K2)
- "Save as Prompt" from textbox context menu

## Detailed Behavior

### J1. Prompt Library
- **Access:** Button in textbox toolbar (K2). Opens popover or side panel.
- **Save:** "Save Current as Prompt" → name the prompt → optional tags → save
- **Dynamic Variables:** Prompts support variables resolved at insertion time:
  - `{{clipboard}}` — current clipboard text content
  - `{{selected_text}}` — currently highlighted text in active application
  - `{{date}}` — current date (format: YYYY-MM-DD)
  - `{{current_wiki_file}}` — filename of currently open wiki file in Wiki Browser (N4)
- **Insert:** Click saved prompt → variables resolved → text inserted into textbox at cursor position
- **Search:** Search bar filters prompts by name or tag
- **Organization:** Tags for categorization (e.g., "coding", "writing", "analysis")
- **Display:** List of saved prompts showing name and first 80 characters of prompt text

### J2. Prompt Management
- **Edit:** Click prompt → opens in editable view → modify prompt text, name, or tags
- **Delete:** Delete button with confirmation: "Delete prompt '[name]'?"
- **Organize:** Create folders/categories. Drag prompts into folders.
- **Default Prompt Library:** No built-in prompts — library starts empty. User builds their own collection.
- **Empty State:** "No saved prompts yet. Type a prompt in the textbox and click 'Save as Prompt' to add it to your library."

## Data
- [`data/prompt-template.md`](data/prompt-template.md) — prompt name, text, tags, folder, created/updated dates

## Success/Failure States
- **Save Success:** Prompt appears in library. Toast: "Prompt saved: [name]"
- **Insert with Unresolved Variable:** If {{selected_text}} used but nothing selected, variable replaced with empty string. No error.
- **Insert with Clipboard Empty:** {{clipboard}} replaced with empty string.

## Permissions
- Single-user app.

## Interactions
- J1 accessed from K2 (textbox toolbar)
- `{{selected_text}}` requires global keyboard hook (P1) to capture selection
- `{{current_wiki_file}}` reads from N4 (Wiki Browser) state
