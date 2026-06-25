# Generic File Viewer Tabs — Feature Spec

## What the User Accomplishes

The user opens arbitrary files as read-only tabs in the main content area, alongside chat tabs. The file viewer provides syntax highlighting for text/code files, rendered Markdown for .md files, and zoom/pan for images. Files can be opened via drag & drop, File → Open menu, double-click in file picker, model's `present_files` results, or the API History button. Tabs behave like chat tabs — reorderable, closeable, reopenable.

This is distinct from:
- **Wiki Browser:** Handles wiki .md files with backlinks, indexing, related sections, and file tree navigation
- **Artifacts Panel:** WebView2-powered right panel for AI-generated artifacts with version history and diff views
- **Media Library:** Gallery grid for media (images, audio, video) across all chats

## Trigger

- User drags a file from Windows Explorer onto the tab bar
- File → Open menu (Ctrl+O)
- Double-click a file in a file picker dialog within the app
- Model calls `present_files` — user clicks a presented file to open in main area
- User clicks "📡 API History" button in chat header (opens `_api_history.json`)
- User clicks an image thumbnail from a message (C9a)
- User drags a file viewer tab onto the chat textbox to include as context

## Detailed Behavior

### Supported File Types

| Category | Extensions | Rendering |
|----------|-----------|-----------|
| **Text** | .txt, .log | Plain text with monospace font. Find/search (Ctrl+F). |
| **Markdown** | .md | Rendered Markdown (headings, bold, italic, code blocks with syntax highlighting, lists, links, tables, blockquotes). Same rendering engine as Wiki Browser viewer region, but WITHOUT backlinks, indexing, related sections, or file tree navigation. |
| **Code** | .json, .xml, .yaml, .yml, .csv, .py, .js, .ts, .jsx, .tsx, .html, .css, .cs, .java, .c, .cpp, .h, .rs, .go, .rb, .php, .swift, .kt, .sql, .sh, .ps1, .bat, .toml, .ini, .cfg, .env (and all other common code extensions) | Syntax highlighting by declared language (same engine as code blocks in chat — C3). Line numbers. Copy button on hover. Find/search (Ctrl+F). |
| **Images** | .png, .jpg, .jpeg, .gif, .webp, .bmp, .svg | Full-size viewer with zoom (mouse wheel / +/- buttons) and pan (click-drag). Fit-to-window toggle. Copy to Clipboard button. |
| **Unsupported** | All other extensions | Plain text rendering with monospace font. Warning banner: "File type not natively supported — displayed as plain text. [Open in External Editor]" |

### Open Methods

1. **Drag & Drop onto Tab Bar:** User drags a file from Windows Explorer onto the tab bar area. A new read-only file viewer tab opens with the file's content.
2. **File → Open Menu (Ctrl+O):** Standard file open dialog. Supports multi-select — each selected file opens in its own tab.
3. **Double-Click in File Picker:** Any file picker dialog within the app that lists files (e.g., workspace file list).
4. **Model's present_files Results:** When the model calls `present_files`, the surfaced artifacts appear in the Artifacts panel (F2). The user can click an artifact entry to open it in a file viewer tab in the main content area (separate from the WebView2 artifact viewer).
5. **API History Button:** "📡 API History" in chat header opens `_api_history.json` in a file viewer tab.
6. **Click Image Thumbnail (C9a):** Clicking an attached image thumbnail in the attachment row opens the image in a file viewer tab (full-size viewer with zoom/pan).

### Tab Behavior

File viewer tabs behave identically to chat tabs:
- **Reorder:** Drag-and-drop tabs
- **Close:** X button on tab or Ctrl+W
- **Reopen:** Ctrl+Shift+T reopens the last closed file viewer tab
- **Overflow:** When tabs exceed width, scrollable tab bar with arrows
- **Tab Title:** Filename (truncated if needed). File viewer tabs show a 📄 icon prefix to distinguish from chat tabs.
- **Read-Only Indicator:** A small "Read-Only" badge or lock icon on the tab.

### Tab Context (Dragging to Textbox)

A file viewer tab can be dragged onto the chat textbox to include the file's content as context:
- **Text/code/markdown files:** The file's full text content is included in the chat prompt. If the file exceeds the model's context window, it is truncated with a notice: "⚠️ File '[filename]' was truncated — [N] of [M] characters included."
- **Image files:** The image is attached as a vision input (same as C9). If the active model doesn't support vision, a warning badge appears (C9c).
- **Unsupported files:** Only filename, type, and size metadata are included (same as C9 behavior for unsupported types).
- **Visual feedback during drag:** The textbox highlights with a dashed border and shows "Drop file to include as context."

### Read-Only Enforcement

- No editing capability in the file viewer. The content display is strictly read-only.
- An "Open in External Editor" button is available in the file viewer toolbar — opens the file in the system default application for that file type.
- For workspace files: opens in the default system editor (e.g., VS Code for .cs files, Notepad for .txt).
- For API History JSON files: opens in the system default JSON viewer/editor.

### File Viewer Toolbar

Each file viewer tab has a minimal toolbar above the content:

| Element | Action |
|---------|--------|
| 📄 Filename | Read-only display |
| Read-Only badge | Indicator |
| Fit/Full size toggle (images only) | Toggle between fit-to-window and 100% zoom |
| Zoom in/out (images only) | +/- buttons |
| Copy to Clipboard (images only) | Copies image data to clipboard |
| Find (Ctrl+F) (text/code only) | Opens find bar within the viewer |
| Open in External Editor | Opens file in system default application |
| Save As... | Saves a copy of the file to a user-chosen location |

## Data

- Files are read from disk — no separate data entity for the file viewer
- API History JSON files: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/_api_history.json`
- Workspace files: `%LOCALAPPDATA%/MySecondBrain/workspace/{chat-id}/` (bash tool working directory)
- Artifact files: `%LOCALAPPDATA%/MySecondBrain/artifacts/`
- Wiki files: user-configured wiki directory

## Success/Failure States

- **File opened successfully:** Content rendered with appropriate viewer (syntax highlighting, Markdown, or image viewer).
- **File not found:** "File '[path]' no longer exists. It may have been moved or deleted." Tab closes automatically.
- **File too large (>100MB):** "This file is too large to preview ([size]). [Open in External Editor]" The file viewer shows a placeholder. The file is not loaded into memory.
- **Unsupported image format:** Falls back to "Open in External Editor" with a generic file icon.
- **Encoding error (binary file opened as text):** "Cannot display this file — it appears to be a binary file. [Open in External Editor]"
- **File locked by another process:** "Cannot open file — it is being used by another process. [Retry]"

## Permissions

- Single-user app. Files are read from disk with the user's own file permissions.
- If the file is in a location the app cannot access (permission denied by OS): "Cannot access file — permission denied. [path]"

## Interactions

- Used by API History Viewer: [`features/api-history-viewer.md`](api-history-viewer.md)
- Image thumbnails from C9a (paste image) open here when clicked
- Drag tab to textbox feeds into C9 (file attachment for chat context)
- Distinct from: F2 (Artifacts panel — WebView2, versioned, diff view), N4 (Wiki Browser — indexed .md with backlinks), G1 (Media Library — gallery grid)
- Present files results (F1) can open here as an alternative to the WebView2 artifact viewer
