# Artifacts & Side Panel — Feature Spec

## What the User Accomplishes

The user works with AI-generated artifacts (code files, documents, spreadsheets, interactive React apps, config files) that are versioned and viewable in a WebView2-powered side panel. The model creates files in the workspace using `text_editor` or `bash`, then calls `present_files` to surface them as artifacts. The app automatically tracks versions by filename — same filename within a chat = new version. The user can view version history, compare diffs, switch versions, save to disk, and save to wiki. A global artifacts browser shows all artifacts across all chats.

## Trigger

- Model calls `present_files` tool with file paths (H9) during conversation
- User clicks artifact in side panel (F2)
- User navigates to Global Artifacts Browser (F7)
- Model uses `text_editor` to modify a previously-presented file → auto-creates new version (F3)

## Detailed Behavior

### F1. Workspace-to-Artifact Pipeline

The complete flow from model output to side panel:

1. **Model creates files** in the workspace (`%LOCALAPPDATA%/MySecondBrain/workspace/`) using:
   - `text_editor create` — for direct file creation (code, markdown, config)
   - `bash` — for skill-generated files (Excel via xlsx skill, React app via web-artifacts-builder, etc.)
   - Intermediate/temp files stay in workspace and are invisible to the user

2. **Model calls `present_files(["budget.xlsx", "chart.html"])`** — signals "these are done — show them"

3. **App copies files** from workspace to the artifacts directory (persisted with chat)

4. **App surfaces artifacts** in the WebView2-powered side panel. The first file in the `present_files` array is shown first. Multiple files presented in one call all appear.

5. **Version tracking:** If a file with the same name was previously presented in this chat, the app creates a new version (v2, v3...) rather than a new artifact. Version identity is purely by filename within the chat.

**No `create_artifact` tool:** The model does not know about "artifacts." It just writes files and presents them. The artifact concept is entirely client-side — the app's interpretation of presented files. This matches Claude.ai's architecture exactly.

### F2. Side Panel (WebView2-Powered)

- **Technology:** Embedded Microsoft Edge WebView2 control (Chromium-based). Replaces the original WPF-rendered side panel. The chat conversation view remains WPF-native.
- **Location:** Resizable panel to the right of the chat. Top section of the right panel (above Chat Nav).
- **Toggle:** Button in chat header, or auto-opens when `present_files` is called.
- **Content:** Lists all presented artifacts from current chat by name + type icon.
- **Click:** Displays artifact content in the WebView2 viewer (F6).
- **Empty State:** "No artifacts in this chat yet. The AI can create files and present them here."
- **Rendering capabilities (browser-native):**
  - Syntax highlighting: 200+ languages via Prism.js or highlight.js — dramatically better than WPF AvalonEdit
  - Markdown rendering: via marked.js or similar — better than Markdig→FlowDocument
  - Diff views: side-by-side or unified via diff2html.js — colored line-by-line with change navigation
  - Interactive React/Tailwind artifacts: full React app rendering from web-artifacts-builder skill
  - Dark/light theme: CSS class toggled via WPF↔WebView2 JavaScript bridge
  - **Install size:** Adds ~100MB for Edge WebView2 Runtime (pre-installed on Windows 11, auto-installed on Windows 10)

**What stays WPF:**
- Chat conversation rendering (Markdown messages, code blocks inline, thinking blocks, tool call cards) — uses existing ContentBlockRenderers
- Artifact list/sidebar UI (the panel chrome, not the content viewer)
- Version selector dropdown
- Save to Disk / Save to Wiki buttons

### F3. Version History

Versioning is entirely app-side — the app tracks every file write within a chat, not the model.

**How it works:**
- The app watches for `text_editor` writes (create, str_replace, insert) and `present_files` calls
- When a file is created or modified with the same name as a previously-presented artifact → new version snapshot
- When a file is presented with a NEW name → new artifact (v1)
- The model has ZERO awareness of version numbers — it just writes files. The app manages version lineage.

**User-facing:**
- Each artifact shows: "v3" (current) with a version dropdown: v1, v2, v3
- Version dropdown shows timestamp of each version
- Click older version → content updates in viewer. Active version indicator.
- "Latest" badge on the newest version

**Example:**
- User: "Create todo.md" → model: `text_editor.create("todo.md", "...")` + `present_files(["todo.md"])` → app: "todo.md v1"
- User: "Add item 4" → model: `text_editor.str_replace("todo.md", old, new)` → app detects same filename → "todo.md v2"
- User: "Rewrite it entirely" → model: `text_editor.create("todo-v2.md", "...")` + `present_files(["todo-v2.md"])` → NEW filename → "todo-v2.md v1" (separate artifact)

### F4. Diff View

Compare any two versions of the same artifact.

- **Access:** Version history dropdown → "Compare" button. Or select two versions from the version list.
- **Rendering:** WebView2-powered using diff2html.js or similar browser-native diff library.
  - Side-by-side view (default): left = older, right = newer
  - Unified view (toggle): single column with inline changes
  - Color coding: red background = removed lines, green background = added lines
- **Navigation:** "Previous Change" (⬆) / "Next Change" (⬇) buttons. Change count: "3 of 12 changes"
- **Diff computation:** App-side in C# (comparing two artifact versions). WebView2 only renders the computed diff.
- **No model involvement:** Diff viewing is purely a UI operation. The model is not called.

### F5. Version Switching

- Switch which version is "active" (displayed in viewer)
- Active version is what new AI changes are based on: when the user says "change X" and the version selector is on v2, the model sees v2's content as the baseline
- Reverting to older version + requesting changes = new branch from that version

### F6. Artifact Viewer (WebView2)

- **Code files:** Syntax highlighting by language (200+ languages via Prism.js/highlight.js). Line numbers. Copy button.
- **Markdown files:** Rendered Markdown with proper typography, table formatting, link handling.
- **Interactive HTML:** Full React/Tailwind/shadcn/ui rendering for web-artifacts-builder output. Sandboxed iframe.
- **SVG:** Rendered as scalable vector image.
- **PDF:** PDF viewer (browser-native).
- **Unknown types:** Plain text with monospace font.

**Action buttons (WPF-native, around the WebView2):**
- **"Save to Disk" button:** Exports artifact as file to user-chosen path via system save dialog
- **"Save to Wiki" button:** Launches Write-to-Wiki pipeline (N5) with artifact content pre-filling the draft. Same pipeline: Preview Panel with inline editing, AI cross-linking (N10), Diff Viewer for updates.
- **Preview / Code toggle:** Switch between rendered view and raw source (for Markdown and HTML artifacts)

**Inline Section Edit (Markdown artifacts only):**
- When viewing a rendered Markdown artifact, user can highlight any section of text
- Right-click highlighted text → "Edit with AI" → opens a targeted chat prompt pre-populated with the selected section
- Model receives: the full artifact content + the highlighted section + user's edit instruction
- Model proposes a `str_replace` targeting that section
- User reviews the proposed edit in a preview panel → Accept (applied to artifact, new version created) or Refine (iterate)
- This enables surgical edits to long Markdown documents without regenerating the entire file

### F7. Global Artifacts Browser

- **Access:** "📄 Artifacts" in Studio sidebar navigation
- **Content:** All artifacts from ALL chats. Columns: name, type, parent chat, created date, version count.
- **Search:** Search bar filters by name
- **Sort:** By name, date, type, chat
- **Filter:** By type (code, markdown, spreadsheet, document, HTML, config, other)
- **Click:** Opens artifact in side panel viewer with full version history
- **Empty State:** "No artifacts yet. Artifacts are created when the AI writes files and presents them during conversations."
- **Distinction from Media Library (G):** Artifacts are text-based, editable files. Media are images, audio, video.

## Data

- [`data/artifact.md`](../data/artifact.md) — artifact metadata, versions, content
- Artifact files stored in app data directory (persisted with chat)
- Workspace files are temporary (24h cleanup) unless presented

## Success/Failure States

- **Success — present_files:** Files appear in side panel. Toast: "📄 Presented: [filename]" (or "[N] files" for multiple)
- **Success — Save to Disk:** System save dialog. File written. Toast: "Saved: [filename]"
- **Success — Save to Wiki:** Launches N5 pipeline
- **Success — New version created:** Version counter increments silently. Version dropdown updates.
- **Failure — File not found:** "Cannot present [path]: file not found in workspace."
- **Failure — present_files disabled:** Tool not in tools array. Model cannot call it.
- **Empty State — No Artifacts in Chat:** "No artifacts in this chat yet."
- **Empty State — Global Browser:** "No artifacts yet."
- **WebView2 unavailable:** Falls back to WPF-based rendering with limited syntax highlighting. Error banner: "WebView2 runtime not available. Install Microsoft Edge WebView2 for full artifact rendering."
- **Artifact render error:** WebView2 fails to render content (e.g., malformed HTML, unsupported format). Error banner: "⚠️ Could not render this artifact. [Reload] [Send error to chat]." "Send error to chat" copies the error details into the chat input as a new message, allowing the model to diagnose and fix the issue.

## Permissions

- Single-user app. All actions available.
- `present_files` auto-approval configurable in H10. Default: Auto-Approve (presenting files is non-destructive).
- Workspace isolation: model cannot access files outside workspace without user confirmation (H1).

## Interactions

- F1 triggered by H9 (present_files tool), written via H2 (text_editor) or H1 (bash)
- F3 auto-tracks versions from H2 writes and H9 calls
- F6 bridges to N5 (Write to Wiki pipeline)
- F7 is separate from G (Media Library) — text artifacts vs media files
- Artifacts tied to ChatThread; deleted if chat deleted and artifact not saved elsewhere (O5)
- WebView2 rendering in F2/F4/F6 — separate from WPF chat rendering
- web-artifacts-builder skill (W1) produces interactive HTML artifacts rendered in F6
