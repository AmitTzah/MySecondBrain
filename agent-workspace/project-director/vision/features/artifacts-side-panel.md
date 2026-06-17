# Artifacts & Side Panel File Editing — Feature Spec

## What the User Accomplishes
The user works with AI-generated artifacts (code files, documents, config files) that are versioned and editable. Artifacts appear in a side panel alongside the chat. The user can view version history, compare diffs, switch versions, save to disk, and save to wiki. A global artifacts browser shows all artifacts across all chats.

## Trigger
- AI generates an artifact during conversation (F1)
- User clicks artifact in side panel (F2)
- User navigates to Global Artifacts Browser (F7)

## Detailed Behavior

### F1. AI-Generated Artifacts
- AI produces named artifacts with content (code, recipes, documents, config files, etc.)
- Each artifact has: name (e.g., "app.py"), type (inferred from file extension), content
- Artifact appears in side panel upon generation
- ⚠️ FLAGGED: The mechanism by which AI "produces" an artifact (tool use, structured output, parsing markers) is an architectural decision.

### F2. Side Panel
- Resizable panel that opens to the right of the chat
- **Toggle:** Button in chat header or auto-opens when artifact is generated
- **Content:** Lists all artifacts from current chat by name
- **Click:** Displays artifact content in viewer (F6)
- **Empty State:** "No artifacts in this chat yet."

### F3. Version History
- Each artifact maintains version history (v1, v2, v3, ...)
- When user requests changes to an artifact, AI produces new version
- All versions preserved — nothing overwritten
- **Version List:** Dropdown or list showing all versions with timestamps

### F4. Diff View
- Select any two versions → view side-by-side or unified diff
- Red = removed lines, Green = added lines
- Navigation: "Previous Change" / "Next Change" buttons
- **Access:** Version history panel → "Compare" button

### F5. Version Switching
- Switch which version is "active" (displayed in viewer)
- Active version is what new AI changes are based on
- Reverting to older version + requesting changes = new branch from that version

### F6. Artifact Viewer
- Displays artifact content with syntax highlighting (for code files)
- Rendered Markdown view (for .md files)
- **"Save to Disk" button:** Exports artifact as file to user-chosen path
- **"Save to Wiki" button:** Launches Write-to-Wiki pipeline (N5) with artifact content pre-filling the draft
  - Same pipeline: Preview Panel with inline editing, AI cross-linking (N10), Diff Viewer for updates
  - This bridges chat artifacts → permanent wiki entries

### F7. Global Artifacts Browser
- **Access:** Dedicated tab/screen from Studio sidebar ("Artifacts" nav item)
- **Content:** All artifacts from ALL chats. Columns: name, type, parent chat, created date, version count.
- **Search:** Search bar filters by name
- **Sort:** By name, date, type, chat
- **Filter:** By type (code, markdown, config, other)
- **Click:** Opens artifact in side panel viewer with full version history
- **Empty State:** "No artifacts yet. Artifacts are created when AI generates code, documents, or config files during conversations."
- **Distinction from Media Library (G):** Artifacts are text-based, editable files. Media are images, audio, video.

## Data
- [`data/artifact.md`](data/artifact.md) — artifact metadata, versions, content

## Success/Failure States
- **Success — Save to Disk:** System save dialog. File written. Toast: "Saved: [filename]"
- **Success — Save to Wiki:** Launches N5 pipeline
- **Empty State — No Artifacts in Chat:** "No artifacts in this chat yet."
- **Empty State — Global Browser:** "No artifacts yet."

## Permissions
- Single-user app. All actions available.

## Interactions
- F6 bridges to N5 (Write to Wiki pipeline)
- F7 is separate from G (Media Library) — text artifacts vs media files
- Artifacts tied to ChatThread; deleted if chat deleted and artifact not saved elsewhere (O5)
