# Import & Export — Feature Spec

## What the User Accomplishes
The user exports chat conversations to standard formats (Markdown, PDF, JSON) and imports conversations from other AI platforms (ChatGPT, Claude) into MySecondBrain.

## Trigger
- Export: Ctrl+S in Studio, or chat context menu → Export
- Import: Settings → Import, or File menu → Import

## Detailed Behavior

### I1. Export Chat
- **Trigger:** Ctrl+S or context menu → Export
- **Format Selection Dialog:** Radio buttons: Markdown (.md), PDF, JSON
- **Markdown Export:** Includes all messages in current branch with: message role (User/Assistant/Persona name), timestamps, model used per message, token counts. Code blocks with language labels. Images as local file references.
- **PDF Export:** Rendered version of Markdown export. Preserves formatting, code highlighting, images.
- **JSON Export:** Structured JSON with full message data, metadata, and branch information. Suitable for re-import or archival.
- **Destination:** Save file dialog. Default filename: chat title + date.
- **Scope:** Current branch only (not all branches). Toggle option: "Include all branches"
- **Progress:** For long chats, progress bar during export

### I2. Import Chats
- **Trigger:** Settings → Import, or File menu → Import
- **Supported Formats:** ChatGPT export (JSON), Claude export (JSON)
- **File Selection:** File picker. Multiple files supported.
- **Import Process:**
  1. Parse file and validate format
  2. Display preview: chat title, message count, date range
  3. User confirms import
  4. ChatThread created with all messages preserved
  5. Conversation structure (user/assistant roles) maintained
  6. Timestamps preserved
- **Post-Import:** New ChatThread appears in sidebar. User can assign Persona, edit title, etc.
- **Duplicate Detection:** If a chat with same title + same first message exists, warn: "A similar chat already exists. Import anyway?"
- **Failure:** "Could not parse file. Unsupported format or corrupted file."

## Data
- Export: reads [`data/chat-thread.md`](data/chat-thread.md) and [`data/message.md`](data/message.md)
- Import: creates new ChatThread and Messages

## Success/Failure States
- **Export Success:** Toast: "Exported: [filename]"
- **Export Failure:** "Export failed. [reason]"
- **Import Success:** Toast: "Imported 1 chat: '[title]'"
- **Import Failure — Format:** "Could not parse file. Unsupported format or corrupted file."
- **Import Failure — Empty File:** "The selected file contains no recognizable chat data."

## Permissions
- Single-user app.

## Interactions
- I1 exports data from current ChatThread
- I2 creates new ChatThreads (O1)
