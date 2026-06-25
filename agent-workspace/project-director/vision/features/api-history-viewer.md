# API History Viewer — Feature Spec

## What the User Accomplishes

The user views the complete raw JSON API call history for the current chat. Each entry in the log captures the full request/response cycle — user messages, assistant responses with `tool_use` blocks, `tool_result` blocks, `reasoning` blocks, and timestamps. The log is rendered with JSON syntax highlighting in a generic read-only file viewer tab.

This is the raw debugging view that powers the API History feature. The structured, queryable data (token counts, cost, latency, cache stats) lives in the UsageRecord entity and feeds the Usage Dashboard (S).

## Trigger

- User clicks "📡 API History" button in the Studio Chat header bar (near the chat theme selector — Classic/Compact/Bubble)
- The app serializes all API calls for the current chat into a JSON file and opens it in a new read-only file viewer tab

## Detailed Behavior

### Button Placement

The "📡 API History" button appears in the Studio Chat header bar, positioned near the chat theme selector (Classic/Compact/Bubble combo box). It is a standalone button — NOT in the three-dot (⋯) menu. The button is always visible when a chat is active.

### Data Source

- A single JSON log file is maintained per chat: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json`
- Each API call appends a new entry to the JSON array in this file
- The JSON structure follows this format:

```json
[
  {
    "role": "user",
    "content": [
      {
        "type": "text",
        "text": "<user_message>...</user_message>"
      }
    ],
    "ts": 1782297412258
  },
  {
    "role": "assistant",
    "content": [
      {
        "type": "reasoning",
        "text": "...",
        "summary": []
      },
      {
        "type": "tool_use",
        "id": "call_00_abc123",
        "name": "read_file",
        "input": { "path": "some/file.txt" }
      }
    ],
    "ts": 1782297421224
  },
  {
    "role": "user",
    "content": [
      {
        "type": "tool_result",
        "tool_use_id": "call_00_abc123",
        "content": "File: some/file.txt\n[content...]"
      }
    ],
    "ts": 1782297421379
  }
]
```

- `ts` is a Unix timestamp in milliseconds
- `content` is an array that may contain blocks of types: `text`, `reasoning`, `tool_use`, `tool_result`
- `reasoning` blocks include a `summary` array (empty when streaming, may be populated by the model)
- `tool_use` blocks include `id`, `name`, and `input`
- `tool_result` blocks include `tool_use_id` and `content`

### Open Behavior

- Click "📡 API History" → the app reads the `_api_history.json` file for the current chat
- If the file exists and contains data: it is opened in a new read-only file viewer tab alongside chat tabs
- The file viewer provides: JSON syntax highlighting, collapsible sections (if the viewer supports JSON folding), find/search (Ctrl+F), copy, "Save As", "Open in External Editor"
- The tab title is "API History — [Chat Title]"
- If the file does not exist or is empty: the button shows a tooltip "No API calls recorded for this chat yet" and the button is disabled/subtle

### No Custom UI Beyond the Button

The API History viewer has NO custom UI beyond the header button. All viewing, searching, and navigation is handled by the generic file viewer (see [`features/file-viewer-tabs.md`](file-viewer-tabs.md)). There is no modal, no split panel, no collapsible tree — just a JSON file in a file viewer tab.

### Export

- From the file viewer tab: "Save As" saves the JSON file to a user-chosen location
- The user can also use "Open in External Editor" → save from their preferred editor

## Data

- Raw API call JSON stored per chat: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json`
- Structured usage data stored in SQLite: [`data/usage-record.md`](../data/usage-record.md)
- The raw JSON file is NOT the source for the Usage Dashboard — that comes from the UsageRecord entity
- The raw JSON file is cleaned up with the workspace (24h after chat close/delete)

## Success/Failure States

- **File exists with data:** Opens in file viewer tab with JSON syntax highlighting. Tab title: "API History — [Chat Title]".
- **File does not exist (no API calls yet):** Button disabled/subtle. Tooltip: "No API calls recorded for this chat yet."
- **File empty (calls recorded but file corrupted):** Opens with empty JSON array `[]`. User sees an empty file.
- **File too large (very long chat):** The file viewer handles large files. May take a moment to load. "Open in External Editor" recommended for extremely large files (>50MB).

## Permissions

- Single-user app. API history is local data — no permission restrictions.

## Interactions

- Reads from the per-chat raw JSON log file (appended by every ILLMProvider call)
- Uses the generic file viewer for display: [`features/file-viewer-tabs.md`](file-viewer-tabs.md)
- Structured data feeds the Usage Dashboard: [`features/usage-pricing-dashboard.md`](usage-pricing-dashboard.md)
- Raw JSON file cleaned up with workspace (24h grace period after chat close/delete)
